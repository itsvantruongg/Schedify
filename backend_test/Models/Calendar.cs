using Microsoft.Extensions.Logging;

namespace backend_test.Models;

public class Calendar
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#3498DB";
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}