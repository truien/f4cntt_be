
using System.ComponentModel.DataAnnotations;

public class ChatWithPdfRequest
{
    public string DocId { get; set; } = null!;
    public string Message { get; set; } = null!;
    public bool save_chat { get; set; } = true;
}


public class ChatResponse
{
    public string Content { get; set; } = null!;
    public IEnumerable<ChatReference>? References { get; set; }
}

public class ChatReference
{
    public int PageNumber { get; set; }
    public int FromLine { get; set; }
    public int ToLine { get; set; }
}

public class HistoryRequest
{
    [Required] public string DocId { get; set; } = null!;
    public int? ResultsPerPage { get; set; } = 10;
    public int? PageNumber { get; set; } = 1;
    [RegularExpression("asc|desc")]
    public string? SortOrder { get; set; } = "desc";
}

public class HistoryEntry
{
    public string Role { get; set; } = null!;
    public string Message { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class HistoryResponse
{
    public IEnumerable<HistoryEntry> Entries { get; set; } = Array.Empty<HistoryEntry>();
}
