using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using BoardGame.Data;      // Db.Create()
using BoardGame.Models;    // Product
using BoardGame.Services;  // CatalogService, PriceRules, Cart, Session

namespace BoardGame.Views
{
    public partial class ProductView : UserControl
    {
        // ID игры, переданный из каталога
        public int ProductId { get; set; }

        private Product? product;
        private decimal price;   // цена за штуку с учётом скидки

        public ProductView()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                using var db = Db.Create();

                product = await db.Products
                    .Include(p => p.Category)
                    .AsNoTracking()
                    .SingleOrDefaultAsync(p => p.ProductId == ProductId);

                if (product == null)
                {
                    MainWindow.Instance.ShowError(new AppException("Игра не найдена в базе данных."));
                    MainWindow.Instance.GoBack();
                    return;
                }

                price = await CatalogService.GetCurrentPriceAsync(db, product);

                NameText.Text = product.Name;
                CategoryText.Text = product.CategoryName;
                ProductImage.Source = UiSupport.Picture(product.ImagePath);
                DescriptionText.Text = string.IsNullOrWhiteSpace(product.Description)
                    ? "Описание отсутствует" : product.Description;
                PlayersText.Text = string.IsNullOrWhiteSpace(product.Players) ? "—" : product.Players;
                AgeText.Text = product.MinAge == null ? "—" : $"{product.MinAge}+";
                TimeText.Text = product.PlayTime == null ? "—" : $"{product.PlayTime} мин";
                ArticleText.Text = product.Article;
                StockText.Text = product.StockQuantity == 0 ? "нет в наличии" : $"{product.StockQuantity} шт.";

                // Нельзя выбрать больше остатка (с учётом того, что уже лежит в корзине)
                int available = Math.Min(product.StockQuantity - Cart.QuantityOf(product.ProductId), PriceRules.MaxPerItem);
                QuantityBox.Maximum = Math.Max(available, 1);
                QuantityBox.Value = 1;

                ConfigureForRole(available);
                UpdatePrice();
            }
            catch (Exception ex)
            {
                MainWindow.Instance.ShowError(ex);
            }
        }

        // Кнопки покупки доступны только покупателю и администратору
        private void ConfigureForRole(int available)
        {
            string? hint = null;
            if (Session.IsGuest) hint = "Войдите в систему, чтобы оформить заказ.";
            else if (!Session.CanBuy) hint = "Продавцы не оформляют заказы.";
            else if (product!.StockQuantity == 0) hint = "Игры нет на складе.";
            else if (available <= 0) hint = "Весь доступный остаток уже в вашей корзине.";

            HintText.Text = hint;
            HintText.IsVisible = hint != null;
            AddButton.IsEnabled = hint == null || Session.IsGuest;
            QuantityBox.IsEnabled = hint == null;
            CartButton.IsVisible = Session.CanBuy;
        }

        private void QuantityChanged(object? sender, NumericUpDownValueChangedEventArgs e) => UpdatePrice();

        // Пересчёт суммы: цена × количество
        private void UpdatePrice()
        {
            if (product == null) return;
            int quantity = (int)(QuantityBox.Value ?? 1);

            PriceLabel.Text = $"Цена · {price:N0} × {quantity} шт.";
            PriceText.Text = $"{PriceRules.LineTotal(price, quantity):N0} руб.";

            bool discount = price < product.Price;
            DiscountText.IsVisible = discount;
            DiscountText.Text = $"Скидка {PriceRules.StaleDiscount:0}% (было {product.Price:N0} руб.): в прошлом месяце игру не покупали";

            TotalText.Text = $"{PriceRules.LineTotal(price, quantity):N0} руб.";
        }

        private void AddClick(object? sender, RoutedEventArgs e)
        {
            if (product == null) return;

            if (Session.IsGuest)
            {
                MainWindow.Instance.ShowWarning("Чтобы оформить заказ, войдите в систему.");
                MainWindow.Instance.OpenPage(new LoginView());
                return;
            }

            try
            {
                Cart.Add(product.ProductId, (int)(QuantityBox.Value ?? 1), product.StockQuantity);
                MainWindow.Instance.ShowMessage($"«{product.Name}» добавлена в корзину.");
                MainWindow.Instance.GoBack();
            }
            catch (Exception ex)
            {
                MainWindow.Instance.ShowError(ex);
            }
        }

        private void GoToCartClick(object? sender, RoutedEventArgs e) =>
            MainWindow.Instance.OpenPage(new CartView(), clearHistory: true);

        private void BackClick(object? sender, RoutedEventArgs e) => MainWindow.Instance.GoBack();
    }
}
