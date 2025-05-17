using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BACKEND.Models;
using System.ComponentModel.DataAnnotations;

namespace BACKEND.Controllers;

[ApiController]
[Route("api/admin/publishers")]
public class PublisherController : ControllerBase
{
    private readonly DBContext _context;

    public PublisherController(DBContext context)
    {
        _context = context;
    }

    // GET: Lấy danh sách nhà xuất bản với tìm kiếm, sắp xếp, phân trang
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] string? search = null,
        [FromQuery] string sortField = "name",
        [FromQuery] string sortDirection = "asc"
    )
    {
        var query = _context.Publishers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.Name.Contains(search));
        }

        bool asc = sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase);
        query = sortField.ToLower() switch
        {
            "name" => asc ? query.OrderBy(p => p.Name) : query.OrderByDescending(p => p.Name),
            "bookscount" => asc
                ? query.OrderBy(p => _context.Documents.Count(d => d.PublisherId == p.Id))
                : query.OrderByDescending(p => _context.Documents.Count(d => d.PublisherId == p.Id)),
            _ => query.OrderBy(p => p.Name),
        };

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)size);

        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(p => new
            {
                p.Id,
                p.Name,
                BooksCount = _context.Documents.Count(d => d.PublisherId == p.Id)
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

    // GET: Chi tiết 1 nhà xuất bản
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var publisher = await _context.Publishers.FindAsync(id);
        if (publisher == null)
            return NotFound(new { message = "Không tìm thấy nhà xuất bản." });

        var booksCount = await _context.Documents.CountAsync(d => d.PublisherId == publisher.Id);

        return Ok(new
        {
            publisher.Id,
            publisher.Name,
            BooksCount = booksCount
        });
    }

    // POST: Thêm mới nhà xuất bản
    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePublisherDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (await _context.Publishers.AnyAsync(p => p.Name == request.Name))
            return BadRequest(new { message = "Nhà xuất bản đã tồn tại." });

        var publisher = new Publisher { Name = request.Name };
        _context.Publishers.Add(publisher);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Thêm mới thành công."});
    }

    // PUT: Cập nhật nhà xuất bản
    [Authorize(Roles = "admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePublisherDto input)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var publisher = await _context.Publishers.FindAsync(id);
        if (publisher == null)
            return NotFound(new { message = "Nhà xuất bản không tồn tại." });

        publisher.Name = input.Name;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Cập nhật thành công." });
    }

    // DELETE: Xóa nhà xuất bản (chỉ khi chưa có tài liệu liên kết)
    [Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var publisher = await _context.Publishers
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (publisher == null)
            return NotFound(new { message = "Không tìm thấy nhà xuất bản." });

        if (publisher.Documents.Any())
            return BadRequest(new { message = "Không thể xoá vì đã liên kết với tài liệu." });

        _context.Publishers.Remove(publisher);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã xoá nhà xuất bản." });
    }

    // DTO Classes
    public class CreatePublisherDto
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = null!;
    }

    public class UpdatePublisherDto
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = null!;
    }

    public class PublisherDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }
}
