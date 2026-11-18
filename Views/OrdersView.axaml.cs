using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BoardGame.Models;
using BoardGame.Services;

namespace BoardGame.Views
{
    public partial class OrdersView : UserControl
    {
        private List<Order> allOrders = new();

        public OrdersView()
        {
            InitializeComponent();
            StatusFilter.SelectedIndex = 0;
            ConfigureForRole();
            Loaded += async (s, e) => await LoadOrdersAsync();
        }

        // Заголовок, кнопки и подписи зависят от роли
        private void ConfigureForRole()
        {
            if (Session.IsStaff)
            {
                TitleText.Text = "Все заказы";
                SubtitleText.Text = "Заказы покупателей: выдача и отмена броней";
                SumLabel.Text = "ВЫРУЧКА (ВЫДАННЫЕ)";
                NewButton.IsVisible = Session.CanBuy;
            }
            else
            {
                TitleText.Text = "Мои заказы";
                SumLabel.Text = "СУММА ПОКУПОК";
                OrdersList.Classes.Add("own");
            }
        }

        private async Task LoadOrdersAsync()
        {
            try
            {
                allOrders = await OrderService.GetOrdersAsync();

                ReservedCount.Text = allOrders.Count(o => o.IsReserved).ToString();
                IssuedCount.Text = allOrders.Count(o => o.IsIssued).ToString();

                // Выручка/сумма покупок считается только по выданным (оплаченным) заказам
                decimal sum = allOrders.Where(o => o.IsIssued).Sum(o => o.TotalCost);
                SumText.Text = $"{sum:N0} руб.";

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MainWindow.Instance.ShowError(ex);
            }
        }

        private void ApplyFilters()
        {
            string text = (SearchBox.Text ?? "").Trim();
            var ic = StringComparison.OrdinalIgnoreCase;
            string? status = StatusFilter.SelectedIndex > 0
                ? (StatusFilter.SelectedItem as ComboBoxItem)?.Content as string
                : null;

            var rows = allOrders.Where(o =>
                    (status == null || o.StatusName == status) &&
                    (text == "" ||
                     o.OrderId.ToString() == text.TrimStart('№', ' ') ||
                     o.ClientName.Contains(text, ic) ||
                     o.ItemsText.Contains(text, ic)))
                .ToList();

            OrdersList.ItemsSource = rows;

            EmptyPanel.IsVisible = rows.Count == 0;
            EmptyTitle.Text = allOrders.Count == 0 ? "Заказов пока нет" : "Ничего не найдено";
            EmptyHint.IsVisible = allOrders.Count == 0;
        }

        private void SearchChanged(object? sender, TextChangedEventArgs e) => ApplyFilters();

        private void FilterChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (OrdersList != null) ApplyFilters();
        }

        private async void ReloadClick(object? sender, RoutedEventArgs e) => await LoadOrdersAsync();

        private void NewClick(object? sender, RoutedEventArgs e) =>
            MainWindow.Instance.OpenPage(new CatalogView(), clearHistory: true);

        // Щелчок по заказу — открыть подробности
        private void OrdersTapped(object? sender, TappedEventArgs e)
        {
            if (OrdersList.SelectedItem is Order order)
                MainWindow.Instance.OpenPage(new OrderDetailsView { OrderId = order.OrderId });
        }
    }
}
