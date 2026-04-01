namespace backend_test.Services;

using backend_test.DTOs;

public class AuthResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public AuthResponseDto? Data { get; set; }
}

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterDto dto);
    Task<AuthResult> LoginAsync(LoginDto dto);
    Task<AuthResult> RefreshTokenAsync(string refreshToken);
    Task<bool> LogoutAsync(string refreshToken);
}