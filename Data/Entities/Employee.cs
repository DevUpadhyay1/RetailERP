using System.ComponentModel.DataAnnotations;

namespace RetailERP.Data.Entities;

public class Employee
{
    [Key]
    public Guid EmployeeId { get; set; } = Guid.NewGuid();

    [Required, StringLength(20)]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime JoinDate { get; set; }
}