using Microsoft.EntityFrameworkCore;
using BoardGame.Data;
using BoardGame.Models;
using BoardGame.Views;

namespace BoardGame.Services
{
    public static class CatalogService
    {
        // Сколько штук каждой игры продано в предыдущем месяце (относительно даты day).
        // Учитываются только неотменённые заказы. Ключ — product_id.
        public static async Task<Dictionary<int, int>> GetSoldLastMonthAsync(BoardGameContext db, DateOnly day)
        {
            var (start, end) = PriceRules.PreviousMonth(day);
            DateTime from = start.ToDateTime(TimeOnly.MinValue);
            DateTime to = end.ToDateTime(TimeOnly.MinValue);
            int cancelledId = await OrderService.StatusIdAsync(db, OrderService.StatusCancelled);

            return await db.OrderItems
                .Where(i => i.Order.StatusId != cancelledId &&
                            i.Order.CreatedAt >= from &&
                            i.Order.CreatedAt < to)
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Count = g.Sum(i => i.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Count);
        }

        // Текущая цена игры (с учётом скидки) — используется формой товара и корзиной.
        public static async Task<decimal> GetCurrentPriceAsync(BoardGameContext db, Product product)
        {
            var sold = await GetSoldLastMonthAsync(db, DateOnly.FromDateTime(DateTime.Today));
            return PriceRules.Price(product.Price, sold.GetValueOrDefault(product.ProductId) > 0);
        }

        // Строка характеристик: «2–4 игрока · 8+ · 45 мин»
        public static string DetailsText(Product p)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(p.Players)) parts.Add($"{p.Players} игр.");
            if (p.MinAge != null) parts.Add($"{p.MinAge}+");
            if (p.PlayTime != null) parts.Add($"{p.PlayTime} мин");
            return string.Join(" · ", parts);
        }

        public static async Task<List<ProductCard>> LoadCardsAsync()
        {
            using var db = Db.Create();

            // Просроченные брони снимаются, а товар возвращается на склад
            await OrderService.ReleaseExpiredAsync(db);

            List<Product> products = await db.Products
                .Include(p => p.Category)
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync();

            // Продажи каждой игры за прошлый месяц — одним запросом
            Dictionary<int, int> sold = await GetSoldLastMonthAsync(db, DateOnly.FromDateTime(DateTime.Today));

            var cards = new List<ProductCard>();

            foreach (Product p in products)
            {
                int soldCount = sold.GetValueOrDefault(p.ProductId);
                decimal price = PriceRules.Price(p.Price, soldCount > 0);
                bool discount = soldCount == 0;
                bool popular = soldCount >= PriceRules.PopularThreshold;
                bool lowStock = PriceRules.IsLowStock(p.StockQuantity);

                // Плашка: нет в наличии → скидка → хит → обычная
                string tone; string badge;
                if (p.StockQuantity == 0) { tone = "Neutral"; badge = "Нет в наличии"; }
                else if (discount) { tone = "Warning"; badge = $"Скидка {PriceRules.StaleDiscount:0}%"; }
                else if (popular) { tone = "Accent"; badge = "Хит продаж"; }
                else { tone = "Success"; badge = "В наличии"; }

                cards.Add(new ProductCard
                {
                    Id = p.ProductId,
                    Article = p.Article,
                    Name = p.Name,
                    Category = p.Category?.Name ?? "",
                    Description = string.IsNullOrWhiteSpace(p.Description) ? "Описание отсутствует" : p.Description,
                    Details = DetailsText(p),

                    BasePrice = p.Price,
                    Price = price,
                    PriceText = $"{price:N0} руб.",
                    OldPriceText = discount ? $"{p.Price:N0} руб." : "",
                    HasDiscount = discount,

                    Stock = p.StockQuantity,
                    StockText = p.StockQuantity == 0 ? "Нет на складе"
                              : lowStock ? $"Осталось {p.StockQuantity} шт."
                              : $"На складе: {p.StockQuantity} шт.",

                    SoldCount = soldCount,
                    BadgeText = badge,
                    BadgeBackground = UiSupport.Brush(tone + "SoftBrush"),
                    BadgeForeground = UiSupport.Brush(tone + "Brush"),
                    CardBackground = UiSupport.Brush(lowStock ? "LowStockBrush" : "NormalBrush"),

                    Image = UiSupport.Picture(p.ImagePath)
                });
            }
            return cards;
        }
    }
}
