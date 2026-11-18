using BoardGame.Services;

namespace BoardGame.Models;

// Вычисляемые свойства моделей. Классы помечены partial, поэтому
// дополняют сгенерированные scaffold-ом файлы, не изменяя их.

public partial class User
{
    // ФИО одной строкой.
    public string FullName => $"{LastName} {Name}".Trim();

    // Инициалы для круглого аватара: «ШЯ»
    public string Initials => $"{(LastName.Length > 0 ? LastName[0] : ' ')}{(Name.Length > 0 ? Name[0] : ' ')}".Trim().ToUpper();

    public string RoleName => Role?.RoleName ?? "";

    // ToString решает, как объект выглядит в ComboBox и ListBox.
    public override string ToString() => $"{Login} — {FullName}";
}

public partial class Category
{
    public override string ToString() => Name;
}

public partial class Product
{
    public string CategoryName => Category?.Name ?? "";

    public override string ToString() => $"{Name} ({Article})";
}

public partial class Status
{
    public override string ToString() => Name;
}

public partial class OrderItem
{
    // Сумма строки: цена на момент оформления × количество.
    public decimal LineTotal => PriceRules.LineTotal(Price, Quantity);

    public string ProductName => Product?.Name ?? "Товар удалён";
}

public partial class Order
{
    // Итоговая стоимость заказа: сумма всех позиций.
    public decimal TotalCost => PriceRules.Total(OrderItems.Select(i => (i.Price, i.Quantity)));

    // Количество единиц товара в заказе.
    public int ItemsCount => OrderItems.Sum(i => i.Quantity);

    // Краткий состав для списка: «Каркассон ×1, Диксит ×2»
    public string ItemsText => string.Join(", ", OrderItems.Select(i => $"{i.ProductName} ×{i.Quantity}"));

    public string StatusName => Status?.Name ?? "";

    // Флаги для цветных плашек статуса (Classes.cancelled="{Binding IsCancelled}")
    public bool IsCancelled => StatusName == OrderService.StatusCancelled;
    public bool IsIssued => StatusName == OrderService.StatusIssued;
    public bool IsReserved => StatusName == OrderService.StatusReserved;

    public string ClientName => User?.FullName ?? "";

    public string EmployeeName => Employee?.FullName ?? "—";

    public string ReserveText => IsReserved ? $"Бронь до {ReserveUntil:dd.MM.yyyy}" : "";

    public override string ToString() =>
        $"Заказ № {OrderId} · {CreatedAt:dd.MM.yyyy} · {TotalCost:N2} руб.";
}
