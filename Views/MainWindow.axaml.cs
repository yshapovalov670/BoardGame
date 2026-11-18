using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BoardGame.Services; // Для Session, Cart и UiSupport

namespace BoardGame.Views;

public partial class MainWindow : Window
{
    public static MainWindow Instance { get; private set; } = null!;

    // История открытых страниц для возврата назад
    private readonly Stack<UserControl> history = new();

    // Таймер, который прячет всплывающее уведомление
    private readonly DispatcherTimer toastTimer = new() { Interval = TimeSpan.FromSeconds(4) };

    public MainWindow()
    {
        InitializeComponent();
        Instance = this;

        toastTimer.Tick += (s, e) => HideToast();

        // Счётчик корзины в меню обновляется при каждом изменении корзины
        Cart.Changed += UpdateCartBadge;

        // СТАРТОВАЯ СТРАНИЦА — КАТАЛОГ (пользователь сразу Гость)
        OpenPage(new CatalogView(), clearHistory: true);
    }

    /// <summary>
    /// Открыть страницу. clearHistory: true — начать историю заново (вход, выход, пункт меню).
    /// </summary>
    public void OpenPage(UserControl page, bool clearHistory = false)
    {
        if (clearHistory)
            history.Clear();
        else if (PageHost.Content is UserControl current)
            history.Push(current); // Текущую страницу кладём в историю

        ShowPage(page);
    }

    /// <summary>
    /// Вернуться на предыдущую страницу (если она есть).
    /// </summary>
    public void GoBack()
    {
        if (history.Count > 0)
            ShowPage(history.Pop());
        else
            OpenPage(new CatalogView(), clearHistory: true);
    }

    /// <summary>
    /// Показать страницу и обновить меню под роль пользователя.
    /// </summary>
    private void ShowPage(UserControl page)
    {
        PageHost.Content = page;

        // Заголовок окна берётся из свойства Tag страницы
        Title = "Магазин настольных игр — " + (page.Tag ?? "");

        UpdateHeader(page);
    }

    private void UpdateHeader(UserControl page)
    {
        // Плашка пользователя справа в верхней панели
        if (Session.CurrentUser is { } user)
        {
            UserText.Text = user.FullName;
            RoleText.Text = Session.RoleName;
            AvatarText.Text = user.Name.Length > 0 ? user.Name[..1].ToUpper() : "?";
            AuthButton.Content = "Выйти";
            AuthButton.Classes.Remove("primary");
            AuthButton.Classes.Add("ghost");
        }
        else
        {
            UserText.Text = "Гость";
            RoleText.Text = "без входа";
            AvatarText.Text = "Г";
            AuthButton.Content = "Войти";
            AuthButton.Classes.Remove("ghost");
            AuthButton.Classes.Add("primary");
        }

        // Корзина — только тем, кто может покупать (покупатель, администратор)
        CartButton.IsVisible = Session.CanBuy;
        UpdateCartBadge();

        // Заказы видны всем авторизованным, подпись зависит от роли
        OrdersButton.IsVisible = !Session.IsGuest;
        OrdersButton.Content = Session.IsStaff ? "Все заказы" : "Мои заказы";

        // Раздел «Управление» (выпадающий список) — только администратору
        AdminButton.IsVisible = Session.IsAdmin;

        // Подсветка текущего раздела
        SetActive(CatalogButton, page is CatalogView or ProductView);
        SetActive(CartButton, page is CartView);
        SetActive(OrdersButton, page is OrdersView or OrderDetailsView);
        SetActive(AdminButton, page is ProductsManagementView or StaffManagementView or StaffCreationView);
    }

    private void UpdateCartBadge()
    {
        CartBadge.IsVisible = Cart.Count > 0;
        CartCountText.Text = Cart.Count.ToString();
    }

    private static void SetActive(Button button, bool active)
    {
        if (active)
        {
            if (!button.Classes.Contains("active")) button.Classes.Add("active");
        }
        else
        {
            button.Classes.Remove("active");
        }
    }

    // ---------- УВЕДОМЛЕНИЯ ----------

    /// <summary>
    /// Информационное сообщение (зелёное уведомление).
    /// </summary>
    public void ShowMessage(string text) =>
        ShowToast(text, "✓", "SuccessSoftBrush", "SuccessBrush");

    /// <summary>
    /// Предупреждение (жёлтое уведомление) — например, «войдите в систему».
    /// </summary>
    public void ShowWarning(string text) =>
        ShowToast(text, "!", "WarningSoftBrush", "WarningBrush");

    /// <summary>
    /// Сообщение об ошибке (красное уведомление).
    /// </summary>
    public void ShowError(Exception error)
    {
        System.Diagnostics.Trace.WriteLine(error); // Подробности — в окно Output
        ShowToast(UiSupport.ErrorText(error), "✕", "DangerSoftBrush", "DangerBrush");
    }

    private void ShowToast(string text, string icon, string backgroundKey, string foregroundKey)
    {
        Toast.Background = UiSupport.Brush(backgroundKey);
        ToastText.Foreground = UiSupport.Brush(foregroundKey);
        ToastIcon.Foreground = UiSupport.Brush(foregroundKey);
        ToastIcon.Text = icon;
        ToastText.Text = text;
        Toast.IsVisible = true;

        toastTimer.Stop();
        toastTimer.Start();
    }

    private void HideToast()
    {
        toastTimer.Stop();
        Toast.IsVisible = false;
    }

    private void ToastPressed(object? sender, PointerPressedEventArgs e) => HideToast();

    // ---------- ОБРАБОТЧИКИ МЕНЮ ----------

    private void CatalogClick(object? sender, RoutedEventArgs e) =>
        OpenPage(new CatalogView(), clearHistory: true);

    private void CartClick(object? sender, RoutedEventArgs e) =>
        OpenPage(new CartView(), clearHistory: true);

    private void OrdersClick(object? sender, RoutedEventArgs e) =>
        OpenPage(new OrdersView(), clearHistory: true);

    private void ProductsClick(object? sender, RoutedEventArgs e) =>
        OpenPage(new ProductsManagementView(), clearHistory: true);

    private void StaffClick(object? sender, RoutedEventArgs e) =>
        OpenPage(new StaffCreationView(), clearHistory: true);

    private void EmployeesClick(object? sender, RoutedEventArgs e) =>
        OpenPage(new StaffManagementView(), clearHistory: true);

    /// <summary>
    /// Кнопка в карточке пользователя: «Войти» для гостя, «Выйти» для остальных.
    /// </summary>
    private void LogoutOrLoginClick(object? sender, RoutedEventArgs e)
    {
        if (Session.IsGuest)
        {
            OpenPage(new LoginView());
        }
        else
        {
            Session.Logout();
            OpenPage(new CatalogView(), clearHistory: true);
        }
    }
}
