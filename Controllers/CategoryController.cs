using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BACKEND.Models;
using System.ComponentModel.DataAnnotations;


namespace BACKEND.Controllers;

[ApiController]
[Route("api/admin/categories")]
public class CategoryController : ControllerBase
{
    private readonly DBContext _context;

    public CategoryController(DBContext context)
    {
        _context = context;
    }

    // GET: Danh sách tất cả danh mục
    [HttpGet]
    public async Task<IActionResult> GetAll(
       [FromQuery] int page = 1,
       [FromQuery] int size = 10,
       [FromQuery] string? search = null,
       [FromQuery] string sortField = "name",
       [FromQuery] string sortDirection = "asc")
    {
        // 1. Tạo query gốc
        var query = _context.Categories.AsQueryable();

        // 2. Lọc theo từ khóa (tìm trong tên hoặc mô tả)
        if (!string.IsNullOrWhiteSpace(search))
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            query = query.Where(c =>
                c.Name.Contains(search) ||
                c.Description.Contains(search)
            );
#pragma warning restore CS8602 // Dereference of a possibly null reference.
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
            case "description":
                query = asc
                    ? query.OrderBy(c => c.Description)
                    : query.OrderByDescending(c => c.Description);
                break;
            case "isactive":
                query = asc
                    ? query.OrderBy(c => c.IsActive)
                    : query.OrderByDescending(c => c.IsActive);
                break;
            case "bookscount":
                // Nếu muốn sắp xếp theo số tài liệu
                query = asc
                    ? query.OrderBy(c => _context.Documents.Count(d => d.CategoryId == c.Id))
                    : query.OrderByDescending(c => _context.Documents.Count(d => d.CategoryId == c.Id));
                break;
            default:
                // Mặc định sắp xếp theo Name
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
                c.Description,
                c.IsActive,
                booksCount = _context.Documents.Count(d => d.CategoryId == c.Id)
            })
            .ToListAsync();

        // 6. Trả về kết quả theo format yêu cầu
        return Ok(new
        {
            items,
            totalPages,
            totalItems,
            page
        });
    }


    // GET: Lấy chi tiết 1 danh mục
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return NotFound();

        return Ok(new
        {
            category.Id,
            category.Name,
            category.Description,
            category.IsActive,
            CreatedAt = category.CreatedAt,
            booksCount = _context.Documents.Count(d => d.CategoryId == category.Id),
        });
    }

    // POST: Thêm mới danh mục
    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Kiểm tra trùng tên
        if (await _context.Categories.AnyAsync(c => c.Name == request.Name))
            return BadRequest(new { message = "Danh mục đã tồn tại." });

        var category = new Category
        {
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive
        };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        // Trả về DTO kèm status 201
        var result = new CategoryDto { Id = category.Id, Name = category.Name };
        return CreatedAtAction(nameof(GetById), new { id = category.Id }, result);
    }


    // PUT: Cập nhật danh mục
    [Authorize(Roles = "admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCategoryDto input)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return NotFound();
        category.Name = input.Name;
        category.Description = input.Description;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Cập nhật danh mục thành công." });
    }

    // DELETE: Ngưng hoạt động (soft-delete)
    [Authorize(Roles = "admin")]
    [HttpPut("{id}/toggle")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return NotFound();
        if (category.IsActive == false)
        {
            // Nếu đã ngưng hoạt động thì kích hoạt lại
            category.IsActive = true;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã kích hoạt lại danh mục." });
        }
        else
        {
            // Nếu không có tài liệu thì ngưng hoạt động
            category.IsActive = false;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã ngưng hoạt động danh mục." });
        }
    }
    // DELETE: Xoá cứng danh mục
    [Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return NotFound();

        // Kiểm tra xem có tài liệu nào thuộc danh mục này không
        if (await _context.Documents.AnyAsync(d => d.CategoryId == id))
            return BadRequest(new { message = "Không thể xoá danh mục này vì có tài liệu thuộc danh mục." });
        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã xoá danh mục." });
    }

    public class CreateCategoryDto
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = null!;
        [StringLength(500)]
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateCategoryDto
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = null!;
        [StringLength(500)]
        public string? Description { get; set; }
    }

    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }
}
