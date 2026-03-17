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
}