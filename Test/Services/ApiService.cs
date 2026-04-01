using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Test.Models;

namespace Test.Services;

public class ApiService : IApiService
{
    private readonly HttpClient _http;
    private readonly IAuthService _auth;
    private const string Base = "http://10.0.2.2:5000/api";

    private static readonly JsonSerializerOptions Opts =
        new() { PropertyNameCaseInsensitive = true };

    // Tránh nhiều request cùng refresh đồng thời
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);

    public ApiService(HttpClient http, IAuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    // ── Đảm bảo token hợp lệ trước mỗi API call ───────────────
    private async Task<bool> EnsureValidTokenAsync()
    {
        var token = await _auth.GetAccessTokenAsync();

        // Token còn hạn → dùng luôn
        if (!string.IsNullOrEmpty(token) && AppShell.IsTokenValid(token))
        {
            AttachToken(token);
            return true;
        }

        // Hết hạn → refresh (lock tránh race condition)
        await _refreshLock.WaitAsync();
        try
        {
            // Double-check sau khi lấy được lock
            token = await _auth.GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(token) && AppShell.IsTokenValid(token))
            {
                AttachToken(token);
                return true;
            }

            var refreshToken = await SecureStorage.Default.GetAsync("refresh_token");
            if (string.IsNullOrEmpty(refreshToken)) return false;

            var (success, _) = await _auth.RefreshTokenAsync(refreshToken);
            if (!success) return false;

            token = await _auth.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;

            AttachToken(token);
            return true;
        }
        finally { _refreshLock.Release(); }
    }

    private void AttachToken(string token) =>
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    // ── Calendars ─────────────────────────────────────────────
    public async Task<List<CalendarInfo>> GetCalendarsAsync()
    {
        try
        {
            if (!await EnsureValidTokenAsync()) return new();
            return await _http.GetFromJsonAsync<List<CalendarInfo>>(
                $"{Base}/calendars", Opts) ?? new();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetCalendars: {ex.Message}");
            return new();
        }
    }

    // ── Events ────────────────────────────────────────────────
    public async Task<List<EventItem>> GetEventsAsync(
        DateTime? start = null, DateTime? end = null, int? calendarId = null)
    {
        try
        {
            if (!await EnsureValidTokenAsync()) return new();

            var url = $"{Base}/events?";
            if (start.HasValue) url += $"start={start.Value:yyyy-MM-dd}&";
            if (end.HasValue) url += $"end={end.Value:yyyy-MM-dd}&";
            if (calendarId.HasValue) url += $"calendarId={calendarId}&";

            return await _http.GetFromJsonAsync<List<EventItem>>(url, Opts) ?? new();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetEvents: {ex.Message}");
            return new();
        }
    }

    // ── Sync ──────────────────────────────────────────────────
    public async Task<(int imported, int skipped)> SyncImportAsync(
        int calendarId, List<RawEvent> events)
    {
        try
        {
            if (!await EnsureValidTokenAsync()) return (0, 0);

            var res = await _http.PostAsJsonAsync($"{Base}/sync/import", new
            {
                calendarId,
                events = events.Select(e => new
                {
                    e.Date,
                    e.Subject,
                    e.Period,
                    e.Room,
                    e.Teacher,
                    e.Type,
                    e.HocKy
                })
            });

            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"SyncImport error: {err}");
                return (0, 0);
            }

            var json = await res.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(json);
            return (
                result.GetProperty("imported").GetInt32(),
                result.GetProperty("skipped").GetInt32()
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SyncImport: {ex.Message}");
            return (0, 0);
        }
    }

    // ── Delete all ────────────────────────────────────────────
    public async Task DeleteAllEventsAsync()
    {
        try
        {
            if (!await EnsureValidTokenAsync()) return;
            var response = await _http.DeleteAsync($"{Base}/events/all");
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteAllEvents: {ex.Message}");
            throw;
        }
    }
}