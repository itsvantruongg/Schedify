using Test.Services;
using Test.Pages;
namespace Test;

public partial class AppShell : Shell
{
    public AppShell(IAuthService authService)
    {
        InitializeComponent();
        Routing.RegisterRoute("RegisterPage", typeof(RegisterPage));
        Routing.RegisterRoute("LoginPage", typeof(LoginPage));
        _ = CheckAutoLoginAsync(authService);
    }

    private async Task CheckAutoLoginAsync(IAuthService authService)
    {
        try
        {
            // ✅ Có session → vào app NGAY, không quan tâm token còn hạn không
            // Hoạt động hoàn toàn offline
            if (await authService.HasSessionAsync())
            {
                await GoToAsync("//MainPage");

                // Chạy ngầm: nếu có mạng thì refresh access token mới
                _ = TryRefreshInBackgroundAsync(authService);
                return;
            }

            // Không có session → bắt buộc login
            await GoToAsync("//LoginPage");
        }
        catch
        {
            await GoToAsync("//LoginPage");
        }
        //// Thêm tạm vào AppShell.xaml.cs, trong CheckAutoLoginAsync
        //var session = await SecureStorage.Default.GetAsync("session_marker");
        //var userId = await SecureStorage.Default.GetAsync("user_id");
        //var accessToken = await SecureStorage.Default.GetAsync("access_token");
        //var lastActive = Preferences.Default.Get("last_active", "chưa có");

        //System.Diagnostics.Debug.WriteLine("===== LOCAL STORAGE =====");
        //System.Diagnostics.Debug.WriteLine($"session_marker : {session ?? "null"}");
        //System.Diagnostics.Debug.WriteLine($"user_id        : {userId ?? "null"}");
        //System.Diagnostics.Debug.WriteLine($"last_active    : {lastActive}");
        //System.Diagnostics.Debug.WriteLine($"access_token   : {(accessToken != null ? accessToken[..20] + "..." : "null")}");
        //System.Diagnostics.Debug.WriteLine("=========================");
    }

    private static async Task TryRefreshInBackgroundAsync(IAuthService authService)
    {
        try
        {
            // Kiểm tra access token còn hạn không
            var accessToken = await authService.GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(accessToken) && IsTokenValid(accessToken))
                return; // Còn hạn → không cần làm gì

            // Hết hạn → thử refresh (chỉ thành công khi có mạng)
            var refreshToken = await SecureStorage.Default.GetAsync("refresh_token");
            if (!string.IsNullOrEmpty(refreshToken))
                await authService.RefreshTokenAsync(refreshToken);

            // Nếu offline → thất bại im lặng
            // User vẫn đang xem MainPage bình thường với cache local
            // Khi gọi API thật sự, ApiService sẽ tự xử lý
        }
        catch { /* Offline → bỏ qua */ }
    }

    // Decode JWT locally — không cần mạng
    internal static bool IsTokenValid(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return false;

            var payload = parts[1];
            payload = payload.PadRight(
                payload.Length + (4 - payload.Length % 4) % 4, '=');

            var json = System.Text.Encoding.UTF8.GetString(
                         Convert.FromBase64String(payload));
            var doc = System.Text.Json.JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("exp", out var expEl))
                return false;

            var expTime = DateTimeOffset
                .FromUnixTimeSeconds(expEl.GetInt64()).UtcDateTime;

            // Buffer 1 phút để tránh race condition
            return expTime > DateTime.UtcNow.AddMinutes(1);
        }
        catch { return false; }
    }
}