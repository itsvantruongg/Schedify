using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using backend_test.Data;
using backend_test.DTOs;
using backend_test.Models;

namespace backend_test.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<AuthResult> RegisterAsync(RegisterDto dto)
    {
        if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
            return new AuthResult { Success = false, Message = "Username đã tồn tại" };
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
            return new AuthResult { Success = false, Message = "Email đã tồn tại" };

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            DisplayName = dto.DisplayName ?? dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.Calendars.AddRange(
            new Calendar { Name = "Lịch học", Color = "#27AE60", UserId = user.Id },
            new Calendar { Name = "Lịch thi", Color = "#E67E22", UserId = user.Id }
        );
        await _db.SaveChangesAsync();

        var data = await CreateSessionAsync(user);
        return new AuthResult { Success = true, Message = "Đăng ký thành công", Data = data };
    }

    public async Task<AuthResult> LoginAsync(LoginDto dto)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == dto.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return new AuthResult { Success = false, Message = "Username hoặc Password không đúng" };

        // ── Cleanup: xóa session hết hạn cũ của user này ──────
        await CleanupExpiredSessionsAsync(user.Id);

        var data = await CreateSessionAsync(user);
        return new AuthResult { Success = true, Message = "Đăng nhập thành công", Data = data };
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
    {
        var tokenHash = HashToken(refreshToken);

        // Tìm kể cả token đã revoke để detect reuse
        var session = await _db.Sessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.RefreshToken == tokenHash);

        if (session == null)
            return new AuthResult { Success = false, Message = "Refresh token không hợp lệ" };

        // ── Reuse Detection ────────────────────────────────────
        // Token đã bị revoke mà vẫn dùng → có thể bị đánh cắp
        if (session.IsRevoked)
        {
            // Revoke TOÀN BỘ session của user → kick hacker ra
            var allSessions = await _db.Sessions
                .Where(s => s.UserId == session.UserId && !s.IsRevoked)
                .ToListAsync();
            allSessions.ForEach(s => s.IsRevoked = true);
            await _db.SaveChangesAsync();

            return new AuthResult
            {
                Success = false,
                Message = "Phiên đăng nhập bất thường, vui lòng đăng nhập lại"
            };
        }

        if (session.ExpiresAt <= DateTime.UtcNow)
            return new AuthResult { Success = false, Message = "Refresh token đã hết hạn" };

        // ── Token Rotation: hủy token cũ, cấp token mới ───────
        session.IsRevoked = true;
        await _db.SaveChangesAsync();

        // ── Sliding Expiry: mỗi lần refresh → reset thời hạn ──
        var data = await CreateSessionAsync(session.User, slidingReset: true);
        return new AuthResult { Success = true, Message = "Làm mới token thành công", Data = data };
    }

    public async Task<bool> LogoutAsync(string refreshToken)
    {
        var tokenHash = HashToken(refreshToken);
        var session = await _db.Sessions
            .FirstOrDefaultAsync(s => s.RefreshToken == tokenHash && !s.IsRevoked);
        if (session == null) return false;
        session.IsRevoked = true;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Private helpers ────────────────────────────────────────

    private async Task<AuthResponseDto> CreateSessionAsync(User user, bool slidingReset = false)
    {
        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();    // raw token → trả về client
        var tokenHash = HashToken(refreshToken);   // hash → lưu DB

        var expiryDays = int.Parse(_config["JwtSettings:RefreshTokenExpiryDays"]!);

        _db.Sessions.Add(new Session
        {
            RefreshToken = tokenHash,                 // ✅ Lưu hash, không lưu raw
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            UserId = user.Id
        });
        await _db.SaveChangesAsync();

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,              // ✅ Trả raw token về client
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            UserId = user.Id
        };
    }

    // ── SHA-256 hash refresh token trước khi lưu DB ───────────
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    private string GenerateAccessToken(User user)
    {
        var jwt = _config.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwt["SecretKey"]!));
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name,           user.Username),
            new Claim(ClaimTypes.Email,          user.Email)
        };
        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                                    double.Parse(jwt["AccessTokenExpiryMinutes"]!)),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    // ── Xóa session hết hạn — chạy mỗi khi login ─────────────
    private async Task CleanupExpiredSessionsAsync(int userId)
    {
        var expired = await _db.Sessions
            .Where(s => s.UserId == userId
                     && (s.ExpiresAt <= DateTime.UtcNow || s.IsRevoked))
            .ToListAsync();
        if (expired.Any())
        {
            _db.Sessions.RemoveRange(expired);
            await _db.SaveChangesAsync();
        }
    }
}