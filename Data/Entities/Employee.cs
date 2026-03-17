using System.ComponentModel.DataAnnotations;

namespace RetailERP.Data.Entities;

public enum EmployeeStatus
{
    Active = 1,
    Inactive = 2,
    OnLeave = 3,
    Terminated = 4
}

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

    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
}