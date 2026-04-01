namespace backend_test.DTOs;

public class EventResponseDto
{
    public int Id { get; set; }
    public int CalendarId { get; set; }
    public string CalendarName { get; set; } = string.Empty;
    public string CalendarColor { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string Teacher { get; set; } = string.Empty;
    public int HocKy { get; set; }
}

public class CreateEventDto
{
    public int CalendarId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Type { get; set; } = "class";
    public string Period { get; set; } = string.Empty;
    public string Teacher { get; set; } = string.Empty;
    public int HocKy { get; set; } = 1;
}