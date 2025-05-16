using System;
using System.Collections.Generic;

namespace BACKEND.Models;

public partial class Feedback
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public string Email { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string? ResponseMessage { get; set; }

    public DateTime? ResponseAt { get; set; }

    public string Status { get; set; } = null!;

    public string? AttachmentUrl { get; set; }

    public string? Category { get; set; }

    public DateTime SentAt { get; set; }

    public virtual ICollection<FeedbackReply> FeedbackReplies { get; set; } = new List<FeedbackReply>();

    public virtual User? User { get; set; }
}
