using Microsoft.AspNetCore.Mvc;
using BACKEND.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using VNPAY.NET;
using VNPAY.NET.Models;
using VNPAY.NET.Enums;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly DBContext _context;
    private readonly IVnpay _vnpay;
    private readonly IConfiguration _configuration;
    public PaymentController(DBContext context, IVnpay vnpay, IConfiguration configuration)
    {
        _context = context;
        _vnpay = vnpay;
        _configuration = configuration;
        var tmnCode = _configuration["Vnpay:TmnCode"] ?? throw new Exception("Thiếu Vnpay:TmnCode trong config!");
        var hashSecret = _configuration["Vnpay:HashSecret"] ?? throw new Exception("Thiếu Vnpay:HashSecret trong config!");
        var baseUrl = _configuration["Vnpay:BaseUrl"] ?? throw new Exception("Thiếu Vnpay:BaseUrl trong config!");
        var returnUrl = _configuration["Vnpay:ReturnUrl"] ?? throw new Exception("Thiếu Vnpay:ReturnUrl trong config!");

        _vnpay.Initialize(tmnCode, hashSecret, baseUrl, returnUrl);

    }

    [Authorize]
    [HttpPost("vnpay-recharge")]
    public async Task<IActionResult> CreateVnPayRecharge([FromBody] RechargeCoinRequest req)
    {
        // 1. Bắt buộc là bội số của 10 000
        if (req.Amount <= 0 || req.Amount % 10000 != 0)
            return BadRequest(new { message = "Số tiền phải là bội số của 10 000 VND." });

        // 2. Tính số coin: mỗi 10 000 VND → 300 coin
        int coins = (req.Amount / 10000) * 300;

        // 3. Tạo bản ghi PaymentTransaction
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tx = new PaymentTransaction
        {
            UserId = userId,
            Amount = req.Amount,
            PaymentMethod = "vnpay",
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        _context.PaymentTransactions.Add(tx);
        await _context.SaveChangesAsync();

        var paymentRequest = new PaymentRequest
        {
            PaymentId = tx.Id,
            Money = (int)tx.Amount,
            Description = $"Nạp {tx.Amount:N0} VND → {coins} coin",
            IpAddress = NetworkHelper.GetIpAddress(HttpContext),
            CreatedDate = DateTime.Now,
            Currency = Currency.VND,
            Language = DisplayLanguage.Vietnamese,
            BankCode = BankCode.ANY
        };
        var paymentUrl = _vnpay.GetPaymentUrl(paymentRequest);

        return Ok(new { paymentUrl });
    }


    [AllowAnonymous]
    [HttpGet("callback")]
    public async Task<IActionResult> Callback()
    {
        var result = _vnpay.GetPaymentResult(Request.Query);
        if (!result.IsSuccess)
            return Redirect("/thanh-toan/that-bai");

        var tx = await _context.PaymentTransactions.FindAsync((int)result.PaymentId);
        if (tx == null) return NotFound();

        if (tx.Status != "completed")
        {
            tx.Status = "completed";
            tx.ResponseData = Request.QueryString.Value;
            await _context.SaveChangesAsync();

            // 6. Quy đổi và cộng coin cho user
            int coins = ((int)tx.Amount / 10000) * 300;
            var user = await _context.Users.FindAsync(tx.UserId);
            if (user != null)
            {
                user.Score += coins;
                _context.PointTransactions.Add(new PointTransaction
                {
                    UserId = user.Id,
                    ChangeAmount = coins,
                    Reason = "vnpay_recharge",
                    RelatedId = tx.Id,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }
        }

        return Redirect("/thanh-toan/thanh-cong");
    }
    public class RechargeCoinRequest
    {
        public int Amount { get; set; }
    }

}

