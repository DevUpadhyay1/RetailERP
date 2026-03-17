using System.ComponentModel.DataAnnotations;
using RetailERP.Data.Identity;

namespace RetailERP.Data.Entities;

public class AuditLog
{
    public long Id { get; set; }

    [MaxLength(64)]
    public string Action { get; set; } = "";

    [MaxLength(64)]
    public string EntityType { get; set; } = "";

    [MaxLength(64)]
    public string? EntityId { get; set; }

    public Guid? ActorUserId { get; set; }

    public ApplicationUser? ActorUser { get; set; }

    [MaxLength(256)]
    public string? ActorEmail { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Optional: store extra info (qty change, warehouse, totals, etc.)
    public string? DataJson { get; set; }
}