using System;
using System.Collections.Generic;

namespace BoardGame.Models;

public partial class Order
{
    public int OrderId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateOnly ReserveUntil { get; set; }

    public int UserId { get; set; }

    public int? EmployeeId { get; set; }

    public int StatusId { get; set; }

    public virtual User? Employee { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Status Status { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
