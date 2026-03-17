namespace RetailERP.Data.Auditing;

public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; set; }
    DateTime UpdatedAtUtc { get; set; }

    Guid? CreatedByUserId { get; set; }
    Guid? UpdatedByUserId { get; set; }
}
