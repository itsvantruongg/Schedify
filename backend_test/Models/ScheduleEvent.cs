namespace backend_test.Models;

public class ScheduleEvent
{
    public int Id { get; set; }
    public string Date { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Teacher { get; set; } = string.Empty;
    public string Type { get; set; } = "class";
    public int HocKy { get; set; } = 1;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // FK → User
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}