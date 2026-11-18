using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using BoardGame.Data;
using BoardGame.Models;
using BoardGame.Services;

namespace BoardGame.Views;

public partial class ProductsManagementView : UserControl
{
    // null — режим добавления, иначе — редактируем игру с этим id
    private int? currentProductId;

    private List<Product> allProducts = new();

    public ProductsManagementView()
    {
        InitializeComponent();
        PreviewImage.Source = UiSupport.Picture(null);
        Loaded += async (s, e) => await LoadAsync();
    }

    private async Task LoadAsync(int? selectId = null)
    {
        try
        {
            using var db = Db.Create();

            EditCategory.ItemsSource = await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

            allProducts = await db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
            ApplySearch();

            if (selectId != null)
                ProductsList.SelectedItem = allProducts.FirstOrDefault(p => p.ProductId == selectId);
            else
                StartAdding();
        }
        catch (Exception ex)
        {
            MainWindow.Instance.ShowError(ex);
        }
    }

    private void ApplySearch()
    {
        string text = (SearchBox.Text ?? "").Trim();
        var ic = StringComparison.OrdinalIgnoreCase;
        ProductsList.ItemsSource = allProducts
            .Where(p => text == "" || p.Name.Contains(text, ic) || p.Article.Contains(text, ic))
            .ToList();
    }

    private void SearchChanged(object? sender, TextChangedEventArgs e) => ApplySearch();

    private void ProductSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ProductsList.SelectedItem is Product p)
        {
            currentProductId = p.ProductId;
            FormTitle.Text = $"Редактирование: {p.Name}";
            EditArticle.Text = p.Article;
            EditName.Text = p.Name;
            EditCategory.SelectedItem = (EditCategory.ItemsSource as IEnumerable<Category>)?
                .FirstOrDefault(c => c.CategoryId == p.CategoryId);
            EditPrice.Value = p.Price;
            EditStock.Value = p.StockQuantity;
            EditPlayers.Text = p.Players;
            EditAge.Value = p.MinAge;
            EditTime.Value = p.PlayTime;
            EditDescription.Text = p.Description;
            EditImage.Text = p.ImagePath;
            DeleteButton.IsVisible = true;
        }
    }

    private void AddProductClick(object? sender, RoutedEventArgs e)
    {
        ProductsList.SelectedItem = null;
        StartAdding();
        EditArticle.Focus();
    }

    private void StartAdding()
    {
        currentProductId = null;
        FormTitle.Text = "Добавление новой игры";
        EditArticle.Text = "";
        EditName.Text = "";
        EditCategory.SelectedItem = null;
        EditPrice.Value = 1000;
        EditStock.Value = 0;
        EditPlayers.Text = "";
        EditAge.Value = null;
        EditTime.Value = null;
        EditDescription.Text = "";
        EditImage.Text = "";
        DeleteButton.IsVisible = false;
    }

    private void ImageChanged(object? sender, TextChangedEventArgs e)
    {
        PreviewImage.Source = UiSupport.Picture(EditImage.Text?.Trim());
    }

    private async void SaveProductClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            Session.RequireAdmin();

            string article = EditArticle.Text?.Trim() ?? "";
            string name = EditName.Text?.Trim() ?? "";
            string players = EditPlayers.Text?.Trim() ?? "";
            string desc = EditDescription.Text?.Trim() ?? "";
            string image = EditImage.Text?.Trim() ?? "";

            if (article == "" || name == "")
                throw new AppException("Укажите артикул и название игры.");

            if (EditCategory.SelectedItem is not Category category)
                throw new AppException("Выберите категорию.");

            decimal price = EditPrice.Value ?? 0;
            if (price <= 0)
                throw new AppException("Цена должна быть больше нуля.");

            int stock = (int)(EditStock.Value ?? 0);
            if (stock < 0)
                throw new AppException("Остаток не может быть отрицательным.");

            using var db = Db.Create();

            // Артикул должен быть уникальным
            if (await db.Products.AnyAsync(p => p.Article == article && p.ProductId != (currentProductId ?? 0)))
                throw new AppException("Игра с таким артикулом уже существует.");

            Product? product;
            if (currentProductId.HasValue)
            {
                product = await db.Products.FindAsync(currentProductId.Value)
                    ?? throw new AppException("Игра не найдена — возможно, её удалили.");
            }
            else
            {
                product = new Product();
                db.Products.Add(product);
            }

            product.Article = article;
            product.Name = name;
            product.CategoryId = category.CategoryId;
            product.Price = decimal.Round(price, 2);
            product.StockQuantity = stock;
            product.Players = players == "" ? null : players;
            product.MinAge = EditAge.Value is decimal age ? (int)age : null;
            product.PlayTime = EditTime.Value is decimal time ? (int)time : null;
            product.Description = desc == "" ? null : desc;
            product.ImagePath = image == "" ? null : image;

            await db.SaveChangesAsync();

            MainWindow.Instance.ShowMessage($"Игра «{name}» сохранена.");
            // После сохранения новая игра становится выбранной — повторное
            // нажатие «Сохранить» уже редактирует её, а не создаёт дубликат
            await LoadAsync(product.ProductId);
        }
        catch (Exception ex)
        {
            MainWindow.Instance.ShowError(ex);
        }
    }

    private async void DeleteProductClick(object? sender, RoutedEventArgs e)
    {
        if (!currentProductId.HasValue) return;

        try
        {
            Session.RequireAdmin();

            using var db = Db.Create();
            var product = await db.Products.FindAsync(currentProductId.Value);
            if (product == null) return;

            // Игры с историей продаж не удаляем — иначе потеряется история заказов
            if (await db.OrderItems.AnyAsync(i => i.ProductId == product.ProductId))
                throw new AppException("Нельзя удалить игру, которая есть в заказах. Установите остаток 0.");

            db.Products.Remove(product);
            await db.SaveChangesAsync();
            Cart.Remove(product.ProductId);

            MainWindow.Instance.ShowMessage($"Игра «{product.Name}» удалена.");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MainWindow.Instance.ShowError(ex);
        }
    }
}
