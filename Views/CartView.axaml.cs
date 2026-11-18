using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using BoardGame.Data;
using BoardGame.Services;

namespace BoardGame.Views
{
    // Строка корзины для отображения (товар + количество + цена со скидкой)
    public class CartLine
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = "";
        public Bitmap? Image { get; set; }
        public decimal BasePrice { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public int Stock { get; set; }

        public int MaxQuantity => Math.Max(1, Math.Min(Stock, PriceRules.MaxPerItem));
        public string PriceText => Price < BasePrice
            ? $"{Price:N0} руб./шт. (скидка {PriceRules.StaleDiscount:0}%)"
            : $"{Price:N0} руб./шт.";
        public string StockText => $" · на складе {Stock} шт.";
        public string LineTotalText => $"{PriceRules.LineTotal(Price, Quantity):N0} руб.";
    }

    public partial class CartView : UserControl
    {
        private List<CartLine> lines = new();

        public CartView()
        {
            InitializeComponent();
            ReserveText.Text = $"Заказ бронируется на {PriceRules.ReserveDays} дня — до " +
                               $"{PriceRules.ReserveUntil(DateOnly.FromDateTime(DateTime.Today)):dd.MM.yyyy}. " +
                               "Оплата и получение — в магазине.";
            Loaded += async (s, e) => await LoadLinesAsync();
        }

        private async Task LoadLinesAsync()
        {
            try
            {
                using var db = Db.Create();
                var ids = Cart.Items.Keys.ToList();

                var products = await db.Products.AsNoTracking()
                    .Where(p => ids.Contains(p.ProductId))
                    .ToListAsync();
                var sold = await CatalogService.GetSoldLastMonthAsync(db, DateOnly.FromDateTime(DateTime.Today));

                // Товар могли удалить из каталога — убираем его из корзины
                foreach (int id in ids.Where(id => products.All(p => p.ProductId != id)).ToList())
                    Cart.Remove(id);

                lines = products
                    .OrderBy(p => p.Name)
                    .Select(p => new CartLine
                    {
                        ProductId = p.ProductId,
                        Name = p.Name,
                        Image = UiSupport.Picture(p.ImagePath),
                        BasePrice = p.Price,
                        Price = PriceRules.Price(p.Price, sold.GetValueOrDefault(p.ProductId) > 0),
                        Quantity = Cart.QuantityOf(p.ProductId),
                        Stock = p.StockQuantity
                    })
                    .ToList();

                LinesList.ItemsSource = lines;
                UpdateSummary();
            }
            catch (Exception ex)
            {
                MainWindow.Instance.ShowError(ex);
            }
        }

        // Итог: S = Σ (Pi × Qi); скидка = сумма без скидки − итог
        private void UpdateSummary()
        {
            decimal total = PriceRules.Total(lines.Select(l => (l.Price, l.Quantity)));
            decimal baseSum = PriceRules.Total(lines.Select(l => (l.BasePrice, l.Quantity)));

            CountText.Text = $"{lines.Sum(l => l.Quantity)} шт.";
            BaseSumText.Text = $"{baseSum:N0} руб.";
            DiscountText.Text = baseSum > total ? $"−{baseSum - total:N0} руб." : "0 руб.";
            TotalText.Text = $"{total:N0} руб.";

            EmptyPanel.IsVisible = lines.Count == 0;
            CheckoutButton.IsEnabled = lines.Count > 0;
        }

        private void QuantityChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            if (sender is not NumericUpDown { Tag: CartLine line }) return;

            int quantity = (int)(e.NewValue ?? 1);
            if (quantity == line.Quantity) return; // значение не изменилось (первичная привязка)

            line.Quantity = quantity;
            Cart.Set(line.ProductId, line.Quantity);

            // Перерисовываем список, чтобы обновилась сумма строки
            LinesList.ItemsSource = null;
            LinesList.ItemsSource = lines;
            UpdateSummary();
        }

        private void RemoveClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: CartLine line }) return;

            Cart.Remove(line.ProductId);
            lines.Remove(line);
            LinesList.ItemsSource = null;
            LinesList.ItemsSource = lines;
            UpdateSummary();
        }

        private void ClearClick(object? sender, RoutedEventArgs e)
        {
            Cart.Clear();
            lines.Clear();
            LinesList.ItemsSource = null;
            UpdateSummary();
        }

        // Оформление: все проверки (остатки, права, цены) выполняет сервис в транзакции
        private async void CheckoutClick(object? sender, RoutedEventArgs e)
        {
            CheckoutButton.IsEnabled = false;
            try
            {
                int orderId = await OrderService.CreateOrderAsync(Cart.Items);
                Cart.Clear();

                MainWindow.Instance.OpenPage(new OrderDetailsView { OrderId = orderId }, clearHistory: true);
                MainWindow.Instance.ShowMessage($"Заказ № {orderId} оформлен! Заберите его в магазине в течение {PriceRules.ReserveDays} дней.");
            }
            catch (Exception ex)
            {
                MainWindow.Instance.ShowError(ex);
                // Возможно, остатки изменились — обновим корзину
                await LoadLinesAsync();
            }
            finally
            {
                CheckoutButton.IsEnabled = lines.Count > 0;
            }
        }

        private void CatalogClick(object? sender, RoutedEventArgs e) =>
            MainWindow.Instance.OpenPage(new CatalogView(), clearHistory: true);
    }
}
