using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BACKEND.Models;
using System.ComponentModel.DataAnnotations;

namespace BACKEND.Controllers;

[ApiController]
[Route("api/authors")]

public class AuthorController : ControllerBase
{
    private readonly DBContext _context;

    public AuthorController(DBContext context)
    {
        _context = context;
    }

    // GET: Lấy tất cả tác giả
    [HttpGet]
    public async Task<IActionResult> GetAllAuthors(
    [FromQuery] int page = 1,
    [FromQuery] int size = 10,
    [FromQuery] string? search = null,
    [FromQuery] string sortField = "name",
    [FromQuery] string sortDirection = "asc"
    )
    {
        var query = _context.Authors.AsQueryable();

        // 2. Lọc theo từ khóa (tìm trong tên hoặc mô tả)
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c =>
                c.Name.Contains(search)
            );
        }

        // 3. Sắp xếp động theo sortField & sortDirection
        bool asc = sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase);
        switch (sortField.ToLower())
        {
            case "name":
                query = asc
                    ? query.OrderBy(c => c.Name)
                    : query.OrderByDescending(c => c.Name);
                break;
            case "bookscount":
                query = asc
                    ? query.OrderBy(c => _context.Documents.Count(d => d.CategoryId == c.Id))
                    : query.OrderByDescending(c => _context.Documents.Count(d => d.CategoryId == c.Id));
                break;
            default:
                query = query.OrderBy(c => c.Name);
                break;
        }

        // 4. Tính tổng và số trang
        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)size);

        // 5. Lấy danh sách page hiện tại
        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(c => new
            {
                c.Id,
                c.Name,
                booksCount = _context.Documents.Count(d => d.CategoryId == c.Id)
            })
            .ToListAsync();

        return Ok(new
        {
            items,
            totalPages,
            totalItems,
            page
        });
    }

    // GET: Chi tiết 1 tác giả
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAuthorById(int id)
    {
        var author = await _context.Authors.Where(a => a.Id == id).Select(p => new
        {
            p.Id,
            p.Name,
            booksCount = _context.Documents.Count(d => d.AuthorId == p.Id)
        }).FirstOrDefaultAsync();
        if (author == null)
            return NotFound(new { message = "Không tìm thấy tác giả." });

        return Ok(author);
    }

    // POST: Thêm mới tác giả
    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<IActionResult> CreateAuthor([FromBody] CreateAuthorDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Kiểm tra trùng tên
        if (await _context.Authors.AnyAsync(a => a.Name == request.Name))
            return BadRequest(new { message = "Tác giả đã tồn tại." });

        // Tạo entity và lưu
        var author = new Author { Name = request.Name };
        _context.Authors.Add(author);
        await _context.SaveChangesAsync();

        // Trả về DTO
        var result = new AuthorDto { Id = author.Id, Name = author.Name };
        return CreatedAtAction(nameof(GetAuthorById), new { id = author.Id }, result);
    }

    // PUT: Cập nhật tác giả
    [Authorize(Roles = "admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAuthor(int id, [FromBody] Author input)
    {
        if (id != input.Id)
            return BadRequest(new { message = "ID không khớp." });

        var author = await _context.Authors.FindAsync(id);
        if (author == null)
            return NotFound(new { message = "Tác giả không tồn tại." });

        if (string.IsNullOrWhiteSpace(input.Name))
            return BadRequest(new { message = "Tên tác giả không được để trống." });

        author.Name = input.Name;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Cập nhật thành công." });
    }

    // DELETE: Xoá tác giả
    [Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAuthor(int id)
    {
        var author = await _context.Authors
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (author == null)
            return NotFound(new { message = "Tác giả không tồn tại." });

        if (author.Documents.Any())
            return BadRequest(new { message = "Không thể xoá vì đã liên kết với tài liệu." });

        _context.Authors.Remove(author);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã xoá tác giả." });
    }
    public class CreateAuthorDto
    {
        [Required]
        public string Name { get; set; } = null!;
    }
    public class AuthorDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }
}
