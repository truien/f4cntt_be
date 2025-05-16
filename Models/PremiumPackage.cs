using System;
using System.Collections.Generic;

namespace BACKEND.Models;

public partial class PremiumPackage
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int MaxDownloads { get; set; }

    public int DurationDays { get; set; }

    public decimal Price { get; set; }

    public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();

    public virtual ICollection<UserPremium> UserPremia { get; set; } = new List<UserPremium>();
}
