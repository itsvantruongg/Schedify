namespace Test.Models
{
    // ── EventItem — thay thế ScheduleEvent ───────────────────
    public class EventItem
    {
        public int Id { get; set; }
        public int CalendarId { get; set; }
        public string CalendarName { get; set; } = string.Empty;
        public string CalendarColor { get; set; } = "#3498DB";
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Type { get; set; } = "class";
        public string Period { get; set; } = string.Empty;
        public string Teacher { get; set; } = string.Empty;
        public int HocKy { get; set; } = 1;
        public string DateKey => StartTime.ToString("dd/MM/yyyy");

        public override string ToString()
            => $"{DateKey} - {Title} ({Period}) - {Location}";
    }

    // ── RawEvent — dữ liệu thô từ WebScraper ─────────────────
    public class RawEvent
    {
        public string Date { get; set; } = string.Empty; // "dd/MM/yyyy"
        public string Subject { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public string Teacher { get; set; } = string.Empty;
        public string Type { get; set; } = "class";
        public int HocKy { get; set; } = 1;
        public string? Notes { get; set; }

        public override string ToString()
            => $"{Date} - {Subject} ({Period}) - {Room}";
    }

    // ── CalendarInfo — thông tin calendar ─────────────────────
    public class CalendarInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#3498DB";
        public int EventCount { get; set; }
    }

    // ── DotHocOption — giữ nguyên (dùng trong WebScraper) ────
    public class DotHocOption
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    // ── PeriodTimes — giữ nguyên ──────────────────────────────
    public static class PeriodTimes
    {
        public static readonly Dictionary<int, (string Start, string End)> Mapping = new()
        {
            {  1, ("06:55", "07:40") },
            {  2, ("07:45", "08:30") },
            {  3, ("08:35", "09:20") },
            {  4, ("09:30", "10:15") },
            {  5, ("10:20", "11:05") },
            {  6, ("11:10", "11:55") },
            {  7, ("12:05", "12:50") },
            {  8, ("12:55", "13:40") },
            {  9, ("13:45", "14:30") },
            { 10, ("14:40", "15:25") },
            { 11, ("15:30", "16:15") },
            { 12, ("16:20", "17:05") }
        };

        public static ((int Hour, int Minute) Start, (int Hour, int Minute) End)
            GetPeriodTime(int periodNum)
        {
            if (Mapping.TryGetValue(periodNum, out var times))
            {
                var s = times.Start.Split(':');
                var e = times.End.Split(':');
                return (
                    (int.Parse(s[0]), int.Parse(s[1])),
                    (int.Parse(e[0]), int.Parse(e[1]))
                );
            }
            return ((6 + periodNum, 0), (6 + periodNum + 1, 0));
        }

        /// <summary>
        /// Parse "Tiết 4-6" → StartTime và EndTime chính xác
        /// </summary>
        public static (DateTime Start, DateTime End) ParsePeriodToDateTime(
        string periodStr, DateTime date)
        {
            if (string.IsNullOrWhiteSpace(periodStr))
                return (date.AddHours(7), date.AddHours(9));

            try
            {
                // ── Format lịch thi: "Chiều (15H00-17H00)" hoặc chỉ "15H00-17H00" ──
                var timeMatch = System.Text.RegularExpressions.Regex.Match(
                    periodStr,
                    @"(\d{1,2})H(\d{2})\s*-\s*(\d{1,2})H(\d{2})",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (timeMatch.Success)
                {
                    int startH = int.Parse(timeMatch.Groups[1].Value);
                    int startM = int.Parse(timeMatch.Groups[2].Value);
                    int endH = int.Parse(timeMatch.Groups[3].Value);
                    int endM = int.Parse(timeMatch.Groups[4].Value);

                    return (
                        date.Date.AddHours(startH).AddMinutes(startM),
                        date.Date.AddHours(endH).AddMinutes(endM)
                    );
                }

                // ── Format lịch học: "Tiết 4-6" ──
                var nums = periodStr
                    .Replace("Tiết", "").Replace("tiết", "").Trim()
                    .Split('-')
                    .Select(p => int.Parse(p.Trim()))
                    .ToArray();

                if (nums.Length < 1)
                    return (date.AddHours(7), date.AddHours(9));

                var startPeriod = nums[0];
                var endPeriod = nums.Length > 1 ? nums[^1] : nums[0];

                var (s, _) = GetPeriodTime(startPeriod);
                var (_, e) = GetPeriodTime(endPeriod);

                return (
                    date.Date.AddHours(s.Hour).AddMinutes(s.Minute),
                    date.Date.AddHours(e.Hour).AddMinutes(e.Minute)
                );
            }
            catch
            {
                return (date.AddHours(7), date.AddHours(9));
            }
        }
    }
}