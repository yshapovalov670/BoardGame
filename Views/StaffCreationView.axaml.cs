using Avalonia.Controls;
using Avalonia.Interactivity;
using BoardGame.Services; // Для Session и AppException

namespace BoardGame.Views
{
    public partial class StaffCreationView : UserControl
    {
        public StaffCreationView()
        {
            InitializeComponent();
        }

        private string SelectedRole() =>
            (RoleSelect.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

        private async void SaveClick(object? sender, RoutedEventArgs e)
        {
            SaveButton.IsEnabled = false;
            try
            {
                string roleName = SelectedRole();
                if (roleName == "")
                    throw new AppException("Выберите роль сотрудника.");

                // Проверка полей и уникальность логина — в сервисе
                await Session.CreateStaffAsync(
                    StaffLogin.Text ?? "", StaffPass.Text ?? "",
                    StaffFirst.Text ?? "", StaffLast.Text ?? "",
                    StaffPhone.Text ?? "", (StaffEmail.Text ?? "").Trim(),
                    roleName);

                string name = $"{StaffFirst.Text?.Trim()} {StaffLast.Text?.Trim()}";
                MainWindow.Instance.OpenPage(
                    new StaffManagementView(),
                    clearHistory: true);
                MainWindow.Instance.ShowMessage($"Сотрудник {name} ({roleName}) добавлен.");
            }
            catch (Exception ex)
            {
                MainWindow.Instance.ShowError(ex);
            }
            finally
            {
                SaveButton.IsEnabled = true;
            }
        }

        private void CancelClick(object? sender, RoutedEventArgs e)
        {
            MainWindow.Instance.OpenPage(new StaffCreationView(), clearHistory: true);
        }
    }
}
