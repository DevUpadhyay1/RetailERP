using System.ComponentModel.DataAnnotations;

namespace RetailERP.Data.Entities;

public class Customer
{
    [Key]
    public Guid CustomerId { get; set; } = Guid.NewGuid();

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(200)]
    public string? Email { get; set; }
}