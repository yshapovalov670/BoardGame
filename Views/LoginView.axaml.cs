using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BoardGame.Services; // Для Session и AppException

namespace BoardGame.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();

        // Enter в любом поле — то же, что кнопка «Войти»
        LoginBox.KeyDown += OnFieldKeyDown;
        PasswordBox.KeyDown += OnFieldKeyDown;

        Loaded += (s, e) => LoginBox.Focus();
    }

    private void OnFieldKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            LoginClick(sender, new RoutedEventArgs());
    }

    // ---------- ЛОГИКА ВХОДА ----------
    private async void LoginClick(object? sender, RoutedEventArgs e)
    {
        StatusText.IsVisible = false;

        string login = (LoginBox.Text ?? "").Trim();
        string pass = PasswordBox.Text ?? "";

        if (login == "" || pass == "")
        {
            ShowStatus("Введите логин и пароль.");
            return;
        }

        LoginButton.IsEnabled = false;
        try
        {
            await Session.LoginAsync(login, pass);

            // Успешный вход: переходим в каталог, меню перестроится под роль
            MainWindow.Instance.OpenPage(new CatalogView(), clearHistory: true);
            MainWindow.Instance.ShowMessage($"Добро пожаловать, {Session.CurrentUser!.Name}!");
        }
        catch (AppException ex)
        {
            ShowStatus(ex.Message);
        }
        catch (Exception ex)
        {
            ShowStatus(UiSupport.ErrorText(ex));
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void ShowStatus(string text)
    {
        StatusText.Text = text;
        StatusText.IsVisible = true;
    }

    // ---------- ПЕРЕХОД НА РЕГИСТРАЦИЮ КЛИЕНТА ----------
    private void GoToRegisterClick(object? sender, RoutedEventArgs e)
    {
        MainWindow.Instance.OpenPage(new RegistrationView());
    }
}
