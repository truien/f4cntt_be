using System;
using System.Collections.Generic;

namespace BACKEND.Models;

public partial class Transaction
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public decimal Amount { get; set; }

    public string? Type { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
