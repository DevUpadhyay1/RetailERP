using System.ComponentModel.DataAnnotations;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

public class Unit : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid UnitId { get; set; } = Guid.NewGuid();

    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Symbol { get; set; }

    public bool IsActive { get; set; } = true;

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
