using SQLite;
using Test.Models;

namespace Test.Services;

public class LocalCacheService : ILocalCacheService
{
    private SQLiteAsyncConnection? _db;
    private string _currentUserId = "default";
    public string GetCurrentUserId() => _currentUserId;

    [Table("ScheduleCache")]
    public class ScheduleCacheEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public string Teacher { get; set; } = string.Empty;
        public string Type { get; set; } = "class";
        public int HocKy { get; set; } = 1;
        public int CalendarId { get; set; }
        public string CalendarName { get; set; } = string.Empty;
        public string CalendarColor { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    [Table("SyncMeta")]
    public class SyncMeta
    {
        [PrimaryKey]
        public string UserId { get; set; } = string.Empty;
        public string LastSyncUtc { get; set; } = string.Empty;
    }

    private async Task<SQLiteAsyncConnection> GetDbAsync()
    {
        if (_db != null) return _db;
        var path = Path.Combine(FileSystem.AppDataDirectory, "schedule_cache.db");
        _db = new SQLiteAsyncConnection(path);
        await _db.CreateTableAsync<ScheduleCacheEntry>();
        await _db.CreateTableAsync<SyncMeta>();
        return _db;
    }

    public void SetCurrentUser(string userId)
    {
        _currentUserId = string.IsNullOrEmpty(userId) ? "default" : userId;
        System.Diagnostics.Debug.WriteLine($"🔑 Cache userId = {_currentUserId}");
    }

    public async Task SaveScheduleAsync(List<EventItem> events)
    {
        try
        {
            var userId = _currentUserId; // Snapshot tránh race condition
            var db = await GetDbAsync();
            await db.ExecuteAsync(
                "DELETE FROM ScheduleCache WHERE UserId = ?", userId);

            var entries = events.Select(e => new ScheduleCacheEntry
            {
                UserId = userId, // Dùng snapshot
                Date = e.StartTime.ToString("dd/MM/yyyy"),
                Subject = e.Title,
                Period = e.Period,
                Room = e.Location,
                Teacher = e.Teacher,
                Type = e.Type,
                HocKy = e.HocKy,
                CalendarId = e.CalendarId,
                CalendarName = e.CalendarName,
                CalendarColor = e.CalendarColor,
                Notes = e.Description
            }).ToList();

            await db.InsertAllAsync(entries);
            await SetLastSyncTimeAsync(DateTime.Now);

            System.Diagnostics.Debug.WriteLine(
                $"💾 Saved {entries.Count} events for userId={userId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ SaveCache: {ex.Message}");
        }
    }

    public async Task<List<EventItem>> LoadScheduleAsync()
    {
        try
        {
            var db = await GetDbAsync();
            var entries = await db.Table<ScheduleCacheEntry>()
                .Where(e => e.UserId == _currentUserId)
                .ToListAsync();

            return entries.Select(e => new EventItem
            {
                CalendarId = e.CalendarId,
                CalendarName = e.CalendarName,
                CalendarColor = e.CalendarColor,
                Title = e.Subject,
                StartTime = DateTime.ParseExact(e.Date, "dd/MM/yyyy",
                                    System.Globalization.CultureInfo.InvariantCulture),
                EndTime = DateTime.ParseExact(e.Date, "dd/MM/yyyy",
                                    System.Globalization.CultureInfo.InvariantCulture).AddHours(2),
                Location = e.Room,
                Period = e.Period,
                Teacher = e.Teacher,
                Type = e.Type,
                HocKy = e.HocKy,
                Description = e.Notes
            }).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ LoadCache: {ex.Message}");
            return new();
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            var db = await GetDbAsync();
            await db.ExecuteAsync(
                "DELETE FROM ScheduleCache WHERE UserId = ?", _currentUserId);
            await db.ExecuteAsync(
                "DELETE FROM SyncMeta WHERE UserId = ?", _currentUserId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ ClearCache: {ex.Message}");
        }
    }

    public async Task<bool> HasCacheAsync()
    {
        try
        {
            var db = await GetDbAsync();
            var count = await db.Table<ScheduleCacheEntry>()
                .Where(e => e.UserId == _currentUserId).CountAsync();
            return count > 0;
        }
        catch { return false; }
    }

    public async Task<DateTime?> GetLastSyncTimeAsync()
    {
        try
        {
            var db = await GetDbAsync();
            var meta = await db.Table<SyncMeta>()
                .Where(m => m.UserId == _currentUserId)
                .FirstOrDefaultAsync();
            if (meta == null || string.IsNullOrEmpty(meta.LastSyncUtc))
                return null;
            return DateTime.Parse(meta.LastSyncUtc);
        }
        catch { return null; }
    }

    public async Task SetLastSyncTimeAsync(DateTime time)
    {
        try
        {
            var db = await GetDbAsync();
            await db.InsertOrReplaceAsync(new SyncMeta
            {
                UserId = _currentUserId,
                LastSyncUtc = time.ToString("o")
            });
        }
        catch { }
    }
}