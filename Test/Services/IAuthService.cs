using Test.Models;

namespace Test.Services;

public interface IAuthService
{
    // ── Session (offline-safe) ─────────────────────────────────
    Task<bool> HasSessionAsync();

    // ── Auth ──────────────────────────────────────────────────
    Task<(bool Success, string Message, AuthResponse? Data)> RegisterAsync(
        string username, string displayName, string email, string password);

    Task<(bool Success, string Message, AuthResponse? Data)> LoginAsync(
        string username, string password);

    Task<bool> LogoutAsync();

    Task<(bool Success, string Message)> RefreshTokenAsync(string refreshToken);

    Task<string?> GetAccessTokenAsync();
}