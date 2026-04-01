using System.Text.Json;
using Test.Models;
using Microsoft.Maui.Controls;

namespace Test.Services
{
    public class WebScraper
    {
        private const string BASE_URL = "https://tinchi.hau.edu.vn";
        private const string SCHEDULE_URL = BASE_URL + "/TraCuuLichHoc/Index";
        private const string EXAM_URL = BASE_URL + "/TraCuuLichThi/Index";

        private readonly Action<string> _logCallback;
        private readonly WebView _webView;
        private readonly HtmlParser _htmlParser;
        private readonly Dictionary<string, string> _collectedHtmlData;
        private Action<string>? _progressCallback;

        public WebScraper(Action<string> logCallback, WebView webView)
        {
            _logCallback = logCallback;
            _webView = webView;
            _htmlParser = new HtmlParser(logCallback);
            _collectedHtmlData = new Dictionary<string, string>();
        }

        private void Log(string message) => _logCallback?.Invoke(message);

        public void SetProgressCallback(Action<string> callback)
            => _progressCallback = callback;

        private void UpdateProgress(string message)
        {
            _progressCallback?.Invoke(message);
            Log(message);
        }

        public void Reset()
        {
            _collectedHtmlData.Clear();
            Log("🔄 Đã reset scraper");
        }

        public async Task<bool> WaitForLoginAsync()
        {
            Log("👉 Vui lòng đăng nhập trên trình duyệt...");
            Log("👉 Khi URL chuyển sang /SinhVien/Home, tool sẽ tự chạy");
            _webView.Source = BASE_URL;
            return true;
        }

        public async Task<bool> SelectHocKyAsync(int hocKy)
        {
            try
            {
                UpdateProgress($"📚 Đang chọn học kỳ {hocKy}...");
                string js = $@"
                    (function() {{
                        try {{
                            var select = document.getElementById('cmbHocKy');
                            if (!select) return 'FAIL: Select not found';
                            var targetIndex = -1;
                            for (var i = 0; i < select.options.length; i++) {{
                                if (select.options[i].value === '{hocKy}') {{
                                    targetIndex = i; break;
                                }}
                            }}
                            if (targetIndex < 0) return 'FAIL: Option not found';
                            if (typeof $ !== 'undefined') {{
                                $('#cmbHocKy').val('{hocKy}').trigger('change');
                                return 'OK [jQuery] HK{hocKy}: ' + $('#cmbHocKy option:selected').text();
                            }}
                            select.selectedIndex = targetIndex;
                            select.value = '{hocKy}';
                            ['input','change','click'].forEach(function(n) {{
                                select.dispatchEvent(new Event(n, {{bubbles:true}}));
                            }});
                            return 'OK [Native] HK{hocKy}: ' + select.options[select.selectedIndex].text;
                        }} catch(e) {{ return 'ERROR: ' + e.message; }}
                    }})();";

                var result = await _webView.EvaluateJavaScriptAsync(js);
                Log($"   📌 Select HK{hocKy}: {result}");
                await Task.Delay(2000);
                return result.Contains("OK");
            }
            catch (Exception ex)
            {
                Log($"❌ Lỗi chọn học kỳ: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SelectDotHocAsync(int dotValue)
        {
            try
            {
                UpdateProgress($"🖱️ Đang chọn đợt {dotValue}...");
                string js = $@"
                    (function() {{
                        try {{
                            var select = document.getElementById('ddlDotHoc');
                            if (!select) return 'FAIL: Select not found';
                            var targetIndex = -1;
                            for (var i = 0; i < select.options.length; i++) {{
                                if (select.options[i].value === '{dotValue}') {{
                                    targetIndex = i; break;
                                }}
                            }}
                            if (targetIndex < 0) return 'FAIL: Option not found';
                            if (typeof $ !== 'undefined') {{
                                $('#ddlDotHoc').val('{dotValue}').trigger('change');
                                return 'OK [jQuery] Dot {dotValue}: ' + $('#ddlDotHoc option:selected').text();
                            }}
                            select.selectedIndex = targetIndex;
                            select.value = '{dotValue}';
                            ['input','change','click'].forEach(function(n) {{
                                select.dispatchEvent(new Event(n, {{bubbles:true}}));
                            }});
                            return 'OK [Native] Dot {dotValue}: ' + select.options[select.selectedIndex].text;
                        }} catch(e) {{ return 'ERROR: ' + e.message; }}
                    }})();";

                var result = await _webView.EvaluateJavaScriptAsync(js);
                Log($"      📌 Select Đợt {dotValue}: {result}");
                await Task.Delay(1000);
                return result.Contains("OK");
            }
            catch (Exception ex)
            {
                Log($"❌ Lỗi chọn đợt: {ex.Message}");
                return false;
            }
        }

        public async Task<List<DotHocOption>> GetDotHocOptionsAsync(int hocKy)
        {
            try
            {
                await SelectHocKyAsync(hocKy);
                string js = @"
                    (function() {
                        try {
                            var select = document.getElementById('ddlDotHoc');
                            if (!select) return JSON.stringify({error: 'Select not found'});
                            var options = [];
                            for (var i = 0; i < select.options.length; i++) {
                                var opt = select.options[i];
                                if (opt.value && opt.value !== '0' && opt.value !== '')
                                    options.push({ value: opt.value, text: opt.text });
                            }
                            return JSON.stringify(options);
                        } catch(e) { return JSON.stringify({error: e.message}); }
                    })();";

                var json = await _webView.EvaluateJavaScriptAsync(js);
                json = System.Text.RegularExpressions.Regex.Unescape(json.Trim('"'));
                Log($"   📋 Đợt học JSON: {json}");

                List<DotHocOption>? list = null;
                try { list = JsonSerializer.Deserialize<List<DotHocOption>>(json); }
                catch { list = null; }

                if (list == null || list.Count == 0)
                {
                    Log($"   ⚠️ Không parse được JSON, thử 1-10");
                    list = Enumerable.Range(1, 10)
                        .Select(i => new DotHocOption { Value = i.ToString(), Text = $"Đợt {i}" })
                        .ToList();
                }

                Log($"   ✅ Tìm thấy {list.Count} đợt");
                return list;
            }
            catch (Exception ex)
            {
                Log($"❌ Lỗi lấy đợt HK{hocKy}: {ex.Message}");
                return new List<DotHocOption>();
            }
        }

        public async Task<string> GetDataHtmlAsync()
        {
            try
            {
                string js = @"
                    (function() {
                        try {
                            var div = document.getElementById('daTa');
                            if (!div) return 'ERROR: div#daTa not found';
                            var html = div.innerHTML;
                            return 'LENGTH:' + html.length + '|' + html;
                        } catch(e) { return 'ERROR: ' + e.message; }
                    })();";

                var raw = await _webView.EvaluateJavaScriptAsync(js);
                raw = System.Text.RegularExpressions.Regex.Unescape(raw.Trim('"'));

                string actualHtml = "";
                int htmlLength = 0;

                if (raw.StartsWith("LENGTH:"))
                {
                    var parts = raw.Split(new[] { '|' }, 2);
                    if (parts.Length == 2)
                    {
                        int.TryParse(parts[0].Replace("LENGTH:", ""), out htmlLength);
                        actualHtml = parts[1];
                    }
                }
                else
                {
                    actualHtml = raw;
                    htmlLength = raw.Length;
                }

                if (!string.IsNullOrEmpty(actualHtml) &&
                    !actualHtml.StartsWith("ERROR:") && htmlLength > 100)
                {
                    Log($"      ✅ Lấy được {htmlLength:N0} ký tự HTML");
                    return actualHtml;
                }

                Log($"      ⚠️ Không có dữ liệu");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Log($"❌ Lỗi lấy HTML: {ex.Message}");
                return string.Empty;
            }
        }

        public async Task<List<RawEvent>> GetAllSchedulesAsync()
        {
            try
            {
                UpdateProgress("🔍 Đang lấy lịch học...");
                _webView.Source = SCHEDULE_URL;
                await Task.Delay(2000);

                var allData = new List<RawEvent>();

                for (int hocKy = 1; hocKy <= 2; hocKy++)
                {
                    Log($"\n📚 HỌC KỲ {hocKy}");
                    UpdateProgress($"📚 Học kỳ {hocKy}");

                    var dotList = await GetDotHocOptionsAsync(hocKy);
                    if (dotList.Count == 0) { Log($"⚠️ HK{hocKy}: Không có đợt"); continue; }

                    for (int idx = 0; idx < dotList.Count; idx++)
                    {
                        UpdateProgress($"📚 HK{hocKy} - Đợt {idx + 1}/{dotList.Count}");
                        Log($"\n📍 HK{hocKy} Đợt {idx + 1}: {dotList[idx].Text}");

                        await SelectDotHocAsync(idx + 1);
                        var html = await GetDataHtmlAsync();

                        if (!string.IsNullOrEmpty(html))
                        {
                            var scheduleData = _htmlParser.ExtractScheduleFromHtml(html, hocKy);
                            allData.AddRange(scheduleData);
                            Log($"      💾 {scheduleData.Count} buổi");
                        }

                        await Task.Delay(1000);
                    }
                }

                Log($"\n✅ TỔNG LỊCH HỌC: {allData.Count} buổi");
                return allData;
            }
            catch (Exception ex)
            {
                Log($"❌ Lỗi: {ex.Message}");
                return new List<RawEvent>();
            }
        }

        public async Task<List<RawEvent>> GetExamScheduleAsync()
        {
            try
            {
                UpdateProgress("🔍 Đang lấy lịch thi...");
                _webView.Source = EXAM_URL;
                await Task.Delay(2000);

                var allExamData = new List<RawEvent>();

                for (int hocKy = 1; hocKy <= 2; hocKy++)
                {
                    Log($"\n📝 LỊCH THI HỌC KỲ {hocKy}");
                    UpdateProgress($"📝 Học kỳ {hocKy}");

                    var dotList = await GetDotHocOptionsAsync(hocKy);

                    if (dotList.Count == 0)
                    {
                        await Task.Delay(2000);
                        var html = await GetDataHtmlAsync();
                        if (!string.IsNullOrEmpty(html))
                        {
                            var examData = _htmlParser.ExtractExamFromHtml(html, hocKy);
                            allExamData.AddRange(examData);
                            Log($"      💾 {examData.Count} môn thi");
                        }
                        continue;
                    }

                    for (int idx = 0; idx < dotList.Count; idx++)
                    {
                        UpdateProgress($"📝 HK{hocKy} - Đợt {idx + 1}/{dotList.Count}");
                        Log($"\n📍 HK{hocKy} Đợt {idx + 1}: {dotList[idx].Text}");

                        await SelectDotHocAsync(idx + 1);
                        await Task.Delay(2000);

                        var html = await GetDataHtmlAsync();
                        if (!string.IsNullOrEmpty(html))
                        {
                            var examData = _htmlParser.ExtractExamFromHtml(html, hocKy);
                            allExamData.AddRange(examData);
                            Log($"      💾 {examData.Count} môn thi");
                        }

                        await Task.Delay(1000);
                    }
                }

                Log($"\n✅ TỔNG LỊCH THI: {allExamData.Count} môn");
                return allExamData;
            }
            catch (Exception ex)
            {
                Log($"❌ Lỗi: {ex.Message}");
                return new List<RawEvent>();
            }
        }

        public Dictionary<string, string> GetCollectedHtmlData()
            => _collectedHtmlData;
    }
}