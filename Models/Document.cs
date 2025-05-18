using System;
using System.Collections.Generic;

namespace BACKEND.Models;

public partial class Document
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? PdfUrl { get; set; }

    public int CategoryId { get; set; }

    public int AuthorId { get; set; }

    public int PublisherId { get; set; }

    public int Status { get; set; }

    public int CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsPremium { get; set; }

    public int? TotalPages { get; set; }

    public int? PreviewPageLimit { get; set; }

    public string ConversionStatus { get; set; } = null!;

    public string? ConversionJobId { get; set; }

    public string? Summarystatus { get; set; }

    public string TtsStatus { get; set; } = null!;

    public string SummaryTtsStatus { get; set; } = null!;

    /// <summary>
    /// Đường dẫn tới file audio TTS cho summary
    /// </summary>
    public string? SummaryTtsUrl { get; set; }

    /// <summary>
    /// Đường dẫn tới file audio TTS (MP3/OVA/etc)
    /// </summary>
    public string? TtsUrl { get; set; }

    /// <summary>
    /// Đường dẫn tới hình ảnh minh họa cho tài liệu
    /// </summary>
    public string? ImgUrl { get; set; }

    /// <summary>
    /// Số điểm cần có để tải tài liệu này
    /// </summary>
    public int PointCost { get; set; }

    public virtual Author Author { get; set; } = null!;

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<DocumentSummary> DocumentSummaries { get; set; } = new List<DocumentSummary>();

    public virtual ICollection<Download> Downloads { get; set; } = new List<Download>();

    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    public virtual Publisher Publisher { get; set; } = null!;

    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
}
