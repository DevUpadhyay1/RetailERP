using System.ComponentModel.DataAnnotations;

namespace RetailERP.Data.Entities;

/// <summary>Sprint 5: Stores JWT refresh tokens per user.</summary>
public class RefreshToken
{
    [Key]
    public Guid RefreshTokenId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required, StringLength(200)]
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; }
}
