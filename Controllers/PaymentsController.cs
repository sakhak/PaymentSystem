using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentSystem.API.Models;
using PaymentSystem.Data;
using PaymentSystem.DTOs;
using PaymentSystem.Services;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;

namespace PaymentSystem.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly BakongService _bakongService;
        private readonly ApplicationDbContext _context;
        private readonly KhqrService _khqrService;
        public PaymentsController(BakongService bakongservice, ApplicationDbContext context, KhqrService khqrService)
        {
            _bakongService = bakongservice;
            _context = context;
            _khqrService = khqrService;
        }
        [HttpGet]
        public async Task<IActionResult> GetAllAction()
        {
            var payment = await _context.Payments
                .AsNoTracking()
                .ToListAsync();
            return Ok(payment);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var payment = await _context.Payments.FindAsync(id);
            if(payment == null)
            {
                return NotFound(new
                {
                    message = "Payment not found.",
                    paymentId = id
                });
            }
            return Ok(payment);
        }



        [HttpPost("check")]
        public async Task<IActionResult> check([FromBody] CheckPaymentRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Md5))
                return BadRequest("Md5 is required.");

            //Find payment in SQLite
            var payment = await _context.Payments
                .FirstOrDefaultAsync(
                    p => p.KhqrMd5 == request.Md5, 
                    cancellationToken);
            if(payment == null)
            {
                return NotFound(new
                {
                    message = "Payment not found in SQLite."
                });
            }
            try
            {
                var resurt = await _bakongService.CheckPaymentAsync(request.Md5, cancellationToken);
                return Ok(new
                {
                    payment.Id,
                    payment.OrderId,
                    payment.Status,
                    BakongReponse = resurt
                });
            }
            catch (BakongApiException ex)
                when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return StatusCode(502, new
                {
                    message = "Bakong rejected the API request (403 Forbidden).",
                    detail = string.IsNullOrWhiteSpace(ex.ResponseExcerpt)
                        ? "Check that the token is valid for the SIT environment and authorized for this API."
                        : ex.ResponseExcerpt
                });
            }
            //Ask Bakong to check transaction
            
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreatePaymentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.OrderId))
                { 
                    return BadRequest("Order id is required"); 
                }
            if(request.Amount <= 0)
                {
                    return BadRequest("Amount must be greater than zero.");
                }
            if (request.Currency != "USD" && request.Currency != "KHR")
                { 
                    return BadRequest("Only USD and  KHR are supported"); 
                }
            var existingPayment = await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.OrderId == request.OrderId);
            if(existingPayment != null)
            {
                return Conflict(new
                {
                    message = "A payment already exists for this Order ID.",
                    orderId = request.OrderId,
                });
            }

            //Generate QR
            var khqr = _khqrService.GenerateIndividual(
                    request.Amount,
                    request.Currency,
                    request.OrderId
                );
            //Create payment object
            var payment = new Payment
            {
                OrderId = request.OrderId,
                Amount = request.Amount,
                Currency = request.Currency,
                Status = "Pending",
                KhqrMd5 = khqr.Md5,
                CreatedAt = DateTime.UtcNow
                

            };
            //Save payment to SQLite
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            //Return payment and KHQR
            return Ok(new
            {
                payment.Id,
                payment.OrderId,
                payment.Amount,
                payment.Currency,
                payment.Status,
                payment.CreatedAt,
                Khqr = khqr.Qr,
                Md5 = khqr.Md5
            });

        }


    }
}
