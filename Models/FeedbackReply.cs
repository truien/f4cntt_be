using System;
using System.Collections.Generic;

namespace BACKEND.Models;

public partial class FeedbackReply
{
    public int Id { get; set; }

    public int FeedbackId { get; set; }

    public int? UserId { get; set; }

    public string Message { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Feedback Feedback { get; set; } = null!;

    public virtual User? User { get; set; }
}
