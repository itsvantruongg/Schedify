namespace backend_test.DTOs;

public class RawEventDto
{
    public string Date { get; set; } = string.Empty; // "dd/MM/yyyy"
    public string Subject { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Teacher { get; set; } = string.Empty;
    public string Type { get; set; } = "class";
    public int HocKy { get; set; } = 1;
}

public class SyncImportDto
{
    public int CalendarId { get; set; }
    public List<RawEventDto> Events { get; set; } = new();
}

public class SyncResultDto
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Total { get; set; }
}