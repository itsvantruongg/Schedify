using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using backend_test.Data;
using backend_test.DTOs;
using backend_test.Models;

namespace backend_test.Controllers;

[Authorize]
[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly AppDbContext _db;
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public EventsController(AppDbContext db) => _db = db;

    // GET api/events
    [HttpGet]
    public async Task<IActionResult> GetEvents(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        [FromQuery] int? calendarId)
    {
        var query = _db.Events
            .Include(e => e.Calendar)
            .Where(e => e.UserId == UserId);

        if (start.HasValue) query = query.Where(e => e.StartTime >= start.Value);
        if (end.HasValue) query = query.Where(e => e.StartTime <= end.Value);
        if (calendarId.HasValue) query = query.Where(e => e.CalendarId == calendarId.Value);

        var events = await query
            .OrderBy(e => e.StartTime)
            .Select(e => new EventResponseDto
            {
                Id = e.Id,
                CalendarId = e.CalendarId,
                CalendarName = e.Calendar.Name,
                CalendarColor = e.Calendar.Color,
                Title = e.Title,
                Description = e.Description,
                StartTime = e.StartTime,
                EndTime = e.EndTime,
                Location = e.Location,
                Type = e.Type,
                Period = e.Period,
                Teacher = e.Teacher,
                HocKy = e.HocKy
            })
            .ToListAsync();

        return Ok(events);
    }

    // POST api/events
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEventDto dto)
    {
        var calendar = await _db.Calendars
            .FirstOrDefaultAsync(c => c.Id == dto.CalendarId && c.UserId == UserId);
        if (calendar == null)
            return BadRequest(new { message = "Calendar không tồn tại" });

        var ev = new Event
        {
            CalendarId = dto.CalendarId,
            UserId = UserId,
            Title = dto.Title,
            Description = dto.Description,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Location = dto.Location,
            Type = dto.Type,
            Period = dto.Period,
            Teacher = dto.Teacher,
            HocKy = dto.HocKy,
            SourceId = string.Empty
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        return Ok(new EventResponseDto
        {
            Id = ev.Id,
            CalendarId = ev.CalendarId,
            CalendarName = calendar.Name,
            CalendarColor = calendar.Color,
            Title = ev.Title,
            Description = ev.Description,
            StartTime = ev.StartTime,
            EndTime = ev.EndTime,
            Location = ev.Location,
            Type = ev.Type,
            Period = ev.Period,
            Teacher = ev.Teacher,
            HocKy = ev.HocKy
        });
    }

    // PUT api/events/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateEventDto dto)
    {
        var ev = await _db.Events
            .Include(e => e.Calendar)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == UserId);
        if (ev == null) return NotFound();

        ev.Title = dto.Title;
        ev.Description = dto.Description;
        ev.StartTime = dto.StartTime;
        ev.EndTime = dto.EndTime;
        ev.Location = dto.Location;
        ev.Type = dto.Type;
        ev.Period = dto.Period;
        ev.Teacher = dto.Teacher;
        ev.HocKy = dto.HocKy;
        ev.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new EventResponseDto
        {
            Id = ev.Id,
            CalendarId = ev.CalendarId,
            CalendarName = ev.Calendar.Name,
            CalendarColor = ev.Calendar.Color,
            Title = ev.Title,
            Description = ev.Description,
            StartTime = ev.StartTime,
            EndTime = ev.EndTime,
            Location = ev.Location,
            Type = ev.Type,
            Period = ev.Period,
            Teacher = ev.Teacher,
            HocKy = ev.HocKy
        });
    }

    // DELETE api/events/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == UserId);
        if (ev == null) return NotFound();
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Đã xóa sự kiện" });
    }
    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAllEvents()
    {
        var events = await _db.Events
            .Where(e => e.UserId == UserId)
            .ToListAsync();
        _db.Events.RemoveRange(events);
        await _db.SaveChangesAsync();
        return Ok(new { deleted = events.Count });
    }
}