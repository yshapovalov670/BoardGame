using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using BoardGame.Data;
using BoardGame.Models;
using BoardGame.Services;

namespace BoardGame.Views;

public partial class StaffManagementView : UserControl
{
    public StaffManagementView()
    {
        InitializeComponent();
        Loaded += async (s, e) => await LoadStaffAsync();
    }

    private async Task LoadStaffAsync(int? selectId = null)
    {
        try
        {
            using var db = Db.Create();

            // Сотрудники — пользователи с ролями «Продавец» и «Администратор»
            var staff = await db.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .Where(u => u.Role.RoleName == Session.RoleSeller || u.Role.RoleName == Session.RoleAdmin)
                .OrderBy(u => u.LastName)
                .ToListAsync();

            StaffList.ItemsSource = staff;
            if (selectId != null)
                StaffList.SelectedItem = staff.FirstOrDefault(u => u.UserId == selectId);
        }
        catch (Exception ex)
        {
            MainWindow.Instance.ShowError(ex);
        }
    }

    private void StaffSelected(object? sender, SelectionChangedEventArgs e)
    {
        bool selected = StaffList.SelectedItem is User;
        EditPanel.IsVisible = selected;
        EmptyHint.IsVisible = !selected;

        if (StaffList.SelectedItem is User u)
        {
            EditTitle.Text = $"Редактирование: {u.FullName}";
            EditName.Text = u.Name;
            EditLastName.Text = u.LastName;
            EditPhone.Text = u.Phone;
            EditEmail.Text = u.Email;
            EditLogin.Text = u.Login;
            EditRole.SelectedIndex = u.RoleName == Session.RoleAdmin ? 1 : 0;
        }
    }

    private async void SaveStaffClick(object? sender, RoutedEventArgs e)
    {
        if (StaffList.SelectedItem is not User u) return;

        try
        {
            Session.RequireAdmin();

            // Та же проверка, что при создании (логин и пароль здесь не меняются)
            string phone = Session.ValidateUserData(u.Login, u.Password,
                EditName.Text ?? "", EditLastName.Text ?? "", EditPhone.Text ?? "", (EditEmail.Text ?? "").Trim());

            string roleName = (EditRole.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? Session.RoleSeller;

            // Администратор не может лишить прав сам себя — иначе потеряет доступ к управлению
            if (u.UserId == Session.CurrentUser!.UserId && roleName != Session.RoleAdmin)
                throw new AppException("Нельзя снять роль администратора со своей учётной записи.");

            using var db = Db.Create();
            var dbUser = await db.Users.FirstOrDefaultAsync(x => x.UserId == u.UserId)
                ?? throw new AppException("Сотрудник не найден — возможно, его удалили.");
            var role = await db.Roles.FirstAsync(r => r.RoleName == roleName);

            dbUser.Name = EditName.Text!.Trim();
            dbUser.LastName = EditLastName.Text!.Trim();
            dbUser.Phone = phone;
            dbUser.Email = EditEmail.Text!.Trim();
            dbUser.RoleId = role.RoleId;

            await db.SaveChangesAsync();

            MainWindow.Instance.ShowMessage("Данные сотрудника обновлены.");
            await LoadStaffAsync(u.UserId);
        }
        catch (Exception ex)
        {
            MainWindow.Instance.ShowError(ex);
        }
    }

    private void CancelEditClick(object? sender, RoutedEventArgs e)
    {
        StaffList.SelectedItem = null;
    }
}
