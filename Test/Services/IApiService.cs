using Test.Models;

namespace Test.Services;

public interface IApiService
{
    Task<List<CalendarInfo>> GetCalendarsAsync();
    Task<List<EventItem>> GetEventsAsync(DateTime? start = null, DateTime? end = null, int? calendarId = null);
    Task<(int imported, int skipped)> SyncImportAsync(int calendarId, List<RawEvent> events);
    Task DeleteAllEventsAsync();
}