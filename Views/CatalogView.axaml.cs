using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BoardGame.Services; // Для ProductCard, CatalogService, Cart и Session

namespace BoardGame.Views
{
    public partial class CatalogView : UserControl
    {
        private const string AllCategories = "Все категории";

        private List<ProductCard> allCards = new();

        public CatalogView()
        {
            InitializeComponent();
            SortBox.SelectedIndex = 0;

            if (Session.IsSeller)
                SubtitleText.Text = "Просмотр ассортимента и остатков (продавцы не оформляют заказы)";
            else if (Session.IsGuest)
                SubtitleText.Text = "Войдите в систему, чтобы добавлять игры в корзину";

            Loaded += async (s, e) => await LoadCardsAsync();
        }

        // ЗАГРУЗКА ДАННЫХ ИЗ БАЗЫ
        private async Task LoadCardsAsync()
        {
            try
            {
                allCards = await CatalogService.LoadCardsAsync();

                // Список категорий для фильтра строится по загруженным товарам
                string? selected = CategoryBox.SelectedItem as string;
                var categories = new List<string> { AllCategories };
                categories.AddRange(allCards.Select(c => c.Category).Distinct().OrderBy(c => c));
                CategoryBox.ItemsSource = categories;
                CategoryBox.SelectedItem = selected != null && categories.Contains(selected) ? selected : AllCategories;

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MainWindow.Instance.ShowError(ex);
            }
        }

        // ПОИСК, ФИЛЬТРАЦИЯ И СОРТИРОВКА (на стороне клиента, по загруженному списку)
        private void ApplyFilters()
        {
            string text = (SearchBox.Text ?? "").Trim();
            var ic = StringComparison.OrdinalIgnoreCase;
            string category = CategoryBox.SelectedItem as string ?? AllCategories;

            IEnumerable<ProductCard> rows = allCards.Where(c =>
                (text == "" || c.Name.Contains(text, ic) || c.Article.Contains(text, ic) || c.Description.Contains(text, ic)) &&
                (category == AllCategories || c.Category == category) &&
                (InStockOnly.IsChecked != true || c.InStock));

            rows = SortBox.SelectedIndex switch
            {
                1 => rows.OrderBy(c => c.Price).ThenBy(c => c.Name),
                2 => rows.OrderByDescending(c => c.Price).ThenBy(c => c.Name),
                3 => rows.OrderByDescending(c => c.SoldCount).ThenBy(c => c.Name),
                _ => rows.OrderBy(c => c.Name)
            };

            List<ProductCard> result = rows.ToList();

            CardsList.ItemsSource = result;
            EmptyPanel.IsVisible = result.Count == 0 && allCards.Count > 0;
            CountText.Text = $"Найдено: {result.Count} из {allCards.Count}";
        }

        private void SearchChanged(object? sender, TextChangedEventArgs e) => ApplyFilters();

        private void FilterChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (CardsList != null) ApplyFilters();
        }

        private void InStockChanged(object? sender, RoutedEventArgs e)
        {
            if (CardsList != null) ApplyFilters();
        }

        private async void ReloadClick(object? sender, RoutedEventArgs e) => await LoadCardsAsync();

        // Может ли текущий пользователь покупать; иначе — подсказка
        private static bool CheckCanBuy()
        {
            if (Session.IsGuest)
            {
                MainWindow.Instance.ShowWarning("Чтобы оформить заказ, войдите в систему.");
                MainWindow.Instance.OpenPage(new LoginView());
                return false;
            }

            if (!Session.CanBuy)
            {
                MainWindow.Instance.ShowWarning("Продавцы не оформляют заказы.");
                return false;
            }
            return true;
        }

        // КНОПКА «В КОРЗИНУ» — добавляет 1 шт.
        private void AddToCartClick(object? sender, RoutedEventArgs e)
        {
            e.Handled = true; // клик по кнопке не должен открывать карточку товара
            if (sender is not Button { Tag: ProductCard card }) return;
            if (!CheckCanBuy()) return;

            try
            {
                Cart.Add(card.Id, 1, card.Stock);
                MainWindow.Instance.ShowMessage($"«{card.Name}» добавлена в корзину (в корзине: {Cart.QuantityOf(card.Id)} шт.).");
            }
            catch (Exception ex)
            {
                MainWindow.Instance.ShowError(ex);
            }
        }

        // КЛИК ПО КАРТОЧКЕ — переход к описанию игры
        private void Card_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Border { Tag: ProductCard card }) return;
            MainWindow.Instance.OpenPage(new ProductView { ProductId = card.Id });
        }
    }
}
