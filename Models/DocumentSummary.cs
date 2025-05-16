using System;
using System.Collections.Generic;

namespace BACKEND.Models;

public partial class DocumentSummary
{
    public int Id { get; set; }

    public int DocumentId { get; set; }

    public string SummaryText { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Document Document { get; set; } = null!;
}
