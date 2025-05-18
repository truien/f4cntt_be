using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BACKEND.Models;
using BACKEND.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BACKEND.Controllers
{
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

        // GET api/document
        [HttpGet]
        public async Task<IActionResult> GetDocuments([FromQuery] DocumentQueryParams query)
        {
            var docs = _context.Documents
                .Include(d => d.Category)
                .Include(d => d.Author)
                .Include(d => d.Publisher)
                .Where(d => d.Status == 1);

            // Search
            if (!string.IsNullOrWhiteSpace(query.Search))
                docs = docs.Where(d => d.Title.Contains(query.Search));

            // Filter by category
            if (query.CategoryId.HasValue)
                docs = docs.Where(d => d.CategoryId == query.CategoryId.Value);

            // Sorting
            docs = (query.SortBy, query.SortOrder) switch
            {
                (DocumentSortBy.Oldest, SortOrder.Asc) => docs.OrderBy(d => d.CreatedAt),
                (DocumentSortBy.Oldest, SortOrder.Desc) => docs.OrderByDescending(d => d.CreatedAt),
                (DocumentSortBy.Popularity, SortOrder.Asc) => docs.OrderBy(d => _context.Downloads.Count(dl => dl.DocumentId == d.Id)),
                (DocumentSortBy.Popularity, SortOrder.Desc) => docs.OrderByDescending(d => _context.Downloads.Count(dl => dl.DocumentId == d.Id)),
                _ => docs.OrderByDescending(d => d.CreatedAt) // Newest desc
            };

            var total = await docs.CountAsync();

            var items = await docs
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(d => new
                {
                    Id = d.Id,
                    Title = d.Title,
                    CategoryName = d.Category.Name,
                    CreatedAt = d.CreatedAt,
                    FileType = Path.GetExtension(d.FileUrl).TrimStart('.').ToUpper(),
                    DownloadCount = _context.Downloads.Count(dl => dl.DocumentId == d.Id),
                    IsPremium = d.IsPremium,
                    BannerImage = d.ImgUrl != null ? $"{Request.Scheme}://{Request.Host}{d.ImgUrl}" : null,

                })
                .ToListAsync();

            return Ok(new
            {
                TotalItems = total,
                Page = query.Page,
                PageSize = query.PageSize,
                Items = items
            });
        }

        // POST api/document/upload
        [Authorize]
        [HttpPost("upload")]
        public async Task<IActionResult> UploadDocument([FromForm] DocumentUploadRequest req)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim == null) return Unauthorized();
            int userId = int.Parse(idClaim.Value);

            // Lưu PDF gốc
            var pdfDir = Path.Combine(_env.WebRootPath, "uploads", "originals");
            Directory.CreateDirectory(pdfDir);
            var pdfExt = Path.GetExtension(req.File.FileName);
            var pdfName = $"doc_{Guid.NewGuid()}{pdfExt}";
            var pdfPath = Path.Combine(pdfDir, pdfName);
            await using (var fs = new FileStream(pdfPath, FileMode.Create))
                await req.File.CopyToAsync(fs);

            // Lưu banner image
            string? bannerUrl = null;
            if (req.Image?.Length > 0)
            {
                var imgDir = Path.Combine(_env.WebRootPath, "uploads", "BannerDocuments");
                Directory.CreateDirectory(imgDir);
                var imgExt = Path.GetExtension(req.Image.FileName);
                var imgName = $"banner_{Guid.NewGuid()}{imgExt}";
                var imgPath = Path.Combine(imgDir, imgName);
                await using (var fs = new FileStream(imgPath, FileMode.Create))
                    await req.Image.CopyToAsync(fs);
                bannerUrl = $"/uploads/BannerDocuments/{imgName}";
            }

            // Tạo record
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
                FileUrl = $"/uploads/originals/{pdfName}",
                ImgUrl = bannerUrl
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            return Accepted(new { message = "Tài liệu đã được upload thành công" });
        }

        // GET api/document/{id}
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
                status = doc.Status,
                isPremium = doc.IsPremium,
                createdAt = doc.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                totalPages = doc.TotalPages,
                pdfUrl = $"{Request.Scheme}://{Request.Host}{doc.FileUrl}",
                bannerImage = doc.ImgUrl != null ? $"{Request.Scheme}://{Request.Host}{doc.ImgUrl}" : null
            });
        }

        // GET api/document/view/{id}
        [HttpGet("view/{id}")]
        public async Task<IActionResult> ViewPdf(int id)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc == null || doc.Status != 1) return NotFound();

            var userId = GetUserId();
            var hasPremium = await HasPremiumAccess(userId);
            int? allowed = hasPremium ? doc.TotalPages : doc.PreviewPageLimit;

            return Ok(new
            {
                PdfUrl = $"{Request.Scheme}://{Request.Host}{doc.FileUrl}",
                AllowedPages = allowed
            });
        }

        // GET api/document/download/{id}
        [Authorize]
        [HttpGet("download/{id}")]
        public async Task<IActionResult> Download(int id)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc == null || doc.Status != 1) return NotFound();

            var userId = GetUserId();
            var hasPremium = await HasPremiumAccess(userId);
            if (!hasPremium && doc.IsPremium)
                return Forbid("Bạn cần mua gói Premium để tải tài liệu này.");

            _context.Downloads.Add(new Download
            {
                UserId = userId,
                DocumentId = doc.Id,
                DownloadedAt = DateTime.UtcNow
            });
            var userPremium = await _context.UserPremiums
                .Include(p => p.Package)
                .Where(p => p.UserId == userId && p.EndDate >= DateTime.UtcNow)
                .FirstOrDefaultAsync();
            if (userPremium != null) userPremium.DownloadsUsed++;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Tải file thành công",
                fileUrl = $"{Request.Scheme}://{Request.Host}{doc.FileUrl}",
                bannerImage = doc.ImgUrl != null ? $"{Request.Scheme}://{Request.Host}{doc.ImgUrl}" : null
            });
        }

        // POST api/document/rate
        [Authorize]
        [HttpPost("rate")]
        public async Task<IActionResult> RateDocument([FromBody] Rating rating)
        {
            var userId = GetUserId();
            var existing = await _context.Ratings
                .FirstOrDefaultAsync(r => r.DocumentId == rating.DocumentId && r.UserId == userId);
            if (existing != null) existing.Score = rating.Score;
            else
            {
                rating.UserId = userId;
                rating.CreatedAt = DateTime.UtcNow;
                _context.Ratings.Add(rating);
            }
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đánh giá thành công" });
        }

        // POST api/document/favorite/{id}
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
                return Ok(new { message = "Đã xoá khỏi yêu thích" });
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

        // GET api/document/favorites
        [Authorize]
        [HttpGet("favorites")]
        public async Task<IActionResult> GetFavorites()
        {
            var userId = GetUserId();
            var list = await _context.Favorites
                .Where(f => f.UserId == userId)
                .Select(f => new { f.DocumentId, f.Document.Title, f.Document.FileUrl })
                .ToListAsync();
            return Ok(list);
        }

        // POST api/document/comment
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

        [HttpGet("details/{id}")]
        public async Task<IActionResult> GetDocumentDetails(int id)
        {
            var doc = await _context.Documents
                .Include(d => d.Author)
                .Include(d => d.Category)
                .Include(d => d.Publisher)
                .FirstOrDefaultAsync(d => d.Id == id && d.Status == 1);
            if (doc == null) return NotFound();

            // Tính tổng lượt tải
            var downloadCount = await _context.Downloads
                .CountAsync(d => d.DocumentId == id);

            // Tính trung bình đánh giá
            var ratingData = await _context.Ratings
                .Where(r => r.DocumentId == id)
                .GroupBy(r => r.DocumentId)
                .Select(g => new
                {
                    Count = g.Count(),
                    Average = g.Average(r => r.Score)
                })
                .FirstOrDefaultAsync();
            int ratingCount = ratingData?.Count ?? 0;
            double ratingAvg = ratingData?.Average ?? 0;

            // Tính thời gian đọc ước tính (giả sử 30 trang/giờ)
            double readHours = Math.Round((doc.TotalPages ?? 0) / 30.0, 1);

            // Số trang preview (nếu premium thì 1/3, else full)
            int previewPages = doc.IsPremium
                ? (doc.TotalPages ?? 0) / 3
                : doc.TotalPages ?? 0;

            return Ok(new
            {
                id = doc.Id,
                title = doc.Title,
                description = doc.Description,
                category = new { doc.Category.Id, doc.Category.Name },
                author = new { doc.Author.Id, doc.Author.Name },
                publisher = new { doc.Publisher.Id, doc.Publisher.Name },
                uploadedAt = doc.CreatedAt.ToString("yyyy-MM-dd"),
                totalPages = doc.TotalPages,
                previewPages = previewPages,
                isPremium = doc.IsPremium,
                readTimeHours = readHours,
                downloadCount = downloadCount,
                ratingCount = ratingCount,
                ratingAverage = Math.Round(ratingAvg, 1),
                pdfUrl = $"{Request.Scheme}://{Request.Host}{doc.FileUrl}",
                bannerImage = doc.ImgUrl != null
                                       ? $"{Request.Scheme}://{Request.Host}{doc.ImgUrl}"
                                       : null,
            });
        }
        // GET api/document/comments/{documentId}
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
                })
                .ToListAsync();
            return Ok(comments);
        }

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

    // DTOs & helpers

    public class DocumentQueryParams
    {
        public string? Search { get; set; }
        public int? CategoryId { get; set; }
        public DocumentSortBy SortBy { get; set; } = DocumentSortBy.Newest;
        public SortOrder SortOrder { get; set; } = SortOrder.Desc;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public enum DocumentSortBy { Newest, Oldest, Popularity, Size }

    public enum SortOrder { Asc, Desc }


    public class PagedResult<T>
    {
        public int TotalItems { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    }
}
