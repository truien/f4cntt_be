using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BACKEND.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace BACKEND.Controllers;

[ApiController]
[Route("api/admin/users")]
public class AdminUserController : ControllerBase
{
    private readonly DBContext _context;
    private readonly IWebHostEnvironment _env;

    public AdminUserController(DBContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    // GET: Lấy danh sách tất cả người dùng
    [Authorize(Roles = "admin")]
    [HttpGet]
    public async Task<IActionResult> GetAllUsers(
    [FromQuery] int page = 1,
    [FromQuery] int size = 10,
    [FromQuery] string? search = null,
    [FromQuery] string sortField = "username",
    [FromQuery] string sortDirection = "asc"
)
    {
        // Chuẩn bị query gốc, không lấy tài khoản admin
        var query = _context.Users
            .Include(u => u.Role)
            .Where(u => u.RoleId != 1)
            .AsNoTracking();

        // Tìm kiếm theo username, email, fullname
        if (!string.IsNullOrWhiteSpace(search))
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            query = query.Where(u =>
                u.Username.Contains(search) ||
                u.Email.Contains(search) ||
                u.FullName.Contains(search));
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }

        // Xử lý sắp xếp động theo sortField và sortDirection
        bool asc = sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase);
        query = sortField.ToLower() switch
        {
            "username" => asc ? query.OrderBy(u => u.Username) : query.OrderByDescending(u => u.Username),
            "email" => asc ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email),
            "fullname" => asc ? query.OrderBy(u => u.FullName) : query.OrderByDescending(u => u.FullName),
            "createdat" => asc ? query.OrderBy(u => u.CreatedAt) : query.OrderByDescending(u => u.CreatedAt),
            "isactive" => asc ? query.OrderBy(u => u.IsActive) : query.OrderByDescending(u => u.IsActive),
            "role" => asc ? query.OrderBy(u => u.Role.RoleName) : query.OrderByDescending(u => u.Role.RoleName),
            _ => query.OrderBy(u => u.Username),
        };

        // Tính tổng số lượng người dùng
        var totalItems = await query.CountAsync();

        // Lấy dữ liệu theo trang
        var users = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                u.FullName,
                u.CreatedAt,
                Role = u.Role.RoleName,
                u.IsActive,
                Avatar = !string.IsNullOrEmpty(u.Avatar)
                    ? (u.Avatar.StartsWith("http") ? u.Avatar : $"{Request.Scheme}://{Request.Host}/uploads/avatars/{u.Avatar}")
                    : null,
                CanDelete = !_context.Documents.Any(d => d.CreatedBy == u.Id)
            })
            .ToListAsync();

        // Tính tổng số trang
        var totalPages = (int)Math.Ceiling(totalItems / (double)size);

        // Trả về kết quả đầy đủ
        var result = new
        {
            items = users,
            totalPages,
            totalItems,
            page,
            size,
            hasPrevious = page > 1,
            hasNext = page < totalPages
        };

        return Ok(result);
    }


    // GET: Lấy thông tin 1 người dùng
    [Authorize(Roles = "admin")]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        return Ok(new
        {
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.CreatedAt,
            Role = user.Role.RoleName,
            user.IsActive,
            CanDelete = !_context.Documents.Any(d => d.CreatedBy == user.Id)
        });
    }
    // POST: Tạo tài khoản mới
    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromForm] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Kiểm tra trùng Email/Username
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest(new { message = "Email đã tồn tại." });
        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            return BadRequest(new { message = "Username đã tồn tại." });

        // Hash password
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        string? avatarFileName = null;
        if (request.Avatar != null && request.Avatar.Length > 0)
        {
            // Tạo thư mục nếu chưa tồn tại
            var avatarsDir = Path.Combine(_env.WebRootPath, "uploads/avatars");
            if (!Directory.Exists(avatarsDir))
                Directory.CreateDirectory(avatarsDir);

            // Đặt tên file duy nhất
            var ext = Path.GetExtension(request.Avatar.FileName);
            avatarFileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(avatarsDir, avatarFileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await request.Avatar.CopyToAsync(stream);
        }

        // Tạo entity User, gán Avatar (tên file)
        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            FullName = request.FullName ?? "",
            PasswordHash = hashedPassword,
            RoleId = 2,
            IsActive = true,
            Avatar = avatarFileName
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Tạo tài khoản thành công"
        });
    }
    // PUT: Cập nhật thông tin người dùng
    [Authorize(Roles = "admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromForm] EditUserRequest input)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (id != input.Id)
            return BadRequest(new { message = "ID không khớp." });

        if (await _context.Users.AnyAsync(u => u.Email == input.Email && u.Id != id))
            return BadRequest(new { message = "Email đã tồn tại." });

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        // Xử lý avatar nếu có upload mới
        if (input.Avatar != null && input.Avatar.Length > 0)
        {
            // Thư mục lưu avatars
            var avatarsDir = Path.Combine(_env.WebRootPath, "uploads", "avatars");
            if (!Directory.Exists(avatarsDir))
                Directory.CreateDirectory(avatarsDir);

            // Xoá file cũ nếu có
            if (!string.IsNullOrEmpty(user.Avatar))
            {
                var oldPath = Path.Combine(avatarsDir, user.Avatar);
                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
            }

            // Lưu file mới
            var ext = Path.GetExtension(input.Avatar.FileName);
            var newFileName = $"{Guid.NewGuid()}{ext}";
            var newPath = Path.Combine(avatarsDir, newFileName);
            using var stream = new FileStream(newPath, FileMode.Create);
            await input.Avatar.CopyToAsync(stream);

            user.Avatar = newFileName;
        }

        // Cập nhật các trường còn lại
        user.Email = input.Email;
        user.FullName = input.FullName ?? user.FullName;
        user.IsActive = input.IsActive;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật thông tin người dùng thành công.",
        });
    }
    // PUT: Vô hiệu hoá (không xoá cứng)
    [Authorize(Roles = "admin")]
    [HttpPut("{id}/deactivate")]
    public async Task<IActionResult> DeactivateUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.IsActive = false;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã vô hiệu hoá tài khoản." });
    }
    [Authorize(Roles = "admin")]
    // PUT: Kích hoạt lại tài khoản
    [HttpPut("{id}/activate")]
    public async Task<IActionResult> ActivateUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.IsActive = true;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã kích hoạt lại tài khoản." });
    }

    [Authorize(Roles = "admin")]
    [HttpPut("{id}/toogle")]
    public async Task<IActionResult> ToggleUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.IsActive = !user.IsActive;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã chuyển trạng thái tài khoản." });
    }

    // DELETE: Xoá cứng tài khoản (cân nhắc dùng rất hạn chế)
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        // Kiểm tra xem người dùng có tài liệu nào không
        var documents = await _context.Documents
            .Where(d => d.CreatedBy == id)
            .ToListAsync();
        if (documents.Any())
            return BadRequest(new { message = "Không thể xoá tài khoản vì có tài liệu liên quan." });

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã xoá tài khoản." });
    }
    // Lấy thông tin sau khi đăng nhập
    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var IdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (IdClaim == null) return Unauthorized();
        int userId = int.Parse(IdClaim.Value);
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound();

        return Ok(new
        {
            user.Id,
            user.Username,
            Avatar = !string.IsNullOrEmpty(user.Avatar)
    ? (user.Avatar.StartsWith("http") ? user.Avatar : $"{Request.Scheme}://{Request.Host}/uploads/avatars/{user.Avatar}")
    : null,
            user.Email,
            user.FullName,
            Role = user.Role.RoleName,
            user.IsActive
        });

    }
    // Lấy danh sách tất cả vai trò
    [HttpGet("roles")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAllRoles()
    {
        var roles = await _context.Roles
            .Select(r => new
            {
                r.Id,
                r.RoleName
            })
            .ToListAsync();

        return Ok(roles);
    }

    public class UserQueryParameters
    {
        public int Page { get; set; } = 1;
        public int Size { get; set; } = 10;
    }
    public class EditUserRequest
    {
        [Required] public int Id { get; set; }
        [Required][EmailAddress] public string Email { get; set; } = null!;
        public string? FullName { get; set; }
        public bool IsActive { get; set; }
        public IFormFile? Avatar { get; set; }
    }
    public class CreateUserRequest
    {
        [Required] public required string Username { get; set; }
        [Required][EmailAddress] public required string Email { get; set; }
        [Required] public required string Password { get; set; }
        public string? FullName { get; set; }
        public IFormFile? Avatar { get; set; }
    }

}
