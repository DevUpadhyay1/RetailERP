namespace RetailERP.Data.Entities;

/// <summary>Sprint 4 – Marker interface for entities that belong to a tenant (Company).</summary>
public interface ITenantEntity
{
    Guid? CompanyId { get; set; }
}
