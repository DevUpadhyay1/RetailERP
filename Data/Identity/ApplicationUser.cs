using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace RetailERP.Data.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
	[StringLength(100)]
	public string? DisplayName { get; set; }

	public bool IsActive { get; set; } = true;

	// Sprint 4 – Multi-tenant: user belongs to a company
	public Guid? CompanyId { get; set; }

	/// <summary>Optional default for POS: skip store/warehouse pick on "Quick start".</summary>
	public Guid? DefaultPosStoreId { get; set; }

	/// <summary>Warehouse used with <see cref="DefaultPosStoreId"/> for POS quick start.</summary>
	public Guid? DefaultPosWarehouseId { get; set; }
}