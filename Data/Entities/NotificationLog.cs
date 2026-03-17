using System.ComponentModel.DataAnnotations;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>
/// Sprint 11: Log of every notification sent (SMS, WhatsApp, Email).
/// Tracks delivery status for audit and retry.
/// </summary>
public class NotificationLog : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid NotificationLogId { get; set; } = Guid.NewGuid();

    /// <summary>Sms, WhatsApp, Email</summary>
    [Required, StringLength(20)]
    public string Channel { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Recipient { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Subject { get; set; }

    public string Body { get; set; } = string.Empty;

    /// <summary>Queued, Sent, Failed</summary>
    [Required, StringLength(10)]
    public string Status { get; set; } = "Queued";

    [StringLength(500)]
    public string? ErrorMessage { get; set; }

    [StringLength(100)]
    public string? ExternalId { get; set; }

    public Guid? TemplateId { get; set; }
    public NotificationTemplate? Template { get; set; }

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [StringLength(50)]
    public string? RefType { get; set; }

    [StringLength(100)]
    public string? RefId { get; set; }

    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? CompanyId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
