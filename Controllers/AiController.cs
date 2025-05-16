using BACKEND.Models;
using Microsoft.AspNetCore.Mvc;
using BACKEND.Utilities;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly IHuggingFaceService _hf;
    private readonly DBContext _context; // nếu cần lưu summary

    public AiController(IHuggingFaceService hf, DBContext context)
    {
        _hf = hf;
        _context = context;
    }
    [Authorize]
    [HttpPost("summarize")]
    public async Task<IActionResult> Summarize([FromBody] SummarizeRequest rq)
    {
        // 1. Xác thực đầu vào
        if (rq.DocumentId == null && string.IsNullOrWhiteSpace(rq.Text))
            return BadRequest(new { message = "Phải cung cấp DocumentId hoặc Text." });

        // 2. Lấy content
        string content;
        if (rq.DocumentId.HasValue)
        {
            var doc = await _context.Documents.FindAsync(rq.DocumentId.Value);
            if (doc == null) return NotFound(new { message = "Document không tồn tại." });

            // Ví dụ đọc file PDF thành text (cần implement PdfTextExtractor)
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            var path = Path.Combine("wwwroot", doc.PdfUrl.TrimStart('/'));
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            content = PdfTextExtractor.ExtractText(path);
        }
        else
        {
            content = rq.Text!;
        }

        var summary = await _hf.SummarizeAsync(content);

        if (rq.DocumentId.HasValue)
        {
            _context.DocumentSummaries.Add(new DocumentSummary
            {
                DocumentId = rq.DocumentId.Value,
                SummaryText = summary,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        // 5. Trả về response
        return Ok(new SummarizeResponse { Summary = summary });
    }
}
