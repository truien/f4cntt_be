using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BACKEND.Models;

namespace BACKEND.Controllers;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/admin/settings")]
public class SettingsController : ControllerBase
{
    private readonly DBContext _context;

    public SettingsController(DBContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var settings = await _context.Settings
            .Select(s => new
            {
                s.Key,
                s.Value
            }).ToListAsync();

        return Ok(settings);
    }

    // [2] Lấy chi tiết 1 cấu hình theo key
    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key)
    {
        var setting = await _context.Settings.FindAsync(key);
        if (setting == null)
            return NotFound(new { message = "Không tìm thấy cấu hình." });

        return Ok(setting);
    }

    // [3] Cập nhật 1 cấu hình
    [HttpPut("{key}")]
    public async Task<IActionResult> Update(string key, [FromBody] string value)
    {
        var setting = await _context.Settings.FindAsync(key);
        if (setting == null)
            return NotFound(new { message = "Không tìm thấy cấu hình." });

        setting.Value = value;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã cập nhật cấu hình." });
    }

    // [4] Thêm mới cấu hình
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Setting input)
    {
        if (await _context.Settings.AnyAsync(s => s.Key == input.Key))
            return BadRequest(new { message = "Key đã tồn tại." });

        _context.Settings.Add(input);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã thêm cấu hình." });
    }

    // [5] Xoá cấu hình (cân nhắc)
    [HttpDelete("{key}")]
    public async Task<IActionResult> Delete(string key)
    {
        var setting = await _context.Settings.FindAsync(key);
        if (setting == null)
            return NotFound(new { message = "Không tìm thấy cấu hình." });

        _context.Settings.Remove(setting);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã xoá cấu hình." });
    }
}
