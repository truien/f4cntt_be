using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BACKEND.Models;
using System.Security.Claims;


using Document = BACKEND.Models.Document;

namespace BACKEND.Controllers;

[ApiController]
[Route("api/admin/documents")]
public class AdminDocumentController : ControllerBase
{
    private readonly DBContext _context;
    private readonly IWebHostEnvironment _env;

    public AdminDocumentController(DBContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    
   
    // PUT: Cập nhật thông tin tài liệu
    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDocument(int id, [FromBody] Document input)
    {
        if (id != input.Id) return BadRequest(new { message = "ID không khớp" });

        var doc = await _context.Documents.FindAsync(id);
        if (doc == null) return NotFound();

        doc.Title = input.Title;
        doc.Description = input.Description;
        doc.AuthorId = input.AuthorId;
        doc.PublisherId = input.PublisherId;
        doc.CategoryId = input.CategoryId;
        doc.IsPremium = input.IsPremium;
        doc.PreviewPageLimit = input.PreviewPageLimit;
        doc.TotalPages = input.TotalPages;
        doc.PdfUrl = input.PdfUrl;
        doc.FileUrl = input.FileUrl;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Cập nhật thành công." });
    }

    // PUT: Duyệt tài liệu\
    [Authorize(Roles = "admin")]
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
    [Authorize(Roles = "admin")]
    [HttpPut("{id}/reject")]
    public async Task<IActionResult> RejectDocument(int id)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc == null) return NotFound();

        doc.IsApproved = false;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã từ chối tài liệu." });
    }

    // DELETE: Xoá tài liệu
    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc == null) return NotFound();

        _context.Documents.Remove(doc);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã xoá tài liệu." });
    }
}
