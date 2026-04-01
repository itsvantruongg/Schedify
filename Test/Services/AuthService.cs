using System.Net.Http.Json;
using System.Text.Json;
using Test.Models;

namespace Test.Services;

public class AuthService : IAuthService
{
    private readonly HttpClient _http;
    private string? _cachedAccessToken;

    private const string BaseUrl = "https://schedify-backend-cxio.onrender.com/api/auth"; //http: //10.0.2.2:5000/api/auth

    // ── Keys ──────────────────────────────────────────────────
    private const string KeyAccessToken = "access_token";
    private const string KeyRefreshToken = "refresh_token";
    private const string KeyUsername = "username";
    private const string KeyEmail = "email";
    private const string KeyUserId = "user_id";
    private const string KeyDisplayName = "display_name";
    private const string KeySession = "session_marker";   // ← Mới
    private const string KeyLastActive = "last_active";      // ← Mới (sliding expiry)

    public AuthService(HttpClient http) => _http = http;

    // ── Kiểm tra có session không (offline được) ───────────────
    public async Task<bool> HasSessionAsync()
    {
        try
        {
            var marker = await SecureStorage.Default.GetAsync(KeySession);
            if (marker != "true") return false;

            // Sliding expiry: quá 90 ngày không mở app → xóa session
            var lastActiveStr = Preferences.Default.Get(KeyLastActive, "");
            if (!string.IsNullOrEmpty(lastActiveStr)
                && DateTime.TryParse(lastActiveStr, out var lastActive)
                && DateTime.UtcNow - lastActive > TimeSpan.FromDays(90))
            {
                await ClearSessionAsync();
                return false;
            }

            // Reset sliding expiry mỗi lần mở app
            Preferences.Default.Set(KeyLastActive, DateTime.UtcNow.ToString("o"));
            return true;
        }
        catch { return false; }
    }

    // ── Register ──────────────────────────────────────────────
    public async Task<(bool Success, string Message, AuthResponse? Data)> RegisterAsync(
        string username, string displayName, string email, string password)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"{BaseUrl}/register",
                new { username, displayName, email, password });
            return await ParseResponseAsync(res);
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi kết nối: {ex.Message}", null);
        }
    }

    // ── Login ─────────────────────────────────────────────────
    public async Task<(bool Success, string Message, AuthResponse? Data)> LoginAsync(
        string username, string password)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"{BaseUrl}/login",
                new { username, password });

            var (success, message, data) = await ParseResponseAsync(res);

            if (success && data != null)
                await SaveSessionAsync(data);

            return (success, message, data);
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi kết nối: {ex.Message}", null);
        }
    }

    // ── Logout ────────────────────────────────────────────────
    public async Task<bool> LogoutAsync()
    {
        try
        {
            // Chỉ gọi server nếu có mạng — không chờ timeout
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                var refreshToken = await SecureStorage.Default.GetAsync(KeyRefreshToken);
                if (refreshToken != null)
                    await _http.PostAsJsonAsync($"{BaseUrl}/logout", new { refreshToken });
            }
        }
        catch { }
        finally
        {
            await ClearSessionAsync();
        }
        return true;
    }

    // ── Refresh token ─────────────────────────────────────────
    public async Task<(bool Success, string Message)> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"{BaseUrl}/refresh",
                new { refreshToken });

            var json = await res.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var message = doc.RootElement
                .TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "";

            if (!res.IsSuccessStatusCode)
                return (false, message);

            if (doc.RootElement.TryGetProperty("data", out var dataEl))
            {
                var data = JsonSerializer.Deserialize<AuthResponse>(dataEl.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data != null)
                    await SaveSessionAsync(data);
            }

            return (true, message);
        }
        catch (Exception ex)
        {
            // Offline → thất bại im lặng, session vẫn còn
            return (false, $"Lỗi kết nối: {ex.Message}");
        }
    }

    // ── Get access token (memory cache → SecureStorage) ───────
    public async Task<string?> GetAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_cachedAccessToken))
            return _cachedAccessToken;

        var token = await SecureStorage.Default.GetAsync(KeyAccessToken);
        _cachedAccessToken = token;
        return token;
    }

    // ── Private: lưu toàn bộ session sau login/refresh ────────
    private async Task SaveSessionAsync(AuthResponse data)
    {
        _cachedAccessToken = data.AccessToken;

        await SecureStorage.Default.SetAsync(KeyAccessToken, data.AccessToken);
        await SecureStorage.Default.SetAsync(KeyRefreshToken, data.RefreshToken);
        await SecureStorage.Default.SetAsync(KeyUsername, data.Username);
        await SecureStorage.Default.SetAsync(KeyEmail, data.Email);
        await SecureStorage.Default.SetAsync(KeyUserId, data.UserId.ToString());
        await SecureStorage.Default.SetAsync(KeyDisplayName, data.DisplayName ?? "");
        await SecureStorage.Default.SetAsync(KeySession, "true");  // ← session_marker

        // Sliding expiry
        Preferences.Default.Set(KeyLastActive, DateTime.UtcNow.ToString("o"));
    }

    // ── Private: xóa toàn bộ session (logout / expired) ───────
    private async Task ClearSessionAsync()
    {
        _cachedAccessToken = null;
        SecureStorage.Default.Remove(KeyAccessToken);
        SecureStorage.Default.Remove(KeyRefreshToken);
        SecureStorage.Default.Remove(KeyUsername);
        SecureStorage.Default.Remove(KeyEmail);
        SecureStorage.Default.Remove(KeyUserId);
        SecureStorage.Default.Remove(KeyDisplayName);
        SecureStorage.Default.Remove(KeySession);
        Preferences.Default.Remove(KeyLastActive);
        await Task.CompletedTask;
    }

    // ── Helper parse response ──────────────────────────────────
    private static async Task<(bool Success, string Message, AuthResponse? Data)>
        ParseResponseAsync(HttpResponseMessage res)
    {
        var json = await res.Content.ReadAsStringAsync();
        System.Diagnostics.Debug.WriteLine($"📡 Response: {json}");

        var doc = JsonDocument.Parse(json);
        var message = doc.RootElement
            .TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "";

        if (!res.IsSuccessStatusCode)
            return (false, message, null);

        AuthResponse? data = null;
        if (doc.RootElement.TryGetProperty("data", out var dataEl))
        {
            data = JsonSerializer.Deserialize<AuthResponse>(dataEl.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            System.Diagnostics.Debug.WriteLine($"📡 UserId parsed: {data?.UserId}");
        }

        return (true, message, data);
    }
}