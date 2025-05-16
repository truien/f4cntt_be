using System.Security.Claims;
using BACKEND.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;


namespace BACKEND.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly DBContext _context;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;
        private readonly EmailService _emailService;

        public AuthController(DBContext context, IConfiguration configuration,
            IMemoryCache memoryCache,
            EmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _memoryCache = memoryCache;
            _emailService = emailService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // 1. Validate đầu vào
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 2. Tìm user theo username hoặc email
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);
            if (user == null)
                return Unauthorized(new { message = "Sai tên đăng nhập hoặc mật khẩu." });

            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            {
                var mins = (int)(user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes;
                return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = $"Tài khoản bị khoá, thử lại sau {mins} phút." }
                );
            }

            if (user.FailedLoginCount >= 3)
            {
                if (string.IsNullOrWhiteSpace(request.CaptchaToken))
                    return BadRequest(new { message = "Vui lòng hoàn thành CAPTCHA." });

                if (!await VerifyCaptchaAsync(request.CaptchaToken!))
                    return BadRequest(new { message = "CAPTCHA không hợp lệ." });
            }

            // 5. Xác thực mật khẩu
            bool valid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!valid)
            {
                user.FailedLoginCount++;
                if (user.FailedLoginCount >= 5)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                    user.FailedLoginCount = 0;
                }
                await _context.SaveChangesAsync();
                return Unauthorized(new { message = "Sai tên đăng nhập hoặc mật khẩu." });
            }

            // 6. Đăng nhập thành công → reset lockout & counter
            user.FailedLoginCount = 0;
            user.LockoutEnd = null;
            await _context.SaveChangesAsync();

            // 7. Tạo claims & cookie
            var roleName = await _context.Roles
                .Where(r => r.Id == user.RoleId)
                .Select(r => r.RoleName)
                .FirstOrDefaultAsync() ?? "user";

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name,           user.Username),
        new Claim(ClaimTypes.Role,           roleName)
    };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(100)
                }
            );

            return Ok(new { message = "Đăng nhập thành công" });
        }


        private async Task<bool> VerifyCaptchaAsync(string token)
        {
            var secret = _configuration["Recaptcha:SecretKey"];
            using var client = new HttpClient();
            var response = await client.GetStringAsync(
                $"https://www.google.com/recaptcha/api/siteverify?secret={secret}&response={token}");
            var json = JsonDocument.Parse(response).RootElement;
            return json.GetProperty("success").GetBoolean();
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // 1. Validate input
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 2. Check duplicate Email / Username
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest(new { message = "Email đã tồn tại." });

            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                return BadRequest(new { message = "Username đã tồn tại." });

            // 3. Hash password with BCrypt
            string hashed = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // 4. Tạo User mới
            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = hashed,
                FullName = request.FullName,
                RoleId = 2
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // 5. Lấy role name
            var roleName = await _context.Roles
                .Where(r => r.Id == user.RoleId)
                .Select(r => r.RoleName)
                .FirstOrDefaultAsync()
                ?? "user";
            // 6. Tạo claims và đăng nhập
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name,           user.Username),
                new Claim(ClaimTypes.Role,           roleName)
            };
            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
            );
            var props = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(100)
            };
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                props
            );

            // 7. Trả về kết quả
            return Ok(new { message = "Đăng ký thành công" });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { message = "Đăng xuất thành công" });
        }
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Email không được để trống." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
                return BadRequest(new { message = "Email không tồn tại trong hệ thống." });

            // Phát sinh token và lưu vào cache 15 phút
            var resetToken = Guid.NewGuid().ToString();
            _memoryCache.Set(resetToken, user.Id, TimeSpan.FromMinutes(15));

            // Tạo link reset
            var frontendUrl = _configuration["FrontendBaseUrl"]?.TrimEnd('/');
            var resetLink = $"{frontendUrl}/reset-password?token={resetToken}";

            // Gửi email
            _emailService.SendEmail(
                user.Email,
                "Link đặt lại mật khẩu",
                $"Nhấn vào đây để đặt lại mật khẩu (có hiệu lực 15 phút): {resetLink}"
            );

            return Ok(new { message = "Liên kết đặt lại mật khẩu đã được gửi tới email của bạn." });
        }
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { message = "Token và mật khẩu mới đều bắt buộc." });

            if (!_memoryCache.TryGetValue(request.Token, out int userId))
                return BadRequest(new { message = "Token không hợp lệ hoặc đã hết hạn." });

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "Người dùng không tồn tại." });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();

            _memoryCache.Remove(request.Token);
            return Ok(new { message = "Đặt lại mật khẩu thành công." });
        }
        [Authorize]
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.OldPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { message = "Mật khẩu cũ và mới đều bắt buộc." });

            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) return Unauthorized();
            int userId = int.Parse(claim.Value);

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "Người dùng không tồn tại." });

            if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
                return BadRequest(new { message = "Mật khẩu cũ không chính xác." });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đổi mật khẩu thành công." });
        }
    }


    // DTO cho Register
    public class RegisterRequest
    {
        [Required] public string Username { get; set; } = null!;
        [Required] public string Email { get; set; } = null!;
        [Required] public string Password { get; set; } = null!;
        public string? FullName { get; set; }
    }
    public class ForgotPasswordRequest
    {
        [Required] public string Email { get; set; } = null!;
    }

    public class ResetPasswordRequest
    {
        [Required] public string Token { get; set; } = null!;
        [Required] public string NewPassword { get; set; } = null!;
    }

    public class ChangePasswordRequest
    {
        [Required] public string OldPassword { get; set; } = null!;
        [Required] public string NewPassword { get; set; } = null!;
    }



    public class LoginRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? CaptchaToken { get; set; }
    }
}
