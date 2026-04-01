using System.Globalization;
using System.Windows.Input;
using Test.Models;
using Test.Services;

namespace Test.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly IApiService _apiService;
    private readonly ILocalCacheService _localCache;

    private List<CalendarInfo> _calendars = new();
    private CancellationTokenSource? _fetchCts;

    // ── Menu ──────────────────────────────────────────────────
    private bool _isMenuVisible;
    private string _username = string.Empty;
    private string _email = string.Empty;
    private string _avatarLetter = "U";

    public bool IsMenuVisible
    {
        get => _isMenuVisible;
        set { _isMenuVisible = value; OnPropertyChanged(); }
    }
    public string Username
    {
        get => _username;
        set
        {
            _username = value; OnPropertyChanged();
            AvatarLetter = value.Length > 0
                ? value[0].ToString().ToUpper() : "U";
        }
    }
    public string Email
    {
        get => _email;
        set { _email = value; OnPropertyChanged(); }
    }
    public string AvatarLetter
    {
        get => _avatarLetter;
        set { _avatarLetter = value; OnPropertyChanged(); }
    }

    // ── Log ───────────────────────────────────────────────────
    private string _logText = "✅ Sẵn sàng!";
    public string LogText
    {
        get => _logText;
        set { _logText = value; OnPropertyChanged(); }
    }
    private readonly List<string> _logLines = new();

    // ── Data ──────────────────────────────────────────────────
    public List<EventItem> Schedule { get; private set; } = new();
    public Dictionary<DateTime, List<EventItem>> Events { get; private set; } = new();

    // ── Commands ──────────────────────────────────────────────
    public ICommand OpenMenuCommand { get; }
    public ICommand CloseMenuCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand SyncFromServerCommand { get; }

    // ── Callbacks ─────────────────────────────────────────────
    public Action? OnCalendarNeedsUpdate { get; set; }
    public Action<DateTime>? OnShowEventDetail { get; set; }
    public Action? OnScrollLogToBottom { get; set; }
    public Action? OnUserLoggedOut { get; set; }
    public Func<string, string, string, Task<bool>>? OnConfirmAsync { get; set; }
    public Func<string, string[], Task<string?>>? OnActionSheetAsync { get; set; }

    public MainViewModel(IAuthService authService,
                         IApiService apiService,
                         ILocalCacheService localCache)
    {
        _authService = authService;
        _apiService = apiService;
        _localCache = localCache;

        OpenMenuCommand = new Command(() => IsMenuVisible = true);
        CloseMenuCommand = new Command(() => IsMenuVisible = false);
        LogoutCommand = new Command(async () => await LogoutAsync(), () => IsNotBusy);
        ClearCommand = new Command(async () => await ClearAsync(), () => IsNotBusy);
        SyncFromServerCommand = new Command(async () => await SyncFromServerAsync(), () => IsNotBusy);
    }

    // ── SetUser: gọi từ MainPage.OnAppearing ─────────────────
    public void SetUser(string userId, string username, string email)
    {
        System.Diagnostics.Debug.WriteLine(
            $"👤 SetUser: userId={userId} username={username}");
        _localCache.SetCurrentUser(userId);
        _calendars.Clear();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Username = string.IsNullOrEmpty(username) ? "User" : username;
            Email = email ?? string.Empty;
        });
    }

    // ── Init ──────────────────────────────────────────────────
    public async Task InitAsync()
    {
        _fetchCts?.Cancel();
        _fetchCts = new CancellationTokenSource();
        var cts = _fetchCts;

        // Load SQLite — nhanh vì local
        var cached = await _localCache.LoadScheduleAsync();

        if (cached.Any())
        {
            Schedule = cached;
            BuildEvents();
            // Cập nhật UI ngay trên MainThread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnCalendarNeedsUpdate?.Invoke();
            });
            var lastSync = await _localCache.GetLastSyncTimeAsync();
            AddLog($"📱 {cached.Count} sự kiện");
            if (lastSync.HasValue)
                AddLog($"🕐 {lastSync.Value:dd/MM HH:mm}");
        }
        else
        {
            AddLog("📱 Chưa có dữ liệu — bấm 'Tải Web 🌐'");
        }

        // Fetch server ngầm — không block UI
        _ = Task.Run(async () =>
        {
            if (!cts.Token.IsCancellationRequested)
                await FetchFromServerAsync(cts.Token);
        });
    }

    // ── Fetch từ server (DUY NHẤT) ────────────────────────────
    private async Task FetchFromServerAsync(CancellationToken ct = default)
    {
        try
        {
            var fetchUserId = _localCache.GetCurrentUserId();
            System.Diagnostics.Debug.WriteLine($"🌐 Fetch START userId={fetchUserId}");

            var start = new DateTime(DateTime.Today.Year, 1, 1);
            var end = new DateTime(DateTime.Today.Year, 12, 31);
            var events = await _apiService.GetEventsAsync(start, end);

            var currentUserId = _localCache.GetCurrentUserId();
            System.Diagnostics.Debug.WriteLine(
                $"🌐 Fetch GOT {events.Count} events, " +
                $"fetchUserId={fetchUserId}, currentUserId={currentUserId}");

            if (ct.IsCancellationRequested) return;
            if (!events.Any()) return;

            if (currentUserId != fetchUserId)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ UserId thay đổi! Bỏ qua save.");
                return;
            }

            Schedule = events;
            BuildEvents();
            await _localCache.SaveScheduleAsync(events);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!ct.IsCancellationRequested)
                {
                    OnCalendarNeedsUpdate?.Invoke();
                    AddLog($"✅ Đồng bộ {events.Count} sự kiện");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FetchFromServer ERR: {ex.Message}");
        }
    }

    // ── Sync thủ công (nút Server ☁️) ────────────────────────
    public async Task SyncFromServerAsync()
    {
        IsBusy = true;
        AddLog("☁️ Đang đồng bộ từ server...");
        await FetchFromServerAsync();
        IsBusy = false;
    }

    // ── Sync lên server sau khi scrape ────────────────────────
    public async Task SyncToServerAsync(List<RawEvent> rawEvents)
    {
        if (!rawEvents.Any()) return;

        if (!_calendars.Any())
            _calendars = await _apiService.GetCalendarsAsync();

        System.Diagnostics.Debug.WriteLine(
            $"📋 Calendars: {string.Join(", ", _calendars.Select(c => $"{c.Id}:{c.Name}"))}");

        var classCalendar = _calendars.FirstOrDefault(c => c.Name == "Lịch học");
        var examCalendar = _calendars.FirstOrDefault(c => c.Name == "Lịch thi");

        if (classCalendar == null || examCalendar == null)
        {
            AddLog("❌ Không tìm thấy calendar — thử đăng xuất & đăng nhập lại");
            return;
        }

        var classEvents = rawEvents.Where(e => e.Type == "class").ToList();
        var examEvents = rawEvents.Where(e => e.Type == "exam").ToList();

        AddLog("☁️ Đang lưu lên server...");

        if (classEvents.Any())
        {
            var (imp, skip) = await _apiService.SyncImportAsync(classCalendar.Id, classEvents);
            AddLog($"📚 Lịch học: +{imp} mới, {skip} trùng");
        }

        if (examEvents.Any())
        {
            var (imp, skip) = await _apiService.SyncImportAsync(examCalendar.Id, examEvents);
            AddLog($"📝 Lịch thi: +{imp} mới, {skip} trùng");
        }

        await FetchFromServerAsync();
    }

    // ── Logout ────────────────────────────────────────────────
    private async Task LogoutAsync()
    {
        if (OnConfirmAsync != null)
        {
            var confirmed = await OnConfirmAsync("Đăng xuất",
                "Bạn có chắc muốn đăng xuất không?", "Đăng xuất");
            if (!confirmed) return;
        }

        IsBusy = true;
        IsMenuVisible = false;
        _fetchCts?.Cancel();
        await _authService.LogoutAsync();

        Schedule.Clear();
        Events.Clear();
        _calendars.Clear();
        ClearLog();

        //.Set("view_mode", "month");   // ← THÊM: reset về month khi logout

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Username = string.Empty;
            Email = string.Empty;
            AvatarLetter = "U";
            OnCalendarNeedsUpdate?.Invoke();
        });

        OnUserLoggedOut?.Invoke();
        IsBusy = false;
        await Shell.Current.GoToAsync("//LoginPage");
    }

    // ── Log ───────────────────────────────────────────────────
    public void AddLog(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _logLines.Add(line);
            if (_logLines.Count > 100) _logLines.RemoveAt(0);
            LogText = string.Join("\n", _logLines);
            OnScrollLogToBottom?.Invoke();
        });
    }

    public void ClearLog()
    {
        _logLines.Clear();
        LogText = string.Empty;
    }

    // ── BuildEvents ───────────────────────────────────────────
    public void BuildEvents()
    {
        Events.Clear();
        foreach (var item in Schedule)
        {
            // Normalize giờ nếu StartTime chỉ có date (giờ = 0)
            if (item.StartTime.Hour == 0 && item.EndTime.Hour == 0
                && !string.IsNullOrEmpty(item.Period))
            {
                var (startDt, endDt) = PeriodTimes.ParsePeriodToDateTime(
                    item.Period, item.StartTime.Date);
                item.StartTime = startDt;
                item.EndTime = endDt;
            }

            var date = item.StartTime.Date;
            if (!Events.ContainsKey(date))
                Events[date] = new List<EventItem>();
            Events[date].Add(item);
        }
    }

    // ── Clear ─────────────────────────────────────────────────
    private async Task ClearAsync()
    {
        if (OnActionSheetAsync == null) return;

        var choice = await OnActionSheetAsync(
            "Xóa dữ liệu",
            new[] {
            "🗑️ Xóa local data",
            "☁️ Xóa data trên server",
            "💣 Xóa toàn bộ"
            });

        if (choice == null) return;

        IsBusy = true;

        if (choice == "🗑️ Xóa local data" || choice == "💣 Xóa toàn bộ")
        {
            try
            {
                await _localCache.ClearAsync();
                Schedule.Clear();
                Events.Clear();
                ClearLog();
                OnCalendarNeedsUpdate?.Invoke();
                AddLog("🗑️ Đã xóa local data");
            }
            catch (Exception ex) { AddLog($"❌ Lỗi xóa local: {ex.Message}"); }
        }

        if (choice == "☁️ Xóa data trên server" || choice == "💣 Xóa toàn bộ")
        {
            try
            {
                await _apiService.DeleteAllEventsAsync();
                AddLog("☁️ Đã xóa data trên server");

                if (choice != "💣 Xóa toàn bộ")
                {
                    // Chỉ xóa server → reload từ server (trả về rỗng)
                    Schedule.Clear();
                    Events.Clear();
                    OnCalendarNeedsUpdate?.Invoke();
                }
            }
            catch (Exception ex) { AddLog($"❌ Lỗi xóa server: {ex.Message}"); }
        }

        IsBusy = false;
    }
    //public string Greeting
    //{
    //    get
    //    {
    //        var hour = DateTime.Now.Hour;

    //        if (hour < 12)
    //            return "Chào buổi sáng 🌅";
    //        else if (hour < 18)
    //            return "Chào buổi chiều ☀️";
    //        else
    //            return "Chào buổi tối 🌙";
    //    }
    //}
}