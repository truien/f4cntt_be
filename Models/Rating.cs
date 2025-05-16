using System;
using System.Collections.Generic;

namespace BACKEND.Models;

public partial class Rating
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int DocumentId { get; set; }

    public sbyte Score { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Document Document { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
