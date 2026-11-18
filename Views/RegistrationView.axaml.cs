using Avalonia.Controls;
using Avalonia.Interactivity;
using BoardGame.Services; // Для Session

namespace BoardGame.Views;

public partial class RegistrationView : UserControl
{
    public RegistrationView()
    {
        InitializeComponent();
    }

    private async void SubmitClick(object? sender, RoutedEventArgs e)
    {
        SubmitButton.IsEnabled = false;
        try
        {
            // Проверку полей и уникальность логина выполняет Session.RegisterClientAsync
            await Session.RegisterClientAsync(
                RegLogin.Text ?? "",
                RegPassword.Text ?? "",
                RegFirstName.Text ?? "",
                RegLastName.Text ?? "",
                RegPhone.Text ?? "",
                (RegEmail.Text ?? "").Trim());

            MainWindow.Instance.OpenPage(new LoginView(), clearHistory: true);
            MainWindow.Instance.ShowMessage("Регистрация прошла успешно! Теперь войдите в систему.");
        }
        catch (Exception ex)
        {
            MainWindow.Instance.ShowError(ex);
        }
        finally
        {
            SubmitButton.IsEnabled = true;
        }
    }

    private void CancelClick(object? sender, RoutedEventArgs e)
    {
        MainWindow.Instance.GoBack();
    }
}
