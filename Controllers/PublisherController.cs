using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BACKEND.Models;
using System.ComponentModel.DataAnnotations;

namespace BACKEND.Controllers;

[ApiController]
[Route("api/publishers")]
public class PublisherController : ControllerBase
{
    private readonly DBContext _context;

    public PublisherController(DBContext context)
    {
        _context = context;
    }

    // GET: Lấy danh sách nhà xuất bản
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var publishers = await _context.Publishers
            .Select(p => new
            {
                p.Id,
                p.Name
            }).ToListAsync();

        return Ok(publishers);
    }

    // GET: Chi tiết nhà xuất bản
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var publisher = await _context.Publishers.FindAsync(id);
        if (publisher == null)
            return NotFound(new { message = "Không tìm thấy nhà xuất bản." });

        return Ok(publisher);
    }

    // POST: Thêm nhà xuất bản
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

        var result = new PublisherDto
        {
            Id = publisher.Id,
            Name = publisher.Name
        };
        return CreatedAtAction(nameof(GetById), new { id = publisher.Id }, result);
    }


    // PUT: Cập nhật nhà xuất bản
    [Authorize(Roles = "admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Publisher input)
    {
        if (id != input.Id)
            return BadRequest(new { message = "ID không khớp." });

        var publisher = await _context.Publishers.FindAsync(id);
        if (publisher == null)
            return NotFound(new { message = "Nhà xuất bản không tồn tại." });

        publisher.Name = input.Name;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Cập nhật thành công." });
    }

    // DELETE: Xoá nhà xuất bản (nếu chưa có tài liệu liên kết)
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
    public class CreatePublisherDto
    {
        [Required]
        public string Name { get; set; } = null!;
    }
    public class PublisherDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

}
