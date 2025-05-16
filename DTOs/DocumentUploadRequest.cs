using System.ComponentModel.DataAnnotations;

public class DocumentUploadRequest
{
    [Required] public string Title { get; set; } = null!;
    [Required] public string Description { get; set; } = null!;
    [Required] public int AuthorId { get; set; }
    [Required] public int PublisherId { get; set; }
    [Required] public int CategoryId { get; set; }
    [Required] public bool IsApproved { get; set; }
    [Required] public bool IsPremium { get; set; }
    [Required] public IFormFile File { get; set; } = null!;
}
