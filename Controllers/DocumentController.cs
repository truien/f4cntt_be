using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using BACKEND.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public DocumentController(
        IWebHostEnvironment env,
        DBContext context,
        IHttpClientFactory httpFactory,
        PdfCoKeyManager keyManager)
    {
        _env = env;
        _context = context;
        _httpFactory = httpFactory;
        _keyManager = keyManager;
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
            FileUrl = $"/uploads/originals/{originalName}",
            // không set ConversionJobId,
            ConversionStatus = "Pending"
        };
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        return Accepted(new { message = "Tài liệu đã được upload thành công" });
    }    // GET: Chi tiết tài liệu (public)
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

    // GET: Xem tài liệu PDF (chỉ xem preview nếu không premium)
    [HttpGet("view/{id}")]
    public async Task<IActionResult> ViewPdf(int id)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc == null || doc.Status == 0) return NotFound();

        var userId = GetUserId();
        var hasPremium = await HasPremiumAccess(userId);
        int? allowedPages = hasPremium ? doc.TotalPages : doc.PreviewPageLimit;

        return Ok(new
        {
            PdfUrl = $"{Request.Scheme}://{Request.Host}{doc.PdfUrl}",
            AllowedPages = allowedPages
        });
    }

    // GET: Tải xuống tài liệu (chặn nếu không premium)
    [Authorize]
    [HttpGet("download/{id}")]
    public async Task<IActionResult> Download(int id)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc == null || doc.Status == 0 || doc.Status == 2) return NotFound();

        var userId = GetUserId();
        var hasPremium = await HasPremiumAccess(userId);
        if (!hasPremium && doc.IsPremium)
            return Forbid("Bạn cần mua gói Premium để tải tài liệu này.");

        // Log download
        _context.Downloads.Add(new Download
        {
            UserId = userId,
            DocumentId = doc.Id,
            DownloadedAt = DateTime.UtcNow
        });

        // Cập nhật số lượt tải của gói premium
        var userPremium = await _context.UserPremiums
            .Include(p => p.Package)
            .Where(p => p.UserId == userId && p.EndDate >= DateTime.UtcNow)
            .FirstOrDefaultAsync();

        if (userPremium != null)
        {
            userPremium.DownloadsUsed++;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Tải file thành công",
            FileUrl = $"{Request.Scheme}://{Request.Host}{doc.FileUrl}"
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

    private async Task<bool> HasPremiumAccess(int userId)
    {
        var now = DateTime.UtcNow;
        return await _context.UserPremiums.AnyAsync(p =>
            p.UserId == userId &&
            p.StartDate <= now &&
            p.EndDate >= now &&
            p.DownloadsUsed < p.Package.MaxDownloads);
    }
}
