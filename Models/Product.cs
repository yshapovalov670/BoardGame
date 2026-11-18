using System;
using System.Collections.Generic;

namespace BoardGame.Models;

public partial class Product
{
    public int ProductId { get; set; }

    public string Article { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int CategoryId { get; set; }

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public string? Players { get; set; }

    public int? MinAge { get; set; }

    public int? PlayTime { get; set; }

    public string? ImagePath { get; set; }

    public string? Description { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
