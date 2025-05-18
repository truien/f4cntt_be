using System;
using System.Collections.Generic;

namespace BACKEND.Models;

public partial class PaymentTransaction
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string? TransactionCode { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? ResponseData { get; set; }

    public virtual User User { get; set; } = null!;
}
