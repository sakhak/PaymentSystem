using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentSystem.DTOs;
using PaymentSystem.Services;

namespace PaymentSystem.Controllers;
[Authorize]
[ApiController]
[Route("api/payments")]
public class PaymentsController(PaymentService payments, ILogger<PaymentsController> logger) : ControllerBase
{
    [HttpGet("csrf")]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery) => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken, headerName = "X-CSRF-TOKEN" });
    [HttpGet]
    public async Task<IActionResult> GetAllAction(int page = 1, CancellationToken ct = default)
    {
        page = Math.Clamp(page, 1, 1000000);
        var now = DateTime.UtcNow;
        return Ok(await payments.VisibleTo(User).AsNoTracking().OrderByDescending(p => p.Id).Skip((page - 1) * 50).Take(50)
            .Select(p => new { p.Id, p.OrderId, p.Amount, p.Currency,
                Status = p.Status == "Pending" && p.ExpiresAt <= now ? "Expired" : p.Status,
                p.CreatedAt, p.ExpiresAt }).ToListAsync(ct));
    }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var p = await payments.VisibleTo(User).AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        return p is null ? NotFound() : Ok(new { p.Id, p.OrderId, p.Amount, p.Currency, Status = PaymentService.Status(p), p.CreatedAt, p.ExpiresAt, p.PaidAt });
    }
    [HttpPost("khqr")]
    [HttpPost("create")]
    public async Task<IActionResult> CreateKhqr(CreateKhqrRequest request, CancellationToken ct)
    {
        try
        {
            var p = await payments.CreateAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, request.OrderId, request.Amount, request.Currency, ct: ct);
            return CreatedAtAction(nameof(GetById), new { id = p.Id }, new { p.Id, p.OrderId, p.Amount, p.Currency, p.Qr, p.Md5, p.Status, p.ExpiresAt });
        }
        catch (PaymentException ex) { return Conflict(new { message = ex.Message }); }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "KHQR creation unavailable.");
            return Problem("Payment generation is unavailable. Check server configuration.", statusCode: 503);
        }
    }
    [HttpPost("check")]
    public async Task<IActionResult> Check(CheckPaymentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Md5)) return BadRequest("MD5 is required.");
        var payment = await payments.VisibleTo(User).AsNoTracking().SingleOrDefaultAsync(p => p.Md5 == request.Md5, ct);
        if (payment is null) return NotFound();
        var message = await payments.VerifyAsync(payment, ct);
        var updated = await payments.VisibleTo(User).AsNoTracking().SingleAsync(p => p.Id == payment.Id, ct);
        return Ok(new { payment.Id, Status = PaymentService.Status(updated), message });
    }
}
