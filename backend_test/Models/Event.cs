namespace backend_test.Models;

public class Event
{
    public int Id { get; set; }
    public int CalendarId { get; set; }
    public Calendar Calendar { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Type { get; set; } = "class"; // class | exam
    public string Period { get; set; } = string.Empty;
    public string Teacher { get; set; } = string.Empty;
    public int HocKy { get; set; } = 1;
    public string SourceId { get; set; } = string.Empty; // dùng để dedup khi sync
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}