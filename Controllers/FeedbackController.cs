// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.EntityFrameworkCore;
// using BACKEND.Models;
// using System.Security.Claims;

// namespace BACKEND.Controllers;

// [ApiController]
// [Route("api/feedback")]
// public class FeedbackController : ControllerBase
// {
//     private readonly DBContext _context;
//     private readonly EmailService _emailService;

//     public FeedbackController(DBContext context, EmailService emailService)
//     {
//         _context = context;
//         _emailService = emailService;
//     }

//     // [1] Người dùng gửi phản hồi → gửi mail cho admin
//     [Authorize]
//     [HttpPost]
//     public async Task<IActionResult> SendFeedback([FromBody] FeedbackRequest request)
//     {
//         var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
//         if (userIdClaim == null) return Unauthorized();
//         int userId = int.Parse(userIdClaim.Value);

//         var user = await _context.Users.FindAsync(userId);
//         if (user == null) return Unauthorized();

//         if (string.IsNullOrWhiteSpace(request.Message))
//             return BadRequest(new { message = "Nội dung phản hồi không được để trống." });

//         var feedback = new Feedback
//         {
//             UserId = userId,
//             Email = request.Email,
//             Message = request.Message,
//             SentAt = DateTime.UtcNow
//         };

//         _context.Feedbacks.Add(feedback);
//         await _context.SaveChangesAsync();

//         // Gửi mail cho admin
//         try
//         {
//             string adminEmail = "admin@senselib.vn"; // đổi nếu cần
//             string subject = $"[Phản hồi mới] từ {user.Username}";
//             string body = $"Email: {request.Email ?? user.Email}\nUsername: {user.Username}\n\nNội dung:\n{request.Message}";
//             _emailService.SendEmail(adminEmail, subject, body);
//         }
//         catch (Exception ex)
//         {
//             return Ok(new { message = "Đã lưu phản hồi, nhưng gửi email thất bại", error = ex.Message });
//         }

//         return Ok(new { message = "Phản hồi đã được gửi thành công." });
//     }

//     // [2] Admin xem toàn bộ phản hồi
//     [Authorize(Roles = "admin")]
//     [HttpGet("admin")]
//     public async Task<IActionResult> GetAll()
//     {
//         var data = await _context.Feedbacks
//             .Include(f => f.User)
//             .OrderByDescending(f => f.SentAt)
//             .Select(f => new
//             {
//                 f.Id,
//                 f.Message,
//                 f.Email,
//                 f.SentAt,
//                 f.ResponseMessage,
//                 f.ResponseAt,
//                 Username = f.User.Username
//             })
//             .ToListAsync();

//         return Ok(data);
//     }

//     // [3] Admin gửi phản hồi lại cho người dùng (qua email)
//     [Authorize(Roles = "admin")]
//     [HttpPost("admin/respond/{id}")]
//     public async Task<IActionResult> RespondToFeedback(int id, [FromBody] FeedbackResponseRequest input)
//     {
//         var feedback = await _context.Feedbacks
//             .Include(f => f.User)
//             .FirstOrDefaultAsync(f => f.Id == id);
//         if (feedback == null) return NotFound();

//         feedback.ResponseMessage = input.ResponseMessage;
//         feedback.ResponseAt = DateTime.UtcNow;

//         await _context.SaveChangesAsync();

//         // Gửi mail cho người dùng
//         try
//         {
//             string toEmail = feedback.Email ?? feedback.User.Email;
//             string subject = "Phản hồi từ SenseLib";
//             string body = $"Xin chào {feedback.User.Username},\n\nBạn đã gửi phản hồi:\n\"{feedback.Message}\"\n\nPhản hồi của chúng tôi:\n{input.ResponseMessage}";
//             _emailService.SendEmail(toEmail, subject, body);
//         }
//         catch (Exception ex)
//         {
//             return Ok(new { message = "Đã lưu phản hồi, nhưng gửi email cho người dùng thất bại", error = ex.Message });
//         }

//         return Ok(new { message = "Phản hồi đã được gửi cho người dùng." });
//     }
//     public class FeedbackRequest
//     {
//         public string? Email { get; set; }
//         public string Message { get; set; } = null!;
//     }

//     public class FeedbackResponseRequest
//     {
//         public string ResponseMessage { get; set; } = null!;
//     }

// }
