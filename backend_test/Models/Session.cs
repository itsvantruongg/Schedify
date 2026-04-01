using System.ComponentModel.DataAnnotations;

namespace backend_test.Models;

public class Session
{
    public int Id { get; set; }

    [Required]
    public string RefreshToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; } = false;

    // Foreign key
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}