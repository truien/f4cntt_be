using System;
using System.Collections.Generic;

namespace BACKEND.Models;

public partial class Comment
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int DocumentId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Document Document { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
