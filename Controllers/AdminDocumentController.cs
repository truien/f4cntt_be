using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BACKEND.Models;
using System.ComponentModel.DataAnnotations;

namespace BACKEND.Controllers;

[ApiController]
[Route("api/admin/documents")]
[Authorize(Roles = "admin")]
public class AdminDocumentController : ControllerBase
{
    private readonly DBContext _context;
    private readonly IWebHostEnvironment _env;

    public AdminDocumentController(DBContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    // GET: Lấy danh sách tài liệu (phân trang, tìm kiếm, sắp xếp)
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] string? search = null,
        [FromQuery] string sortField = "createdAt",
        [FromQuery] string sortDirection = "desc"
    )
    {
        var query = _context.Documents
            .Include(d => d.Author)
            .Include(d => d.Category)
            .Include(d => d.Publisher)
            .AsQueryable();

        // Tìm kiếm theo tiêu đề, mô tả hoặc tên tác giả
        if (!string.IsNullOrWhiteSpace(search))
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            query = query.Where(d =>
                d.Title.Contains(search) ||
                d.Description.Contains(search) ||
                d.Author.Name.Contains(search));
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }

        bool asc = sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase);
        query = sortField.ToLower() switch
        {
            "title" => asc ? query.OrderBy(d => d.Title) : query.OrderByDescending(d => d.Title),
            "createdat" => asc ? query.OrderBy(d => d.CreatedAt) : query.OrderByDescending(d => d.CreatedAt),
            "isapproved" => asc ? query.OrderBy(d => d.IsApproved) : query.OrderByDescending(d => d.IsApproved),
            "author" => asc ? query.OrderBy(d => d.Author.Name) : query.OrderByDescending(d => d.Author.Name),
            _ => query.OrderByDescending(d => d.CreatedAt),
        };

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)size);

        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(d => new
            {
                d.Id,
                d.Title,
                d.Description,
                Author = d.Author.Name,
                Category = d.Category.Name,
                Publisher = d.Publisher.Name,
                d.IsApproved,
                d.IsPremium,
                d.CreatedAt,
                d.TotalPages
            })
            .ToListAsync();

        return Ok(new
        {
            items,
            totalPages,
            totalItems,
            page,
            size,
            hasPrevious = page > 1,
            hasNext = page < totalPages
        });
    }

    // GET: Chi tiết 1 tài liệu
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var doc = await _context.Documents
            .Include(d => d.Author)
            .Include(d => d.Category)
            .Include(d => d.Publisher)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null)
            return NotFound(new { message = "Không tìm thấy tài liệu." });

        return Ok(new
        {
            doc.Id,
            doc.Title,
            doc.Description,
            doc.TotalPages,
            doc.PreviewPageLimit,
            doc.IsApproved,
            doc.IsPremium,
            Author = doc.Author.Name,
            Category = doc.Category.Name,
            Publisher = doc.Publisher.Name,
            doc.CreatedAt,
            pdf = $"{Request.Scheme}://{Request.Host}/{doc.PdfUrl}",
        });
    }

    // PUT: Cập nhật tài liệu
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDocument(int id, [FromForm] UpdateDocumentWithFileDto input)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var doc = await _context.Documents.FindAsync(id);
        if (doc == null)
            return NotFound(new { message = "Tài liệu không tồn tại." });

        // 1. Nếu có file mới đính kèm
        if (input.File != null && input.File.Length > 0)
        {
            // Thư mục lưu bản gốc
            var originalsDir = Path.Combine(_env.WebRootPath, "uploads", "originals");
            if (!Directory.Exists(originalsDir))
                Directory.CreateDirectory(originalsDir);

            // Xóa file gốc cũ
            if (!string.IsNullOrEmpty(doc.FileUrl))
            {
                var oldPath = Path.Combine(_env.WebRootPath, doc.FileUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
            }
            // Xóa PDF cũ (nếu có)
            if (!string.IsNullOrEmpty(doc.PdfUrl))
            {
                var oldPdf = Path.Combine(_env.WebRootPath, doc.PdfUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldPdf))
                    System.IO.File.Delete(oldPdf);
            }

            // Lưu file gốc mới
            var ext = Path.GetExtension(input.File.FileName);
            var newFileName = $"doc_{Guid.NewGuid()}{ext}";
            var newPath = Path.Combine(originalsDir, newFileName);
            using var fs = new FileStream(newPath, FileMode.Create);
            await input.File.CopyToAsync(fs);

            doc.FileUrl = $"/uploads/originals/{newFileName}";

            doc.ConversionStatus = "Pending";
            doc.ConversionJobId = null;
        }

        doc.Title = input.Title;
        doc.Description = input.Description;
        doc.AuthorId = input.AuthorId;
        doc.PublisherId = input.PublisherId;
        doc.CategoryId = input.CategoryId;
        doc.IsPremium = input.IsPremium;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Cập nhật tài liệu thành công." });
    }

    // PUT: Duyệt tài liệu
    [HttpPut("{id}/approve")]
    public async Task<IActionResult> ApproveDocument(int id)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc == null) return NotFound();

        doc.IsApproved = true;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã duyệt tài liệu." });
    }

    // PUT: Từ chối tài liệu
    [HttpPut("{id}/reject")]
    public async Task<IActionResult> RejectDocument(int id)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc == null) return NotFound();

        doc.IsApproved = false;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã từ chối tài liệu." });
    }

    // DELETE: Xóa tài liệu
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc == null) return NotFound();

        _context.Documents.Remove(doc);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã xoá tài liệu." });
    }

    // DTO Classes
    public class UpdateDocumentWithFileDto
    {
        [Required] public int Id { get; set; }

        [Required, StringLength(300)]
        public string Title { get; set; } = null!;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required] public int AuthorId { get; set; }
        [Required] public int PublisherId { get; set; }
        [Required] public int CategoryId { get; set; }
        [Required] public bool IsPremium { get; set; }

        // Cho phép đính kèm file mới
        public IFormFile? File { get; set; }
    }
}
