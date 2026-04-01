namespace backend_test.DTOs;

public class ScheduleEventDto
{
    public string Date { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Teacher { get; set; } = string.Empty;
    public string Type { get; set; } = "class";
    public int HocKy { get; set; } = 1;
    public string? Notes { get; set; }
}

public class SaveScheduleDto
{
    public List<ScheduleEventDto> Events { get; set; } = new();
    public string SessionId { get; set; } = string.Empty;
}
