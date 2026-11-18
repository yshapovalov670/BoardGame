using Avalonia.Controls;
using Avalonia.Interactivity;
using BoardGame.Models;
using BoardGame.Services;

namespace BoardGame.Views
{
    public partial class OrderDetailsView : UserControl
    {
        public int OrderId { get; set; }

        public OrderDetailsView()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadOrderAsync();
        }

        private async Task LoadOrderAsync()
        {
            try
            {
                ConfirmPanel.IsVisible = false;

                // Сервис сам проверяет, имеет ли пользователь доступ к заказу
                Order? order = await OrderService.GetOrderAsync(OrderId);

                if (order == null)
                {
                    MainWindow.Instance.ShowWarning("Заказ не найден.");
                    MainWindow.Instance.GoBack();
                    return;
                }

                HeaderText.Text = $"Заказ № {order.OrderId}";
                CreatedText.Text = $"Оформлен {order.CreatedAt:dd.MM.yyyy в HH:mm}";

                ClientText.Text = order.User?.FullName ?? "Неизвестный покупатель";
                ClientContactsText.Text = order.User == null ? "" : $"{order.User.Email} · тел. {order.User.Phone}";
                ReserveText.Text = order.IsReserved ? $"до {order.ReserveUntil:dd.MM.yyyy}" : "—";
                EmployeeText.Text = order.EmployeeName;

                ItemsList.ItemsSource = order.OrderItems.OrderBy(i => i.ProductName).ToList();
                CountText.Text = $"Позиций: {order.OrderItems.Count}, товаров: {order.ItemsCount} шт.";
                TotalText.Text = $"{order.TotalCost:N2} руб.";

                // Цвет плашки статуса
                StatusText.Text = order.StatusName;
                string tone = order.IsCancelled ? "Danger" : order.IsIssued ? "Neutral" : "Success";
                StatusBadge.Background = UiSupport.Brush(tone + "SoftBrush");
                StatusText.Foreground = UiSupport.Brush(tone + "Brush");

                // Кнопки видны только тем, кому действие разрешено
                CancelButton.IsVisible = OrderService.CanCancel(order);
                IssueButton.IsVisible = OrderService.CanIssue(order);
            }
            catch (Exception ex)
            {
                MainWindow.Instance.ShowError(ex);
            }
        }

        private void CancelOrderClick(object? sender, RoutedEventArgs e) => ConfirmPanel.IsVisible = true;

        private void CancelDeleteClick(object? sender, RoutedEventArgs e) => ConfirmPanel.IsVisible = false;

        private async void ConfirmDeleteClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                await OrderService.CancelOrderAsync(OrderId);
                MainWindow.Instance.ShowMessage($"Заказ № {OrderId} отменён, товар возвращён на склад.");
                await LoadOrderAsync();
            }
            catch (Exception ex)
            {
                ConfirmPanel.IsVisible = false;
                MainWindow.Instance.ShowError(ex);
            }
        }

        private async void IssueClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                await OrderService.IssueOrderAsync(OrderId);
                MainWindow.Instance.ShowMessage($"Заказ № {OrderId} выдан покупателю.");
                await LoadOrderAsync();
            }
            catch (Exception ex)
            {
                MainWindow.Instance.ShowError(ex);
            }
        }

        // Возврат к списку: открываем заново, чтобы увидеть новые статусы
        private void GoBackClick(object? sender, RoutedEventArgs e) =>
            MainWindow.Instance.OpenPage(new OrdersView(), clearHistory: true);
    }
}
