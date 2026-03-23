using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Services;
using System.Security.Claims;

namespace RetailERP.Controllers;

/// <summary>
/// Sprint 2: Razorpay payment gateway controller.
/// Handles order creation, payment verification, and refunds.
/// Flow: POS Bill → "Pay Online" → CreateOrder → Razorpay Checkout (browser) → VerifyPayment → done.
/// </summary>
[Authorize(Roles = "Admin,Manager,Cashier")]
[EnableRateLimiting("Pos")]
public class PaymentGatewayController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly RazorpayService _razorpay;
    private readonly PosBillingService _pos;
    private readonly ILogger<PaymentGatewayController> _log;

    public PaymentGatewayController(
        ApplicationDbContext db,
        RazorpayService razorpay,
        PosBillingService pos,
        ILogger<PaymentGatewayController> log)
    {
        _db = db;
        _razorpay = razorpay;
        _pos = pos;
        _log = log;
    }

    private Guid? GetCompanyId()
    {
        var raw = User.FindFirstValue("CompanyId");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    // ────────────────────────────────────────────────────────
    // Step 1: Create Razorpay Order (AJAX — called from POS Bill)
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a Razorpay order for the remaining amount on a POS bill.
    /// Returns order ID + key ID for the Razorpay Checkout widget.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderReq req)
    {
        try
        {
            var companyId = GetCompanyId();
            if (!companyId.HasValue)
                return Json(new { success = false, message = "Access denied." });

            var bill = await _db.PosBills
                .Include(b => b.Payments)
                .Include(b => b.Customer)
                .Include(b => b.Store)
                .FirstOrDefaultAsync(b => b.PosBillId == req.BillId);

            if (bill is null)
                return Json(new { success = false, message = "Bill not found." });
            if (bill.CompanyId != companyId.Value)
                return Json(new { success = false, message = "Access denied." });

            if (bill.Status != 1)
                return Json(new { success = false, message = "Bill is not open." });

            // Calculate remaining amount
            var totalPaid = bill.Payments.Where(p => !p.IsRefund).Sum(p => p.Amount);
            var remaining = bill.GrandTotal - totalPaid;

            // Use specific amount if provided, otherwise pay remaining
            var amount = req.Amount > 0 ? req.Amount : remaining;

            if (amount <= 0)
                return Json(new { success = false, message = "No amount due." });

            if (amount > remaining)
                return Json(new { success = false, message = $"Amount ₹{amount:N2} exceeds remaining ₹{remaining:N2}." });

            // Create Razorpay order
            var order = await _razorpay.CreateOrderAsync(
                amountInRupees: amount,
                receiptId: bill.BillNo,
                notes: $"POS Bill {bill.BillNo} — {bill.Store?.Name}");

            if (order is null)
                return Json(new { success = false, message = "Failed to create payment order. Please try again." });

            _log.LogInformation("Razorpay order {OrderId} created for Bill {BillNo}, Amount ₹{Amount}",
                order.Id, bill.BillNo, amount);

            return Json(new
            {
                success = true,
                orderId = order.Id,
                amount = order.Amount, // in paise
                currency = order.Currency,
                keyId = await _razorpay.GetPublicKeyAsync(),
                billNo = bill.BillNo,
                storeName = bill.Store?.Name ?? "",
                customerName = bill.Customer?.Name ?? "Walk-in Customer",
                customerEmail = bill.Customer?.Email ?? "",
                customerPhone = bill.Customer?.Phone ?? ""
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error creating Razorpay order for Bill {BillId}", req.BillId);
            return Json(new { success = false, message = "Payment gateway error. Please try again." });
        }
    }

    // ────────────────────────────────────────────────────────
    // Step 2: Verify Payment (AJAX — called after Razorpay Checkout success)
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// Verify the Razorpay payment signature, then record the payment on the POS bill.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentReq req)
    {
        try
        {
            var companyId = GetCompanyId();
            if (!companyId.HasValue)
                return Json(new { success = false, message = "Access denied." });

            // 1. Verify signature
            var isValid = await _razorpay.VerifyPaymentSignatureAsync(req.OrderId, req.PaymentId, req.Signature);
            if (!isValid)
            {
                _log.LogWarning("Payment signature verification FAILED. OrderId={OrderId}, PaymentId={PaymentId}",
                    req.OrderId, req.PaymentId);
                return Json(new { success = false, message = "Payment verification failed. Signature mismatch." });
            }

            // 2. Fetch payment details from Razorpay
            var paymentDetails = await _razorpay.FetchPaymentAsync(req.PaymentId);

            // 3. Calculate amount (Razorpay returns paise)
            var amountInRupees = paymentDetails != null
                ? paymentDetails.Amount / 100m
                : req.AmountPaise / 100m;

            // 4. Determine payment method string
            var method = paymentDetails?.Method?.ToLowerInvariant() switch
            {
                "upi" => "UPI",
                "card" => "Card",
                "netbanking" => "NetBanking",
                "wallet" => "Wallet",
                _ => "Online"
            };

            // 5. Build reference string
            var reference = paymentDetails?.Method?.ToLowerInvariant() switch
            {
                "upi" => $"UPI: {paymentDetails?.Vpa ?? req.PaymentId}",
                "card" => $"Card via Razorpay ({req.PaymentId})",
                "netbanking" => $"NetBanking: {paymentDetails?.Bank ?? req.PaymentId}",
                "wallet" => $"Wallet: {paymentDetails?.Wallet ?? req.PaymentId}",
                _ => $"Razorpay: {req.PaymentId}"
            };

            // 6. Record payment in our database
            var bill = await _db.PosBills.Include(b => b.Payments)
                .FirstOrDefaultAsync(b => b.PosBillId == req.BillId);

            if (bill is null)
                return Json(new { success = false, message = "Bill not found." });
            if (bill.CompanyId != companyId.Value)
                return Json(new { success = false, message = "Access denied." });

            if (bill.Status != 1)
                return Json(new { success = false, message = "Bill is not open." });

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                PosBillId = req.BillId,
                Method = method,
                Amount = amountInRupees,
                Reference = reference,
                PaidAtUtc = DateTime.UtcNow,
                IsRefund = false,
                IsGatewayPayment = true,
                RazorpayOrderId = req.OrderId,
                RazorpayPaymentId = req.PaymentId,
                RazorpaySignature = req.Signature,
                GatewayMethod = paymentDetails?.Method,
                GatewayVpa = paymentDetails?.Vpa
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            _log.LogInformation(
                "Razorpay payment recorded: PaymentId={RzpPaymentId}, Method={Method}, ₹{Amount} for Bill {BillNo}",
                req.PaymentId, method, amountInRupees, bill.BillNo);

            // Return updated bill summary
            var totalPaid = bill.Payments.Where(p => !p.IsRefund).Sum(p => p.Amount) + amountInRupees;
            var remaining = bill.GrandTotal - totalPaid;

            return Json(new
            {
                success = true,
                message = $"₹{amountInRupees:N2} received via {method}",
                paymentId = payment.PaymentId,
                method,
                amount = amountInRupees,
                reference,
                totalPaid,
                remaining
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error verifying Razorpay payment. OrderId={OrderId}", req.OrderId);
            return Json(new { success = false, message = "Payment verification error. Contact admin." });
        }
    }

    // ────────────────────────────────────────────────────────
    // Refund (Admin/Manager only — from payment details)
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// Process a Razorpay refund for a gateway payment.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Refund([FromBody] RefundReq req)
    {
        try
        {
            var companyId = GetCompanyId();
            if (!companyId.HasValue)
                return Json(new { success = false, message = "Access denied." });

            var payment = await _db.Payments
                .Include(p => p.PosBill)
                .FirstOrDefaultAsync(p => p.PaymentId == req.PaymentId);
            if (payment is null)
                return Json(new { success = false, message = "Payment not found." });
            if (payment.PosBill?.CompanyId != companyId.Value)
                return Json(new { success = false, message = "Access denied." });

            if (!payment.IsGatewayPayment || string.IsNullOrEmpty(payment.RazorpayPaymentId))
                return Json(new { success = false, message = "This is not a gateway payment." });

            if (!string.IsNullOrEmpty(payment.GatewayRefundId))
                return Json(new { success = false, message = "This payment has already been refunded." });

            var amount = req.Amount > 0 ? req.Amount : payment.Amount;

            var refund = await _razorpay.RefundAsync(
                payment.RazorpayPaymentId, amount, $"Refund for POS Bill payment");

            if (refund is null)
                return Json(new { success = false, message = "Refund request failed. Please try again." });

            // Update the original payment with refund reference
            payment.GatewayRefundId = refund.Id;
            await _db.SaveChangesAsync();

            _log.LogInformation("Razorpay refund {RefundId} processed for PaymentId={PaymentId}, ₹{Amount}",
                refund.Id, payment.RazorpayPaymentId, amount);

            return Json(new
            {
                success = true,
                refundId = refund.Id,
                amount,
                message = $"₹{amount:N2} refund initiated. RefundId: {refund.Id}"
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error processing refund for PaymentId {PaymentId}", req.PaymentId);
            return Json(new { success = false, message = "Refund error. Contact admin." });
        }
    }

    // ── Request DTOs ──

    public class CreateOrderReq
    {
        public Guid BillId { get; set; }
        public decimal Amount { get; set; } // 0 = pay full remaining
    }

    public class VerifyPaymentReq
    {
        public Guid BillId { get; set; }
        public string OrderId { get; set; } = "";
        public string PaymentId { get; set; } = "";
        public string Signature { get; set; } = "";
        public long AmountPaise { get; set; }
    }

    public class RefundReq
    {
        public Guid PaymentId { get; set; }
        public decimal Amount { get; set; } // 0 = full refund
    }
}
