using System;
using System.Collections.Generic;

namespace BACKEND.Models;

public partial class Favorite
{
    public int UserId { get; set; }

    public int DocumentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Document Document { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
