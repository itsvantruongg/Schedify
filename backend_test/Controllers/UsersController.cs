using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using backend_test.Data;

namespace backend_test.Controllers;

[Authorize]
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public UsersController(AppDbContext db) => _db = db;

    // GET api/users/me
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var user = await _db.Users.FindAsync(UserId);
        if (user == null) return NotFound();
        return Ok(new
        {
            id = user.Id,
            username = user.Username,
            email = user.Email,
            displayName = user.DisplayName,
            avatarUrl = user.AvatarUrl,
            createdAt = user.CreatedAt
        });
    }

    // PUT api/users/me
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileDto dto)
    {
        var user = await _db.Users.FindAsync(UserId);
        if (user == null) return NotFound();

        if (!string.IsNullOrEmpty(dto.DisplayName))
            user.DisplayName = dto.DisplayName;
        if (!string.IsNullOrEmpty(dto.AvatarUrl))
            user.AvatarUrl = dto.AvatarUrl;

        await _db.SaveChangesAsync();
        return Ok(new
        {
            id = user.Id,
            username = user.Username,
            email = user.Email,
            displayName = user.DisplayName,
            avatarUrl = user.AvatarUrl
        });
    }
}

public class UpdateProfileDto
{
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
}