using System;
using System.Collections.Generic;

namespace BACKEND.Models;

public partial class UserPremium
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int PackageId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public int? DownloadsUsed { get; set; }

    public virtual PremiumPackage Package { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
