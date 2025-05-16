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

    public DocumentController(
        IWebHostEnvironment env,
        DBContext context,
        IHttpClientFactory httpFactory)
    {
        _env = env;
        _context = context;
        _httpFactory = httpFactory;
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

        // 2. Lưu file gốc vào wwwroot/uploads/originals
        var originalsDir = Path.Combine(_env.WebRootPath, "uploads", "originals");
        Directory.CreateDirectory(originalsDir);

        var ext = Path.GetExtension(req.File.FileName);
        var originalName = $"doc_{Guid.NewGuid()}{ext}";
        var originalPath = Path.Combine(originalsDir, originalName);
        await using (var fs = new FileStream(originalPath, FileMode.Create))
            await req.File.CopyToAsync(fs);

        var remotePdfUrl = await ConvertFileToPdfViaUploadAsync(originalPath, req.File.ContentType, originalName);

        // 4. Download PDF về server
        var pdfDir = Path.Combine(_env.WebRootPath, "uploads", "pdfs");
        Directory.CreateDirectory(pdfDir);
        var pdfFileName = Path.GetFileNameWithoutExtension(originalName) + ".pdf";
        var pdfPath = Path.Combine(pdfDir, pdfFileName);
        await DownloadFileAsync(remotePdfUrl, pdfPath);

        // 5. Đếm số trang và tính previewLimit
        int totalPages;
        using (var pdfDoc = PdfDocument.Open(pdfPath))
            totalPages = pdfDoc.NumberOfPages;
        int previewLimit = Math.Max(1, totalPages / 3);

        // 6. Lưu metadata vào CSDL
        var doc = new Document
        {
            Title = req.Title,
            Description = req.Description,
            AuthorId = req.AuthorId,
            PublisherId = req.PublisherId,
            CategoryId = req.CategoryId,
            CreatedBy = userId,
            IsApproved = req.IsApproved,
            IsPremium = req.IsPremium,
            TotalPages = totalPages,
            PreviewPageLimit = previewLimit,
            FileUrl = $"/uploads/originals/{originalName}",
            PdfUrl = $"/uploads/pdfs/{pdfFileName}"
        };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();

        // 7. Trả về URL public
        var publicPdfUrl = $"{Request.Scheme}://{Request.Host}{doc.PdfUrl}";
        return Ok(new
        {
            message = "Upload & convert PDF thành công.",
            documentId = doc.Id,
            totalPages,
            previewLimit,
            pdfUrl = publicPdfUrl
        });
    }
    // GET: Chi tiết tài liệu (public)
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDocumentById(int id)
    {
        var doc = await _context.Documents
            .Include(d => d.Author)
            .Include(d => d.Category)
            .Include(d => d.Publisher)
            .FirstOrDefaultAsync(d => d.Id == id && d.IsApproved == true);
        if (doc == null) return NotFound();

        return Ok(new
        {
            doc.Id,
            doc.Title,
            doc.Description,
            doc.TotalPages,
            doc.PreviewPageLimit,
            doc.IsPremium,
            Author = doc.Author?.Name,
            Category = doc.Category?.Name,
            Publisher = doc.Publisher?.Name,
            PdfUrl = $"{Request.Scheme}://{Request.Host}{doc.PdfUrl}"
        });
    }

    // GET: Xem tài liệu PDF (chỉ xem preview nếu không premium)
    [HttpGet("view/{id}")]
    public async Task<IActionResult> ViewPdf(int id)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc == null || !doc.IsApproved) return NotFound();

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
        if (doc == null || !doc.IsApproved) return NotFound();

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

    // Helper: Kiểm tra Premium
    private async Task<bool> HasPremiumAccess(int userId)
    {
        var now = DateTime.UtcNow;
        return await _context.UserPremiums.AnyAsync(p =>
            p.UserId == userId &&
            p.StartDate <= now &&
            p.EndDate >= now &&
            p.DownloadsUsed < p.Package.MaxDownloads);
    }
    private async Task<string> ConvertFileToPdfViaUploadAsync(
    string filePath, string contentType, string fileName)
    {
        var client = _httpFactory.CreateClient("PdfCo");

        // 1) Upload file gốc để lấy URL
        using var uploadContent = new MultipartFormDataContent();
        await using var fs = System.IO.File.OpenRead(filePath);
        var streamContent = new StreamContent(fs);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        uploadContent.Add(streamContent, "file", fileName);

        var uploadResp = await client.PostAsync("file/upload", uploadContent);
        uploadResp.EnsureSuccessStatusCode();
        var uploadJson = JsonDocument.Parse(
            await uploadResp.Content.ReadAsStringAsync()
        );
        // trường "url" trỏ đến file đã upload :contentReference[oaicite:0]{index=0}
        var fileUrl = uploadJson
            .RootElement
            .GetProperty("url")
            .GetString()!;

        // 2) Convert từ URL sang PDF
        var convertPayload = new
        {
            name = Path.GetFileNameWithoutExtension(fileName) + ".pdf",
            url = fileUrl,
            async = false
        };
        var convertResp = await client.PostAsJsonAsync(
            "pdf/convert/from/doc", convertPayload
        );
        convertResp.EnsureSuccessStatusCode();
        using var convertJson = JsonDocument.Parse(await convertResp.Content.ReadAsStringAsync());
        var urlElement = convertJson.RootElement.GetProperty("url");
        string pdfUrl;

        // nếu lỡ là mảng thì vẫn xử lý được, còn thường là string
        if (urlElement.ValueKind == JsonValueKind.Array)
        {
            pdfUrl = urlElement[0].GetString()!;
        }
        else
        {
            pdfUrl = urlElement.GetString()!;
        }
        return pdfUrl;
    }

    // Tải file từ URL về localPath
    private async Task DownloadFileAsync(string url, string localPath)
    {
        var client = _httpFactory.CreateClient("PdfCo");
        var bytes = await client.GetByteArrayAsync(url);
        await System.IO.File.WriteAllBytesAsync(localPath, bytes);
    }
}
