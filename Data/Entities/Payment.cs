using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>
/// Phase 4: Payment record. Linked to a POS bill (or optionally an Invoice).
/// Supports split payments — multiple payments can sum to bill total.
/// Method: Cash, Card, UPI, Other.
/// </summary>
public class Payment : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid PaymentId { get; set; } = Guid.NewGuid();

    // Linked to POS bill (primary) or Invoice (optional)
    public Guid? PosBillId { get; set; }
    public PosBill? PosBill { get; set; }

    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    [Required, StringLength(20)]
    public string Method { get; set; } = "Cash"; // Cash / Card / UPI / Other

    [Precision(18, 2)]
    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    [StringLength(100)]
    public string? Reference { get; set; } // Card last-4, UPI txn id, etc.

    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;

    // If this is a refund payment (negative effectively)
    public bool IsRefund { get; set; }

    // Sprint 2: Razorpay payment gateway fields
    [StringLength(50)]
    public string? RazorpayOrderId { get; set; }

    [StringLength(50)]
    public string? RazorpayPaymentId { get; set; }

    [StringLength(200)]
    public string? RazorpaySignature { get; set; }

    [StringLength(20)]
    public string? GatewayMethod { get; set; }  // upi / card / netbanking / wallet

    [StringLength(100)]
    public string? GatewayVpa { get; set; }     // UPI VPA (e.g., user@upi)

    [StringLength(50)]
    public string? GatewayRefundId { get; set; }

    public bool IsGatewayPayment { get; set; }  // true if paid via Razorpay

    public Guid? PosReturnId { get; set; }
    public PosReturn? PosReturn { get; set; }

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
