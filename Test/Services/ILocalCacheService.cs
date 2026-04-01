using Test.Models;

namespace Test.Services;

public interface ILocalCacheService
{
    void SetCurrentUser(string userId);
    Task SaveScheduleAsync(List<EventItem> events);
    Task<List<EventItem>> LoadScheduleAsync();
    Task ClearAsync();
    Task<bool> HasCacheAsync();
    Task<DateTime?> GetLastSyncTimeAsync();
    Task SetLastSyncTimeAsync(DateTime time);
    string GetCurrentUserId();
}