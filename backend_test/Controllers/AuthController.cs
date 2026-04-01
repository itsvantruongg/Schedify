using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using backend_test.DTOs;
using backend_test.Services;

namespace backend_test.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
        => _authService = authService;

    // POST api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var result = await _authService.RegisterAsync(dto);
        if (!result.Success) return BadRequest(new { message = result.Message });
        return Ok(new { message = result.Message, data = result.Data });
    }

    // POST api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        if (!result.Success) return Unauthorized(new { message = result.Message });
        return Ok(new { message = result.Message, data = result.Data });
    }

    // POST api/auth/refresh
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto)
    {
        var result = await _authService.RefreshTokenAsync(dto.RefreshToken);
        if (!result.Success) return Unauthorized(new { message = result.Message });
        return Ok(new { message = result.Message, data = result.Data });
    }

    // POST api/auth/logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenDto dto)
    {
        var ok = await _authService.LogoutAsync(dto.RefreshToken);
        if (!ok) return BadRequest(new { message = "Logout thất bại" });
        return Ok(new { message = "Đăng xuất thành công" });
    }

    // GET api/auth/me
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            Id = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Username = User.FindFirstValue(ClaimTypes.Name),
            Email = User.FindFirstValue(ClaimTypes.Email)
        });
    }
}