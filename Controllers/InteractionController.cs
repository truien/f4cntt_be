using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BACKEND.Models;

namespace BACKEND.Controllers;

[ApiController]
[Route("api/admin/interactions")]
public class InteractionController : ControllerBase
{
    private readonly DBContext _context;

    public InteractionController(DBContext context)
    {
        _context = context;
    }

    // [1] Lấy danh sách bình luận
    [HttpGet("comments")]
    public async Task<IActionResult> GetComments()
    {
        var data = await _context.Comments
            .Include(c => c.User)
            .Include(c => c.Document)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Content,
                c.CreatedAt,
                Username = c.User.Username,
                DocumentTitle = c.Document.Title
            }).ToListAsync();

        return Ok(data);
    }

    // [2] Lấy danh sách đánh giá
    [Authorize(Roles = "admin")]
    [HttpGet("ratings")]
    public async Task<IActionResult> GetRatings()
    {
        var data = await _context.Ratings
            .Include(r => r.User)
            .Include(r => r.Document)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Score,
                r.CreatedAt,
                Username = r.User.Username,
                DocumentTitle = r.Document.Title
            }).ToListAsync();

        return Ok(data);
    }

    // [3] Lấy danh sách lượt tải xuống
    [Authorize(Roles = "admin")]
    [HttpGet("downloads")]
    public async Task<IActionResult> GetDownloads()
    {
        var data = await _context.Downloads
            .Include(d => d.User)
            .Include(d => d.Document)
            .OrderByDescending(d => d.DownloadedAt)
            .Select(d => new
            {
                d.Id,
                d.DownloadedAt,
                Username = d.User.Username,
                DocumentTitle = d.Document.Title
            }).ToListAsync();

        return Ok(data);
    }

    // [4] Lấy danh sách yêu thích
    [HttpGet("favorites")]
    public async Task<IActionResult> GetFavorites()
    {
        var data = await _context.Favorites
            .Include(f => f.User)
            .Include(f => f.Document)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                f.UserId,
                f.DocumentId,
                f.CreatedAt,
                Username = f.User.Username,
                DocumentTitle = f.Document.Title
            }).ToListAsync();

        return Ok(data);
    }
}
