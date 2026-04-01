using System.Globalization;
using Test.Models;
using HtmlAgilityPack;

namespace Test.Services
{
    public class HtmlParser
    {
        private readonly Action<string> _logCallback;

        public HtmlParser(Action<string> logCallback)
        {
            _logCallback = logCallback;
        }

        private void Log(string message) => _logCallback?.Invoke(message);

        public static string NormalizeDate(string text)
        {
            text = text.Trim();
            string[] formats = { "dd/MM/yyyy", "dd/MM/yy", "yyyy-MM-dd" };
            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime date))
                    return date.ToString("dd/MM/yyyy");
            }
            throw new FormatException($"Invalid date format: {text}");
        }

        public List<RawEvent> ExtractScheduleFromHtml(string htmlContent, int hocKy = 1)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            var table = doc.DocumentNode.SelectSingleNode("//table");
            if (table == null)
            {
                Log("⚠️ Không tìm thấy table");
                return new List<RawEvent>();
            }

            var rows = table.SelectNodes(".//tr");
            if (rows == null || rows.Count <= 1)
                return new List<RawEvent>();

            var allRows = new List<List<CellData>>();

            foreach (var row in rows)
            {
                if (row.SelectNodes(".//th") != null) continue;
                var tds = row.SelectNodes(".//td");
                if (tds == null || tds.Count == 0) continue;

                var rowData = new List<CellData>();
                foreach (var td in tds)
                {
                    rowData.Add(new CellData
                    {
                        Text = td.InnerText.Trim(),
                        Rowspan = td.GetAttributeValue("rowspan", 1),
                        Colspan = td.GetAttributeValue("colspan", 1)
                    });
                }
                allRows.Add(rowData);
            }

            var expandedRows = ExpandRows(allRows);
            var data = new List<RawEvent>();

            foreach (var row in expandedRows)
            {
                if (row.Count < 9) continue;

                var subject = row[1].Trim();
                var className = row[3].Trim();
                var timeRange = row[4].Trim();
                var thu = row[5].Trim();
                var tiet = row[6].Trim();
                var phong = row[7].Trim();
                var teacher = row[8].Trim();

                if (string.IsNullOrEmpty(timeRange) || string.IsNullOrEmpty(thu) ||
                    string.IsNullOrEmpty(tiet) || string.IsNullOrEmpty(subject))
                    continue;

                var sessions = AddClassSessions(subject, className, teacher,
                    timeRange, thu, tiet, phong, hocKy);
                data.AddRange(sessions);

                if (sessions.Count > 0)
                {
                    var shortSubject = subject.Length > 40
                        ? subject.Substring(0, 40) : subject;
                    Log($"  ✅ {shortSubject}: {timeRange} | Thứ {thu} | Tiết {tiet} = {sessions.Count} buổi");
                }
            }

            return data;
        }

        private List<List<string>> ExpandRows(List<List<CellData>> allRows)
        {
            var expandedRows = new List<List<string>>();
            var spanningTracker = new Dictionary<(int Row, int Col), (string Value, int EndRow)>();

            for (int rowIdx = 0; rowIdx < allRows.Count; rowIdx++)
            {
                var rowData = allRows[rowIdx];
                var expandedRow = new List<string>();
                int colPosition = 0;
                int cellIdx = 0;

                while (colPosition < 9)
                {
                    if (spanningTracker.TryGetValue((rowIdx, colPosition), out var spanInfo))
                    {
                        expandedRow.Add(spanInfo.Value);
                        if (rowIdx + 1 <= spanInfo.EndRow)
                            spanningTracker[(rowIdx + 1, colPosition)] = spanInfo;
                        colPosition++;
                        continue;
                    }

                    if (cellIdx < rowData.Count)
                    {
                        var cell = rowData[cellIdx];
                        expandedRow.Add(cell.Text);

                        if (cell.Rowspan > 1)
                        {
                            int endRow = rowIdx + cell.Rowspan - 1;
                            for (int futureRow = rowIdx + 1; futureRow <= endRow; futureRow++)
                                spanningTracker[(futureRow, colPosition)] = (cell.Text, endRow);
                        }

                        cellIdx++;
                        colPosition += cell.Colspan;
                    }
                    else
                    {
                        expandedRow.Add("");
                        colPosition++;
                    }
                }

                while (expandedRow.Count < 9) expandedRow.Add("");
                expandedRows.Add(expandedRow.Take(9).ToList());
            }

            return expandedRows;
        }

        private List<RawEvent> AddClassSessions(string subjectName, string className,
            string teacher, string timeRange, string thu, string tiet, string phong, int hocKy)
        {
            var sessions = new List<RawEvent>();

            try
            {
                if (!timeRange.Contains("-")) return sessions;

                var parts = timeRange.Split('-');
                if (parts.Length != 2) return sessions;

                var startDate = DateTime.ParseExact(
                    NormalizeDate(parts[0].Trim()), "dd/MM/yyyy", CultureInfo.InvariantCulture);
                var endDate = DateTime.ParseExact(
                    NormalizeDate(parts[1].Trim()), "dd/MM/yyyy", CultureInfo.InvariantCulture);

                var thuMap = new Dictionary<string, DayOfWeek>
                {
                    { "2",  DayOfWeek.Monday    },
                    { "3",  DayOfWeek.Tuesday   },
                    { "4",  DayOfWeek.Wednesday },
                    { "5",  DayOfWeek.Thursday  },
                    { "6",  DayOfWeek.Friday    },
                    { "7",  DayOfWeek.Saturday  },
                    { "CN", DayOfWeek.Sunday    }
                };

                if (!thuMap.TryGetValue(thu, out DayOfWeek targetWeekday))
                {
                    Log($"⚠️ Thứ không hợp lệ: {thu}");
                    return sessions;
                }

                var currentDate = startDate;
                while (currentDate.DayOfWeek != targetWeekday && currentDate <= endDate)
                    currentDate = currentDate.AddDays(1);

                while (currentDate <= endDate)
                {
                    sessions.Add(new RawEvent
                    {
                        Date = currentDate.ToString("dd/MM/yyyy"),
                        Subject = $"📖 {subjectName} ({className})",
                        Period = $"Tiết {tiet}",
                        Room = string.IsNullOrEmpty(phong) ? "N/A" : phong,
                        Teacher = teacher,
                        Type = "class",
                        HocKy = hocKy
                    });
                    currentDate = currentDate.AddDays(7);
                }

                return sessions;
            }
            catch (Exception ex)
            {
                Log($"⚠️ Lỗi parse: {ex.Message} - {timeRange}, Thứ {thu}");
                return sessions;
            }
        }

        public List<RawEvent> ExtractExamFromHtml(string htmlContent, int? hocKy = null)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            var table = doc.DocumentNode.SelectSingleNode(".//table");
            if (table == null)
            {
                Log("⚠️ Không tìm thấy table");
                return new List<RawEvent>();
            }

            var rows = table.SelectNodes(".//tr");
            var data = new List<RawEvent>();
            if (rows == null) return data;

            foreach (var row in rows)
            {
                if (row.SelectNodes(".//th") != null) continue;
                var tds = row.SelectNodes(".//td");
                if (tds == null || tds.Count < 11) continue;

                try
                {
                    var examDateStr = NormalizeDate(tds[4].InnerText);
                    var examDate = DateTime.ParseExact(examDateStr, "dd/MM/yyyy",
                                          CultureInfo.InvariantCulture);
                    var subjectName = tds[2].InnerText.Trim();
                    var caThi = tds[5].InnerText.Trim();
                    var gioThi = tds[6].InnerText.Trim();
                    var room = tds[10].InnerText.Trim().Replace("\n", " - ");

                    int examHocKy = hocKy ?? (examDate.Month >= 8 ? 1 : 2);
                    string period = !string.IsNullOrEmpty(gioThi)
                        ? (string.IsNullOrEmpty(caThi) ? gioThi : $"{caThi} ({gioThi})")
                        : caThi;

                    data.Add(new RawEvent
                    {
                        Date = examDateStr,
                        Subject = $"📝 THI: {subjectName}",
                        Period = period,
                        Room = room,
                        Teacher = "Lịch thi",
                        Type = "exam",
                        HocKy = examHocKy
                    });

                    var shortSubject = subjectName.Length > 30
                        ? subjectName.Substring(0, 30) : subjectName;
                    Log($"  ✅ Thi {shortSubject}: {examDateStr} - {period}");
                }
                catch (Exception ex)
                {
                    Log($"⚠️ Lỗi parse lịch thi: {ex.Message}");
                }
            }

            return data;
        }

        private class CellData
        {
            public string Text { get; set; } = "";
            public int Rowspan { get; set; } = 1;
            public int Colspan { get; set; } = 1;
        }
    }
}