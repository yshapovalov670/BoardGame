using System.Data;
using Microsoft.EntityFrameworkCore;
using BoardGame.Data;
using BoardGame.Models;

namespace BoardGame.Services
{
    public static class OrderService
    {
        // Названия статусов — ТОЧНО как в таблице Statuses.
        // Идентификаторы ищем по названию, а не «зашиваем» числа в код.
        public const string StatusReserved = "Оформлен";
        public const string StatusIssued = "Выдан";
        public const string StatusCancelled = "Отменён";

        /// <summary>
        /// Найти status_id по названию статуса.
        /// </summary>
        public static async Task<int> StatusIdAsync(BoardGameContext db, string name)
        {
            var status = await db.Statuses.AsNoTracking().FirstOrDefaultAsync(s => s.Name == name);
            if (status == null)
                throw new AppException($"Статус «{name}» не найден в базе данных.");
            return status.StatusId;
        }

        /// <summary>
        /// Оформить заказ из корзины: проверка остатков, фиксация цен,
        /// списание товара со склада и бронь на 3 дня — одной транзакцией.
        /// </summary>
        public static async Task<int> CreateOrderAsync(IReadOnlyDictionary<int, int> items)
        {
            // 1. Проверка прав доступа
            if (Session.CurrentUser == null)
                throw new AppException("Гость не может оформить заказ. Войдите в систему.");

            if (!Session.CanBuy)
                throw new AppException("Продавцы не оформляют заказы от своего имени.");

            // 2. Проверка введённых данных
            if (items.Count == 0)
                throw new AppException("Корзина пуста.");

            foreach (var (_, quantity) in items)
            {
                if (quantity <= 0)
                    throw new AppException("Количество товара должно быть больше нуля.");
                if (quantity > PriceRules.MaxPerItem)
                    throw new AppException($"Не больше {PriceRules.MaxPerItem} шт. одной игры в заказе.");
            }

            using var db = Db.Create();

            // === НАЧАЛО ТРАНЗАКЦИИ ===
            // Serializable защищает от одновременной покупки последнего экземпляра
            using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var ids = items.Keys.ToList();
                var products = await db.Products.Where(p => ids.Contains(p.ProductId)).ToListAsync();

                // 3. Проверка существования товара и остатков на складе
                foreach (var (productId, quantity) in items)
                {
                    var product = products.FirstOrDefault(p => p.ProductId == productId)
                        ?? throw new AppException("Один из товаров удалён из каталога. Обновите корзину.");

                    if (product.StockQuantity < quantity)
                        throw new AppException(
                            $"«{product.Name}»: на складе {product.StockQuantity} шт., а в корзине {quantity} шт.");
                }

                // 4. Цены на момент оформления (со скидкой за отсутствие продаж в прошлом месяце)
                DateOnly today = DateOnly.FromDateTime(DateTime.Today);
                var sold = await CatalogService.GetSoldLastMonthAsync(db, today);

                var order = new Order
                {
                    UserId = Session.CurrentUser.UserId,
                    CreatedAt = DateTime.Now,
                    ReserveUntil = PriceRules.ReserveUntil(today),
                    StatusId = await StatusIdAsync(db, StatusReserved)
                };

                foreach (var (productId, quantity) in items)
                {
                    var product = products.First(p => p.ProductId == productId);

                    order.OrderItems.Add(new OrderItem
                    {
                        ProductId = productId,
                        Quantity = quantity,
                        // Историческая цена: дальнейшее изменение прайса заказ не затронет
                        Price = PriceRules.Price(product.Price, sold.GetValueOrDefault(productId) > 0)
                    });

                    // 5. Списание со склада
                    product.StockQuantity -= quantity;
                }

                db.Orders.Add(order);
                await db.SaveChangesAsync();

                // 6. Подтверждение транзакции
                await transaction.CommitAsync();
                return order.OrderId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Снять просроченные брони: заказы «Оформлен» с истёкшим сроком
        /// получают статус «Отменён», товар возвращается на склад.
        /// </summary>
        public static async Task<int> ReleaseExpiredAsync(BoardGameContext db)
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.Today);
            int reservedId = await StatusIdAsync(db, StatusReserved);

            var expired = await db.Orders
                .Include(o => o.OrderItems).ThenInclude(i => i.Product)
                .Where(o => o.StatusId == reservedId && o.ReserveUntil < today)
                .ToListAsync();

            if (expired.Count == 0) return 0;

            int cancelledId = await StatusIdAsync(db, StatusCancelled);
            foreach (var order in expired)
                ReturnToStock(order, cancelledId);

            await db.SaveChangesAsync();
            return expired.Count;
        }

        // Вернуть позиции заказа на склад и отметить заказ отменённым.
        private static void ReturnToStock(Order order, int cancelledId)
        {
            foreach (var item in order.OrderItems)
                item.Product.StockQuantity += item.Quantity;

            order.StatusId = cancelledId;
        }

        private static IQueryable<Order> WithDetails(BoardGameContext db) => db.Orders
            .Include(o => o.User)
            .Include(o => o.Employee)
            .Include(o => o.Status)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product);

        /// <summary>
        /// Получить список заказов в зависимости от роли пользователя.
        /// </summary>
        public static async Task<List<Order>> GetOrdersAsync()
        {
            if (Session.CurrentUser == null) return new List<Order>();

            using var db = Db.Create();
            await ReleaseExpiredAsync(db);

            IQueryable<Order> query = WithDetails(db);

            // Сотрудники (администратор, продавец) видят ВСЕ заказы, покупатель — только свои.
            if (!Session.IsStaff)
                query = query.Where(o => o.UserId == Session.CurrentUser.UserId);

            return await query
                .OrderByDescending(o => o.CreatedAt)
                .AsNoTracking()
                .AsSplitQuery()
                .ToListAsync();
        }

        /// <summary>
        /// Получить один конкретный заказ со всеми деталями (с проверкой доступа).
        /// </summary>
        public static async Task<Order?> GetOrderAsync(int orderId)
        {
            using var db = Db.Create();

            var order = await WithDetails(db)
                .AsNoTracking()
                .AsSplitQuery()
                .SingleOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null) return null;

            if (!Session.IsStaff && order.UserId != Session.CurrentUser?.UserId)
                throw new AppException("Нет доступа к этому заказу.");

            return order;
        }

        /// <summary>
        /// Можно ли отменить заказ: только оформленный, владелец или сотрудник.
        /// </summary>
        public static bool CanCancel(Order order) =>
            order.Status?.Name == StatusReserved &&
            (Session.IsStaff || order.UserId == Session.CurrentUser?.UserId);

        /// <summary>
        /// Можно ли выдать заказ покупателю: только оформленный и только сотрудник.
        /// </summary>
        public static bool CanIssue(Order order) =>
            order.Status?.Name == StatusReserved && Session.IsStaff;

        /// <summary>
        /// Отменить заказ: статус «Отменён», товар возвращается на склад.
        /// Повторная отмена невозможна — товар не вернётся дважды.
        /// </summary>
        public static async Task CancelOrderAsync(int orderId)
        {
            using var db = Db.Create();
            using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var order = await db.Orders
                .Include(o => o.Status)
                .Include(o => o.OrderItems).ThenInclude(i => i.Product)
                .SingleOrDefaultAsync(o => o.OrderId == orderId)
                ?? throw new AppException("Заказ не найден.");

            if (!CanCancel(order))
                throw new AppException("Отменить можно только свой оформленный (не выданный) заказ.");

            ReturnToStock(order, await StatusIdAsync(db, StatusCancelled));

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        /// <summary>
        /// Выдать заказ покупателю (статус «Выдан»), фиксируется продавец.
        /// </summary>
        public static async Task IssueOrderAsync(int orderId)
        {
            using var db = Db.Create();

            var order = await db.Orders
                .Include(o => o.Status)
                .SingleOrDefaultAsync(o => o.OrderId == orderId)
                ?? throw new AppException("Заказ не найден.");

            if (!CanIssue(order))
                throw new AppException("Выдать можно только оформленный заказ (роль продавца или администратора).");

            order.StatusId = await StatusIdAsync(db, StatusIssued);
            order.EmployeeId = Session.CurrentUser!.UserId;
            await db.SaveChangesAsync();
        }
    }
}
