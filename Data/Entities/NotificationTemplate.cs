using System.ComponentModel.DataAnnotations;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>
/// Sprint 11: Reusable notification templates for SMS, WhatsApp, and Email.
/// Supports variable placeholders: {CustomerName}, {BillNo}, {Amount}, {StoreName}, {Date}, {Points}.
/// </summary>
public class NotificationTemplate : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid NotificationTemplateId { get; set; } = Guid.NewGuid();

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Sms, WhatsApp, Email</summary>
    [Required, StringLength(20)]
    public string Channel { get; set; } = "Email";

    /// <summary>BillReceipt, PaymentConfirmation, LoyaltyUpdate, Promotional, LowStockAlert, Custom</summary>
    [Required, StringLength(30)]
    public string Category { get; set; } = "Custom";

    [StringLength(200)]
    public string? Subject { get; set; }

    [Required]
    public string Body { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public Guid? CompanyId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
