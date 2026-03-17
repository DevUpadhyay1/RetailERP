using System.ComponentModel.DataAnnotations;
using RetailERP.Data.Identity;

namespace RetailERP.Data.Entities;

/// <summary>Sprint 3 – Persists each user's customised dashboard widget layout as JSON.</summary>
public class UserDashboardLayout
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Serialised JSON array of { widgetId, x, y, w, h, visible }.
    /// </summary>
    [Required]
    public string LayoutJson { get; set; } = "[]";

    public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
}
