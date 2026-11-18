using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using BoardGame.Data;      // Для доступа к Db.Create()
using BoardGame.Models;    // Для типов User, Role

namespace BoardGame.Services;

// Session («сеанс») помнит, кто сейчас работает с программой.
public static class Session
{
    // Названия ролей — ТОЧНО как в таблице Roles.
    public const string RoleAdmin = "Администратор";
    public const string RoleSeller = "Продавец";
    public const string RoleClient = "Покупатель";

    // Текущий пользователь. null означает «гость» (никто не вошёл).
    public static User? CurrentUser { get; private set; }

    // Название роли текущего пользователя; для гостя — пустая строка.
    public static string RoleName => CurrentUser?.Role.RoleName ?? "";

    public static bool IsGuest => CurrentUser == null;

    // Администратор имеет полный доступ
    public static bool IsAdmin => RoleName == RoleAdmin;

    // Продавец обслуживает заказы покупателей
    public static bool IsSeller => RoleName == RoleSeller;

    public static bool IsClient => RoleName == RoleClient;

    // Сотрудники магазина: администраторы и продавцы.
    public static bool IsStaff => IsAdmin || IsSeller;

    // Оформлять заказы могут покупатели и администратор (заказ «по телефону»).
    public static bool CanBuy => IsClient || IsAdmin;

    // Подпись пользователя.
    public static string Caption =>
        CurrentUser == null ? "Гость" : $"{CurrentUser.FullName} · {RoleName}";

    // Вход по логину и паролю
    public static async Task LoginAsync(string login, string password)
    {
        using var db = Db.Create();

        string normalized = login.Trim().ToLower();

        // Ищем пользователя по логину (без учёта регистра) вместе с ролью
        var user = await db.Users
            .Include(u => u.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Login.ToLower() == normalized);

        // Одинаковый текст для «нет логина» и «не тот пароль» — не подсказываем злоумышленнику
        if (user == null || user.Password != password)
            throw new AppException("Неверный логин или пароль.");

        CurrentUser = user;
        Cart.Clear();
    }

    // Выход: снова гость, корзина очищается.
    public static void Logout()
    {
        CurrentUser = null;
        Cart.Clear();
    }

    // Проверки прав. Вызываются в сервисах ПЕРЕД изменением данных.
    public static void RequireStaff()
    {
        if (!IsStaff)
            throw new AppException("Недостаточно прав: нужна роль продавца или администратора.");
    }

    public static void RequireAdmin()
    {
        if (!IsAdmin)
            throw new AppException("Недостаточно прав: нужна роль администратора.");
    }

    // ---------------------------------------------------------
    // ПРОВЕРКА ДАННЫХ ПОЛЬЗОВАТЕЛЯ (общая для регистрации и персонала)
    // ---------------------------------------------------------
    public static string ValidateUserData(string login, string password, string firstName,
        string lastName, string phoneText, string email)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            throw new AppException("Логин и пароль обязательны.");

        if (login.Length > 50)
            throw new AppException("Логин не может быть длиннее 50 символов.");

        if (password.Length < 4)
            throw new AppException("Пароль должен содержать не менее 4 символов.");

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            throw new AppException("Укажите имя и фамилию.");

        // Телефон: допускаем «+7 (999) 123-45-67», в БД храним только цифры
        string phone = new string(phoneText.Where(char.IsDigit).ToArray());
        if (phone.Length < 6 || phone.Length > 15)
            throw new AppException("Телефон должен содержать от 6 до 15 цифр.");

        if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw new AppException("Введите корректный email (например, name@mail.ru).");

        return phone;
    }

    // ---------------------------------------------------------
    // РЕГИСТРАЦИЯ ПОКУПАТЕЛЯ (роль «Покупатель»)
    // ---------------------------------------------------------
    public static async Task<int> RegisterClientAsync(string login, string password,
        string firstName, string lastName, string phoneText, string email)
    {
        string phone = ValidateUserData(login, password, firstName, lastName, phoneText, email);
        return await CreateUserAsync(login, password, firstName, lastName, phone, email, RoleClient);
    }

    // ---------------------------------------------------------
    // СОЗДАНИЕ СОТРУДНИКА (раздел «Новый сотрудник», только администратор)
    // ---------------------------------------------------------
    public static async Task<int> CreateStaffAsync(string login, string password,
        string firstName, string lastName, string phoneText, string email, string roleName)
    {
        RequireAdmin();

        if (roleName != RoleSeller && roleName != RoleAdmin)
            throw new AppException("Выберите роль сотрудника.");

        string phone = ValidateUserData(login, password, firstName, lastName, phoneText, email);
        return await CreateUserAsync(login, password, firstName, lastName, phone, email, roleName);
    }

    private static async Task<int> CreateUserAsync(string login, string password,
        string firstName, string lastName, string phone, string email, string roleName)
    {
        using var db = Db.Create();
        login = login.Trim();

        // 1. Логин должен быть уникальным (без учёта регистра)
        string normalized = login.ToLower();
        if (await db.Users.AnyAsync(u => u.Login.ToLower() == normalized))
            throw new AppException("Такой логин уже занят.");

        // 2. Находим роль по названию
        var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == roleName);
        if (role == null)
            throw new AppException($"Роль «{roleName}» не найдена в базе данных.");

        var newUser = new User
        {
            Login = login,
            Password = password, // учебная БД: пароль хранится как есть, без хеширования
            Name = firstName.Trim(),
            LastName = lastName.Trim(),
            Phone = phone.Trim(),
            Email = email.Trim(),
            RoleId = role.RoleId
        };

        db.Users.Add(newUser);
        await db.SaveChangesAsync(); // получаем UserId
        return newUser.UserId;
    }
}
