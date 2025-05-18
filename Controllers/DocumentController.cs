using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using BACKEND.Configuration;
using BACKEND.Models;
using BACKEND.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace BACKEND.Controllers;

[ApiController]
[Route("api/document")]
public class DocumentController : ControllerBase
{
    private readonly DBContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpFactory;
    private readonly PdfCoKeyManager _keyManager;
    private readonly IPdfAiService _pdfAi;
    private readonly string _apiKeyConfig;
    private const string ApiKey = "pp00ypz7e8enqat8wt13y596";
    private const string PdfAiChatUrl = "https://pdf.ai/api/v1/chat";
    private const string PdfAiSummarizeUrl = "https://pdf.ai/api/v1/chat-all";
    private readonly ILogger<DocumentController> _logger;


    public DocumentController(
        IWebHostEnvironment env,
        DBContext context,
        IHttpClientFactory httpFactory,
        PdfCoKeyManager keyManager, IPdfAiService pdfAi,
        IOptions<PdfAiOptions> opts,
        ILogger<DocumentController> logger)
    {
        _env = env;
        _context = context;
        _httpFactory = httpFactory;
        _keyManager = keyManager;
        _pdfAi = pdfAi;
        _apiKeyConfig = opts.Value.ApiKey;
        _logger = logger;
    }

    [Authorize]
    [HttpPost("upload")]
    public async Task<IActionResult> UploadDocument([FromForm] DocumentUploadRequest req)
    {
        // 1. Xác thực user
        var idClaim = User.Claims
            .FirstOrDefault(c => c.Type == "sub"
                              || c.Type == "id"
                              || c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
        if (idClaim == null) return Unauthorized();
        int userId = int.Parse(idClaim.Value);

        var originalsDir = Path.Combine(_env.WebRootPath, "uploads", "originals");
        Directory.CreateDirectory(originalsDir);
        var ext = Path.GetExtension(req.File.FileName);
        var originalName = $"doc_{Guid.NewGuid()}{ext}";
        var localPath = Path.Combine(originalsDir, originalName);
        await using (var fs = new FileStream(localPath, FileMode.Create))
            await req.File.CopyToAsync(fs);

        // 3. Chỉ lưu metadata với ConversionStatus = "Pending", không gọi PDF.co
        var document = new Document
        {
            Title = req.Title,
            Description = req.Description,
            AuthorId = req.AuthorId,
            PublisherId = req.PublisherId,
            CategoryId = req.CategoryId,
            CreatedBy = userId,
            Status = req.Status,
            IsPremium = req.IsPremium,
            ConversionStatus = "Pending",
            Summarystatus = "Pending",
            FileUrl = $"/uploads/originals/{originalName}",

        };
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        return Accepted(new { message = "Tài liệu đã được upload thành công" });
    }
    [HttpPost("chat")]
    public async Task<IActionResult> ChatWithPdf([FromBody] ChatWithPdfRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.DocId) || string.IsNullOrWhiteSpace(req.Message))
            return BadRequest("docId và message là bắt buộc.");

        // Log payload trước khi gửi
        _logger.LogInformation("Calling PDF.ai Chat: docId={DocId}, message={Message}, model={Model}",
                               req.DocId, req.Message, req.Model);

        var client = _httpFactory.CreateClient();
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        var chatPayload = new
        {
            docId = req.DocId,
            message = req.Message,
            save_chat = req.SaveChat,
            language = req.Language,
            model = req.Model
        };

        HttpResponseMessage chatResp;
        string chatBody;
        try
        {
            chatResp = await client.PostAsJsonAsync(PdfAiChatUrl, chatPayload);
            chatBody = await chatResp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Call to /chat failed");
            return StatusCode(502, "Không thể kết nối tới PDF.ai chat service");
        }

        _logger.LogInformation("Chat response: {StatusCode} {Body}", (int)chatResp.StatusCode, chatBody);

        // Nếu chat thành công, parse và trả về luôn
        if (chatResp.IsSuccessStatusCode)
        {
            var chatJson = JsonDocument.Parse(chatBody).RootElement;
            return Ok(new { content = chatJson.GetProperty("content").GetString() });
        }

        // Khi gặp lỗi 500 hoặc chatResp.StatusCode != 200
        _logger.LogWarning("Chat failed ({Status}), falling back to /summarize", chatResp.StatusCode);

        // --- Fallback: gọi /summarize để lấy summary chương 2 ---
        var sumPayload = new
        {
            docId = req.DocId,
            model = "gpt-3.5-turbo",
            language = req.Language
        };

        HttpResponseMessage sumResp;
        string sumBody;
        try
        {
            sumResp = await client.PostAsJsonAsync(PdfAiSummarizeUrl, sumPayload);
            sumBody = await sumResp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Call to /summarize failed");
            return StatusCode(502, "Chat và Summarize đều thất bại");
        }

        if (!sumResp.IsSuccessStatusCode)
        {
            _logger.LogError("Summarize returned {Status}: {Body}", sumResp.StatusCode, sumBody);
            return StatusCode((int)sumResp.StatusCode, sumBody);
        }

        // Trả summary thay thế
        var sumJson = JsonDocument.Parse(sumBody).RootElement;
        return Ok(new
        {
            content = sumJson.GetProperty("content").GetString(),
            fallback_used = true
        });
    }

    public class ChatWithPdfRequest
    {
        public string DocId { get; set; } = "";
        public string Message { get; set; } = "";
        public bool SaveChat { get; set; } = false;
        public string Language { get; set; } = "vietnamese";
        public string Model { get; set; } = "gpt-3.5-turbo";
    }

    // GET: Chi tiết tài liệu (public)
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDocumentById(int id)
    {
        var doc = await _context.Documents
            .Include(d => d.Author)
            .Include(d => d.Category)
            .Include(d => d.Publisher)
            .FirstOrDefaultAsync(d => d.Id == id && d.Status == 1);
        if (doc == null) return NotFound();

        return Ok(new
        {
            id = doc.Id,
            title = doc.Title,
            description = doc.Description,
            author = doc.Author?.Name,
            category = doc.Category?.Name,
            publisher = doc.Publisher?.Name,
            Status = doc.Status,
            isPremium = doc.IsPremium,
            createdAt = doc.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            totalPages = doc.TotalPages,
            pdfUrl = doc.PdfUrl != null ? $"{Request.Scheme}://{Request.Host}{doc.PdfUrl}" : null
        });
    }

    // GET: Xem tài liệu PDF 
    [HttpGet("view/{id}")]
    public async Task<IActionResult> ViewPdf(int id)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc == null || doc.Status == 0) return NotFound();

        var userId = GetUserId();

        return Ok(new
        {
            PdfUrl = $"{Request.Scheme}://{Request.Host}{doc.PdfUrl}",
        });
    }

    // GET: Tải xuống tài liệu (chặn nếu không premium)
    [Authorize]
    [HttpGet("download/{id}")]
    public async Task<IActionResult> Download(int id)
    {
        // 1. Lấy document
        var doc = await _context.Documents.FindAsync(id);
        if (doc == null || doc.Status != 1)
            return NotFound();

        // 2. Tính điểm cần thiết
        int cost = doc.IsPremium ? 100 : 0;

        // 3. Lấy user hiện tại
        int userId = GetUserId();
        var user = await _context.Users.FindAsync(userId);

        // 4. Nếu premium và không đủ điểm thì chặn
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        if (cost > 0 && user.Score < cost)
            return Forbid($"Bạn cần ít nhất {cost} điểm để tải tài liệu premium này.");
#pragma warning restore CS8602 // Dereference of a possibly null reference.

        var filePdf = $"{Request.Scheme}://{Request.Host}{doc.PdfUrl}";
        var fileUrl = $"{Request.Scheme}://{Request.Host}{doc.PdfUrl}";

        // 6. Cập nhật điểm
        if (cost > 0)
        {
            // Trừ điểm của user
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            user.Score -= cost;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            _context.PointTransactions.Add(new PointTransaction
            {
                UserId = userId,
                ChangeAmount = -cost,
                Reason = "download_premium",
                RelatedId = doc.Id
            });
        }

        // Cộng điểm cho tác giả
        var author = await _context.Users.FindAsync(doc.CreatedBy);
        if (author != null && cost > 0)
        {
            author.Score += cost;
            _context.PointTransactions.Add(new PointTransaction
            {
                UserId = author.Id,
                ChangeAmount = cost,
                Reason = "reward_download",
                RelatedId = doc.Id
            });
        }
        _context.Downloads.Add(new Download
        {
            UserId = userId,
            DocumentId = doc.Id,
            DownloadedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        // 8. Trả về kết quả
        return Ok(new
        {
            message = "Tải tài liệu thành công",
            fileUrl,
            filePdf,
        });
    }


    // POST: Đánh giá tài liệu
    [Authorize]
    [HttpPost("rate")]
    public async Task<IActionResult> RateDocument([FromBody] Rating rating)
    {
        var userId = GetUserId();
        var exists = await _context.Ratings.FirstOrDefaultAsync(r =>
            r.DocumentId == rating.DocumentId && r.UserId == userId);

        if (exists != null)
        {
            exists.Score = rating.Score;
        }
        else
        {
            rating.UserId = userId;
            rating.CreatedAt = DateTime.UtcNow;
            _context.Ratings.Add(rating);
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Đánh giá thành công" });
    }

    // POST: Thêm yêu thích
    [Authorize]
    [HttpPost("favorite/{id}")]
    public async Task<IActionResult> ToggleFavorite(int id)
    {
        var userId = GetUserId();
        var fav = await _context.Favorites.FindAsync(userId, id);
        if (fav != null)
        {
            _context.Favorites.Remove(fav);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã xoá khỏi danh sách yêu thích" });
        }

        _context.Favorites.Add(new Favorite
        {
            UserId = userId,
            DocumentId = id,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã thêm vào yêu thích" });
    }

    // GET: Lấy danh sách tài liệu yêu thích
    [Authorize]
    [HttpGet("favorites")]
    public async Task<IActionResult> GetFavorites()
    {
        var userId = GetUserId();
        var favorites = await _context.Favorites
            .Where(f => f.UserId == userId)
            .Select(f => new
            {
                f.Document.Id,
                f.Document.Title,
                f.Document.PdfUrl
            }).ToListAsync();

        return Ok(favorites);
    }

    // POST: Thêm bình luận
    [Authorize]
    [HttpPost("comment")]
    public async Task<IActionResult> PostComment([FromBody] Comment input)
    {
        var userId = GetUserId();
        input.UserId = userId;
        input.CreatedAt = DateTime.UtcNow;

        _context.Comments.Add(input);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã bình luận" });
    }

    // GET: Danh sách bình luận theo tài liệu
    [HttpGet("comments/{documentId}")]
    public async Task<IActionResult> GetComments(int documentId)
    {
        var comments = await _context.Comments
            .Where(c => c.DocumentId == documentId)
            .Include(c => c.User)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Content,
                c.CreatedAt,
                Username = c.User.Username
            }).ToListAsync();

        return Ok(comments);
    }

    // Helper: Lấy userId
    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null ? int.Parse(claim.Value) : 0;
    }

}
