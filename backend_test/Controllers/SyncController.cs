using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using backend_test.Data;
using backend_test.DTOs;
using backend_test.Models;

namespace backend_test.Controllers;

[Authorize]
[ApiController]
[Route("api/sync")]
public class SyncController : ControllerBase
{
    private readonly AppDbContext _db;
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public SyncController(AppDbContext db) => _db = db;

    // GET api/sync/terms — danh sách học kỳ đã có data
    [HttpGet("terms")]
    public async Task<IActionResult> GetTerms()
    {
        var terms = await _db.Events
            .Where(e => e.UserId == UserId)
            .Select(e => new { e.HocKy })
            .Distinct()
            .OrderBy(e => e.HocKy)
            .Select(e => new
            {
                dotValue = e.HocKy,
                name = $"Học kỳ {e.HocKy}"
            })
            .ToListAsync();

        return Ok(terms);
    }

    // POST api/sync/import
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] SyncImportDto dto)
    {
        var calendar = await _db.Calendars
            .FirstOrDefaultAsync(c => c.Id == dto.CalendarId && c.UserId == UserId);
        if (calendar == null)
            return BadRequest(new { message = "Calendar không tồn tại" });

        int imported = 0, skipped = 0;

        foreach (var raw in dto.Events)
        {
            if (!DateTime.TryParseExact(raw.Date, "dd/MM/yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
                continue;

            var sourceId = $"{raw.Date}|{raw.Subject}|{raw.Period}|{raw.Type}";

            var exists = await _db.Events
                .AnyAsync(e => e.UserId == UserId && e.SourceId == sourceId);

            if (exists) { skipped++; continue; }

            _db.Events.Add(new Event
            {
                CalendarId = dto.CalendarId,
                UserId = UserId,
                Title = raw.Subject,
                StartTime = date,
                EndTime = date.AddHours(2),
                Location = raw.Room,
                Type = raw.Type,
                Period = raw.Period,
                Teacher = raw.Teacher,
                HocKy = raw.HocKy,
                SourceId = sourceId,
                Description = string.Empty
            });
            imported++;
        }

        await _db.SaveChangesAsync();

        return Ok(new SyncResultDto
        {
            Imported = imported,
            Skipped = skipped,
            Total = imported + skipped
        });
    }
}