using System.ComponentModel.DataAnnotations;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>
/// Phase 8: Offline sync log entry. Tracks changes made offline that need to be
/// synced to the central server. Conflict resolution is manual.
/// Status: 1 = Pending, 2 = Synced, 3 = Conflict.
/// Action: Create / Update / Delete.
/// </summary>
public class SyncLog : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid SyncLogId { get; set; } = Guid.NewGuid();

    [Required, StringLength(100)]
    public string DeviceId { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string EntityType { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string EntityId { get; set; } = string.Empty;

    [Required, StringLength(10)]
    public string Action { get; set; } = "Create"; // Create / Update / Delete

    // JSON payload of the entity data at time of offline change
    public string? Payload { get; set; }

    public DateTime QueuedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? SyncedAtUtc { get; set; }

    // 1 = Pending, 2 = Synced, 3 = Conflict
    public byte Status { get; set; } = 1;

    [StringLength(1000)]
    public string? ConflictDetails { get; set; }

    [StringLength(10)]
    public string? Resolution { get; set; } // "Server" or "Client" or null

    public DateTime? ResolvedAtUtc { get; set; }

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
