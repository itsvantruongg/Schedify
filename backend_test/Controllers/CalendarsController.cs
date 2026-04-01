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
[Route("api/calendars")]
public class CalendarsController : ControllerBase
{
    private readonly AppDbContext _db;
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public CalendarsController(AppDbContext db) => _db = db;

    // GET api/calendars
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var calendars = await _db.Calendars
            .Where(c => c.UserId == UserId)
            .Select(c => new CalendarResponseDto
            {
                Id = c.Id,
                Name = c.Name,
                Color = c.Color,
                EventCount = c.Events.Count
            })
            .ToListAsync();
        return Ok(calendars);
    }

    // POST api/calendars
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCalendarDto dto)
    {
        var calendar = new Calendar
        {
            Name = dto.Name,
            Color = dto.Color,
            UserId = UserId
        };
        _db.Calendars.Add(calendar);
        await _db.SaveChangesAsync();
        return Ok(new CalendarResponseDto
        {
            Id = calendar.Id,
            Name = calendar.Name,
            Color = calendar.Color
        });
    }

    // PUT api/calendars/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateCalendarDto dto)
    {
        var cal = await _db.Calendars
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId);
        if (cal == null) return NotFound();
        cal.Name = dto.Name;
        cal.Color = dto.Color;
        await _db.SaveChangesAsync();
        return Ok(new CalendarResponseDto
        {
            Id = cal.Id,
            Name = cal.Name,
            Color = cal.Color
        });
    }

    // DELETE api/calendars/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cal = await _db.Calendars
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId);
        if (cal == null) return NotFound();
        _db.Calendars.Remove(cal);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Đã xóa lịch" });
    }
}