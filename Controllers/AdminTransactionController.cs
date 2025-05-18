using System.Linq;
using System.Threading.Tasks;
using BACKEND.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace f4.Controllers
{
    [ApiController]
    [Route("api/admin/transactions")]
    [Authorize(Roles = "admin")]
    public class AdminTransactionController : ControllerBase
    {
        private readonly DBContext _context;
        public AdminTransactionController(DBContext context)
        {
            _context = context;
        }

        /// <summary>
        /// GET api/admin/transactions/{id}
        /// Lấy chi tiết 1 giao dịch theo ID
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetTransactionDetail(int id)
        {
            var tx = await _context.PaymentTransactions
                .AsNoTracking()
                .Where(pt => pt.Id == id)
                .Select(pt => new
                {
                    pt.Id,
                    pt.PackageId,
                    PackageName = pt.Package.Name,
                    pt.Amount,
                    pt.PaymentMethod,
                    pt.TransactionCode,
                    pt.Status,
                    CreatedAt = pt.CreatedAt,
                    pt.ResponseData,
                    user_name = pt.User.Username,
                    user_id = pt.UserId,
                    user_email = pt.User.Email,
                    user_fullname = pt.User.FullName,
                    user_avatar = pt.User.Avatar != null ? $"{Request.Scheme}://{Request.Host}/uploads/avatars/{pt.User.Avatar}" : null,
                    user_role = pt.User.RoleId,
                    user_created_at = pt.User.CreatedAt,
                })
                .FirstOrDefaultAsync();

            if (tx == null)
                return NotFound(new { message = $"Transaction with id={id} not found." });

            return Ok(tx);
        }
        // Lấy danh sách tất cả người dùng đã thanh toán
        [HttpGet]
        [Authorize(Roles = "admin")]
        [HttpGet]
        public async Task<IActionResult> ListTransactions([FromQuery] TransactionQuery q)
        {
            // 1) Source query
            var baseQ = _context.PaymentTransactions
                .AsNoTracking()
                .Include(pt => pt.User)
                .Include(pt => pt.Package)
                .Select(pt => new
                {
                    pt.Id,
                    pt.TransactionCode,
                    pt.Amount,
                    pt.Status,
                    CreatedAt = pt.CreatedAt,
                    UserId = pt.UserId,
                    Username = pt.User.Username,
                    UserEmail = pt.User.Email,
                    PackageId = pt.PackageId,
                    Package = pt.Package.Name
                });

            // 2) Filter search
            if (!string.IsNullOrWhiteSpace(q.Term))
            {
                var term = q.Term.Trim().ToLower();
                baseQ = baseQ.Where(x =>
                    x.TransactionCode.ToLower().Contains(term) ||
                    x.Username.ToLower().Contains(term) ||
                    x.UserEmail.ToLower().Contains(term));
            }

            // 3) Sort
            bool asc = q.SortDir.Equals("asc", StringComparison.OrdinalIgnoreCase);
            baseQ = q.SortBy switch
            {
                "amount" => asc
                    ? baseQ.OrderBy(x => x.Amount)
                    : baseQ.OrderByDescending(x => x.Amount),
                _ => asc
                    ? baseQ.OrderBy(x => x.CreatedAt)
                    : baseQ.OrderByDescending(x => x.CreatedAt),
            };

            // 4) Pagination
            var totalItems = await baseQ.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)q.Size);

            var items = await baseQ
                .Skip((q.Page - 1) * q.Size)
                .Take(q.Size)
                .ToListAsync();

            // 5) Return
            return Ok(new
            {
                totalItems,
                totalPages,
                page = q.Page,
                size = q.Size,
                hasPrevious = q.Page > 1,
                hasNext = q.Page < totalPages,
                items
            });
        }
    }
    public class TransactionQuery
    {
        public int Page { get; set; } = 1;
        public int Size { get; set; } = 10;
        /// <summary>Tìm kiếm theo mã giao dịch hoặc username hoặc email</summary>
        public string Term { get; set; } = "";
        /// <summary>"amount" hoặc "date"</summary>
        public string SortBy { get; set; } = "date";
        /// <summary>"asc" hoặc "desc"</summary>
        public string SortDir { get; set; } = "desc";
    }
}
