using System.Diagnostics;
using Test.Models;
using Test.Services;
using Test.ViewModels;

namespace Test.Pages;

public partial class MainPage : ContentPage
{
    private MainViewModel _vm => (MainViewModel)BindingContext;

    private DateTime _currentDate = DateTime.Today;
    private int _selectedDay = -1;

    private WebScraper? _scraper;
    private bool _isLoginSuccessful = false;
    private bool _isToolRunning = false;
    private bool _isLoading = false;

    private bool _weekViewInitialized = false;
    private bool _isShowingWeekDetail = false;
    private bool _suppressMonthPickerEvent = false;
    private bool _uiRestoredFromCache = false;

    private const string PrefViewMode = "view_mode";

    private DateTime _selectedDate = DateTime.Today;
    private (Frame frame, Color origBg, Color origBorder)? _selectedFrame = null;

    private readonly ILocalCacheService _localCache;

    public MainPage(MainViewModel vm, ILocalCacheService localCache)
    {
        InitializeComponent();
        BindingContext = vm;
        _localCache = localCache;

        vm.OnCalendarNeedsUpdate = () => GenerateCalendar();
        vm.OnConfirmAsync = async (title, msg, accept) =>
            await DisplayAlert(title, msg, accept, "Hủy");
        vm.OnActionSheetAsync = async (title, options) =>
            await ShowRadioDialogAsync(title, options);
        vm.OnScrollLogToBottom = () => ShowEventsForDate(_selectedDate);
        vm.OnShowEventDetail = async (date) => await ShowEventDetailAsync(date);
        vm.OnUserLoggedOut = () => _lastInitUserId = string.Empty;

        GenerateCalendar();
        UpdatePeriodLabel();
        NavigationPage.SetHasNavigationBar(this, false);

        WeekViewControl.OnShowEventDetail = async (date) =>
            await ShowWeekEventDetailAsync(date);
    }
    private string _lastInitUserId = string.Empty;
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_isShowingWeekDetail) return;

        _vm.OnCalendarNeedsUpdate = () => GenerateCalendar();

        var userId = await SecureStorage.GetAsync("user_id") ?? "default";

        // ── Giai đoạn 1: Restore UI ngay lập tức từ cache ──────
        if (!_uiRestoredFromCache)
        {
            _uiRestoredFromCache = true;
            _localCache.SetCurrentUser(userId);

            var cached = await _localCache.LoadScheduleAsync();
            if (cached.Any())
            {
                _vm.Schedule.Clear();
                foreach (var e in cached) _vm.Schedule.Add(e);
                _vm.BuildEvents();
            }

            RestoreViewMode();
        }

        // ── Giai đoạn 2: Init đầy đủ nếu user thay đổi ─────────
        if (userId == _lastInitUserId) return;

        _weekViewInitialized = false;
        _lastInitUserId = userId;
        _currentDate = DateTime.Today;
        _selectedDate = DateTime.Today;

        var username = await SecureStorage.GetAsync("username") ?? "User";
        var displayName = await SecureStorage.GetAsync("display_name") ?? username;
        var email = await SecureStorage.GetAsync("email") ?? "";

        _vm.SetUser(userId, displayName, email);

        await _vm.InitAsync();

        GenerateCalendar();
    }
    private void RestoreViewMode()
    {
        if (_isShowingWeekDetail) return;
        var savedMode = Preferences.Get(PrefViewMode, "month");
        if (savedMode == "week")
            OnWeekViewClicked(this, EventArgs.Empty);
        else
            OnMonthViewClicked(this, EventArgs.Empty);
    }
    // ── Helper: ngày mặc định khi chuyển tháng ───────────────
    private static DateTime GetDefaultDayForMonth(DateTime month)
    {
        if (month.Year == DateTime.Today.Year && month.Month == DateTime.Today.Month)
            return DateTime.Today;
        return new DateTime(month.Year, month.Month, 1);
    }
    // ── Show event detail (month view) ────────────────────────
    private async Task ShowEventDetailAsync(DateTime date)
    {
        ShowEventsForDate(date.Date);
    }
    private void ShowEventsForDate(DateTime date)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var today = DateTime.Today;
            var diff = (date.Date - today).Days;
            LblPanelTitle.Text = diff switch
            {
                0 => "Hôm nay",
                1 => "Ngày mai",
                -1 => "Hôm qua",
                _ => date.ToString("dd/MM/yyyy")
            };

            TodayEventStack.Children.Clear();

            _vm.Events.TryGetValue(date.Date, out var dayEvents);

            if (dayEvents == null || !dayEvents.Any())
            {
                TodayEventStack.Children.Add(BuildEmptyCard(date));
                return;
            }

            var sorted = dayEvents
                .Select(evt =>
                {
                    var (startDt, endDt) = PeriodTimes.ParsePeriodToDateTime(evt.Period, date);
                    return (evt, startDt, endDt);
                })
                .OrderBy(x => x.startDt)
                .ToList();

            foreach (var (evt, startDt, endDt) in sorted)
                TodayEventStack.Children.Add(BuildEventCard(evt, startDt, endDt));
        });
    }
    private Frame BuildEmptyCard(DateTime date)
    {
        var today = DateTime.Today;
        var diff = (date.Date - today).Days;
        var label = diff switch
        {
            0 => "Hôm nay không có lịch",
            1 => "Ngày mai không có lịch",
            -1 => "Hôm qua không có lịch",
            _ => $"Không có lịch ngày {date:dd/MM/yyyy}"
        };

        return new Frame
        {
            BackgroundColor = Color.FromArgb("#F8F9FA"),
            BorderColor = Color.FromArgb("#E0E0E0"),
            CornerRadius = 14,
            Padding = new Thickness(16, 20),
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                HorizontalOptions = LayoutOptions.Center,
                Spacing = 6,
                Children =
                {
                    new Label
                    {
                        Text = "📭",
                        FontSize = 28,
                        HorizontalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = label,
                        FontSize = 14,
                        TextColor = Color.FromArgb("#95A5A6"),
                        HorizontalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            }
        };
    }
    private Frame BuildEventCard(EventItem evt, DateTime startDt, DateTime endDt)
    {
        var isExam = evt.Type == "exam";
        var iconBg = isExam
            ? Color.FromArgb("#FDEBD0")
            : Color.FromArgb("#D6EAF8");
        var accentColor = isExam
            ? Color.FromArgb("#E67E22")
            : Color.FromArgb("#2980B9");
        var iconText = isExam ? "📝" : "📚";
        var timeText = $"{startDt:HH:mm} — {endDt:HH:mm}";

        var iconFrame = new Frame
        {
            BackgroundColor = accentColor.WithAlpha(0.18f),
            BorderColor = Colors.Transparent,
            CornerRadius = 22,
            WidthRequest = 44,
            HeightRequest = 44,
            Padding = 0,
            HasShadow = false,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Content = new Label
            {
                Text = iconText,
                FontSize = 20,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        var infoStack = new VerticalStackLayout { Spacing = 3 };

        infoStack.Children.Add(new Label
        {
            Text = timeText,
            FontSize = 12,
            TextColor = Color.FromArgb("#7F8C8D")
        });
        infoStack.Children.Add(new Label
        {
            Text = evt.Title,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2C3E50"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        infoStack.Children.Add(new Label
        {
            Text = evt.Period,
            FontSize = 12,
            TextColor = Color.FromArgb("#7F8C8D")
        });

        if (!string.IsNullOrWhiteSpace(evt.Location))
        {
            var pillGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 4
            };
            var pinIcon = new Label
            {
                Text = "📍",
                FontSize = 11,
                VerticalOptions = LayoutOptions.Center
            };
            var locLabel = new Label
            {
                Text = evt.Location,
                FontSize = 12,
                TextColor = accentColor,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(pinIcon, 0);
            Grid.SetColumn(locLabel, 1);
            pillGrid.Children.Add(pinIcon);
            pillGrid.Children.Add(locLabel);

            infoStack.Children.Add(new Frame
            {
                BackgroundColor = accentColor.WithAlpha(0.12f),
                BorderColor = Colors.Transparent,
                CornerRadius = 12,
                Padding = new Thickness(10, 4),
                HasShadow = false,
                HorizontalOptions = LayoutOptions.Start,
                Content = pillGrid
            });
        }

        if (!string.IsNullOrWhiteSpace(evt.Teacher))
            infoStack.Children.Add(new Label
            {
                Text = $"👨‍🏫 {evt.Teacher}",
                FontSize = 12,
                TextColor = Color.FromArgb("#7F8C8D")
            });

        var cardGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12
        };
        Grid.SetColumn(iconFrame, 0);
        Grid.SetColumn(infoStack, 1);
        cardGrid.Children.Add(iconFrame);
        cardGrid.Children.Add(infoStack);

        return new Frame
        {
            BackgroundColor = iconBg,
            BorderColor = Colors.Transparent,
            CornerRadius = 14,
            Padding = new Thickness(14, 12),
            HasShadow = false,
            Content = cardGrid
        };
    }
    // ── Tải Web ───────────────────────────────────────────────
    private async void OnLoadWebClicked(object sender, EventArgs e)
    {
        if (_isLoading)
        {
            await DisplayAlert("Thông báo", "Đang tải, vui lòng đợi...", "OK");
            return;
        }
        _isLoginSuccessful = false;
        _isToolRunning = false;
        _isLoading = true;
        _vm.ClearLog();
        _vm.AddLog("🌐 Bắt đầu tải dữ liệu...");
        _vm.AddLog("👉 Vui lòng đăng nhập vào tinchi.hau.edu.vn");
        WebViewContainer.IsVisible = true;
        _scraper = new WebScraper(_vm.AddLog, LoginWebView);
        await _scraper.WaitForLoginAsync();
    }
    private void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
    }
    private async void OnWebViewNavigated(object sender, WebNavigatedEventArgs e)
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        if (!_isLoginSuccessful && !_isToolRunning &&
            e.Url.Contains("/SinhVien/Home"))
        {
            _isLoginSuccessful = true;
            await RunScraperAsync();
        }
    }
    private async Task RunScraperAsync()
    {
        try
        {
            _isToolRunning = true;
            _vm.AddLog("✅ Đăng nhập thành công! Bắt đầu lấy dữ liệu...");
            var classData = await _scraper!.GetAllSchedulesAsync();
            var examData = await _scraper!.GetExamScheduleAsync();
            var rawEvents = new List<RawEvent>();
            rawEvents.AddRange(classData.Select(e => new RawEvent
            {
                Date = e.Date,
                Subject = e.Subject,
                Period = e.Period,
                Room = e.Room,
                Teacher = e.Teacher,
                Type = "class",
                HocKy = e.HocKy
            }));
            rawEvents.AddRange(examData.Select(e => new RawEvent
            {
                Date = e.Date,
                Subject = e.Subject,
                Period = e.Period,
                Room = e.Room,
                Teacher = e.Teacher,
                Type = "exam",
                HocKy = e.HocKy
            }));
            _vm.AddLog($"🎉 {classData.Count} lịch học + {examData.Count} lịch thi");
            WebViewContainer.IsVisible = false;
            await _vm.SyncToServerAsync(rawEvents);
            ClearWebViewCache();
            await DisplayAlert("Thành công ✅",
                $"📚 Lịch học: {classData.Count}\n📝 Lịch thi: {examData.Count}", "OK");
        }
        catch (Exception ex)
        {
            _vm.AddLog($"❌ Lỗi: {ex.Message}");
            WebViewContainer.IsVisible = false;
            await DisplayAlert("Lỗi", ex.Message, "OK");
        }
        finally
        {
            _isToolRunning = false;
            _isLoading = false;
        }
    }
    private void ClearWebViewCache()
    {
        try
        {
            var cacheDir = FileSystem.CacheDirectory;
            if (Directory.Exists(cacheDir))
                foreach (var file in Directory.GetFiles(
                    cacheDir, "*", SearchOption.AllDirectories))
                    try { File.Delete(file); } catch { }
#if ANDROID
            var webView = LoginWebView.Handler?.PlatformView
                as Android.Webkit.WebView;
            webView?.ClearCache(true);
            webView?.ClearHistory();
            Android.Webkit.CookieManager.Instance?.RemoveAllCookies(null);
            Android.Webkit.CookieManager.Instance?.Flush();
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ClearCache: {ex.Message}");
        }
    }
    private void OnCancelLoginClicked(object sender, EventArgs e)
    {
        WebViewContainer.IsVisible = false;
        _isLoading = false;
        _isLoginSuccessful = false;
        _isToolRunning = false;
        _vm.AddLog("❌ Đã hủy");
    }
    // ── Calendar ─────────────────────────────────────────────
    private void GenerateCalendar()
    {
        //_suppressMonthPickerEvent = true;
        //PeriodBtn.Date = _currentDate;

        var toRemove = CalendarGrid.Children
            .Where(c => c is View v && Grid.GetRow(v) > 0).ToList();
        foreach (var c in toRemove)
            CalendarGrid.Children.Remove(c);

        _selectedFrame = null; // reset reference trước khi build lại

        var first = new DateTime(_currentDate.Year, _currentDate.Month, 1);
        int daysInMonth = DateTime.DaysInMonth(_currentDate.Year, _currentDate.Month);
        int startCol = (int)first.DayOfWeek;
        startCol = startCol == 0 ? 6 : startCol - 1;
        int rowsNeeded = (int)Math.Ceiling((startCol + daysInMonth) / 7.0);
        int day = 1;

        for (int row = 1; row <= rowsNeeded; row++)
            for (int col = 0; col < 7; col++)
            {
                int cellIndex = (row - 1) * 7 + col;
                if (cellIndex < startCol || day > daysInMonth)
                    AddEmptyCell(row, col);
                else
                {
                    AddDayCell(_currentDate.Year, _currentDate.Month, day, row, col);
                    day++;
                }
            }

        // Luôn hiển thị lịch của ngày đang selected — không cache, không bỏ qua
        ShowEventsForDate(_selectedDate);
    }
    private void AddEmptyCell(int row, int col)
    {
        var first = new DateTime(_currentDate.Year, _currentDate.Month, 1);
        int startCol = (int)first.DayOfWeek;
        startCol = startCol == 0 ? 6 : startCol - 1;

        int cellIndex = (row - 1) * 7 + col;
        DateTime dateObj;

        if (cellIndex < startCol)
        {
            int daysBefore = startCol - cellIndex;
            dateObj = first.AddDays(-daysBefore);
        }
        else
        {
            int daysInMonth = DateTime.DaysInMonth(_currentDate.Year, _currentDate.Month);
            int daysAfter = cellIndex - (startCol + daysInMonth) + 1;
            dateObj = new DateTime(_currentDate.Year, _currentDate.Month, daysInMonth)
                          .AddDays(daysAfter);
        }

        var fgColor = Color.FromArgb("#798694");

        var dayLabel = new Label
        {
            Text = dateObj.Day.ToString(),
            TextColor = fgColor,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };

        Color? dotColor = null;
        if (_vm.Events.ContainsKey(dateObj.Date))
        {
            var types = _vm.Events[dateObj.Date].Select(e => e.Type).ToList();
            if (types.Contains("exam") && types.Contains("class"))
                dotColor = Color.FromArgb("#80DBC7E3");
            else if (types.Contains("exam"))
                dotColor = Color.FromArgb("#80f5d7c0");
            else
                dotColor = Color.FromArgb("#80a4c6e1");
        }

        var dot = new BoxView
        {
            WidthRequest = 10,
            HeightRequest = 10,
            CornerRadius = 5,
            BackgroundColor = dotColor ?? Colors.Transparent,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End
        };

        var cellContent = new VerticalStackLayout
        {
            Spacing = 0,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { dayLabel, dot }
        };

        var dateCopy = dateObj.Date;
        var frame = new Frame
        {
            BackgroundColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            CornerRadius = 8,
            HeightRequest = 50,
            WidthRequest = 50,
            Padding = new Thickness(2),
            HasShadow = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = cellContent,
            Margin = new Thickness(3)
        };

        frame.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() =>
            {
                _currentDate = dateCopy;
                _selectedDate = dateCopy;
                GenerateCalendar();
            })
        });
        frame.GestureRecognizers.Add(new SwipeGestureRecognizer
        {
            Direction = SwipeDirection.Left,
            Command = new Command(() => NavigateMonth(1))
        });
        frame.GestureRecognizers.Add(new SwipeGestureRecognizer
        {
            Direction = SwipeDirection.Right,
            Command = new Command(() => NavigateMonth(-1))
        });

        Grid.SetRow(frame, row);
        Grid.SetColumn(frame, col);
        CalendarGrid.Children.Add(frame);
    }
    private void AddDayCell(int year, int month, int day, int row, int col)
    {
        var dateObj = new DateTime(year, month, day);
        var isToday = dateObj == DateTime.Today;
        var bgColor = Colors.White;
        var fgColor = Color.FromArgb("#2C3E50");
        var borderColor = Color.FromArgb("#E0E0E0");

        Color? dotColor = null;
        if (_vm.Events.ContainsKey(dateObj.Date))
        {
            var types = _vm.Events[dateObj.Date].Select(e => e.Type).ToList();
            if (types.Contains("exam") && types.Contains("class"))
                dotColor = Color.FromArgb("#DBC7E3");
            else if (types.Contains("exam"))
                dotColor = Color.FromArgb("#f5d7c0");
            else
                dotColor = Color.FromArgb("#a4c6e1");
        }

        if (isToday)
        {
            borderColor = Color.FromArgb("#3498DB");
            bgColor = Color.FromArgb("#EBF5FB");
        }

        var isSelected = _selectedDate.Date == dateObj.Date;

        var dayLabel = new Label
        {
            Text = $"{day}",
            TextColor = isToday ? Color.FromArgb("#3498DB") : fgColor,
            FontSize = 15,
            //FontFamily = "Arial",
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var dot = new BoxView
        {
            WidthRequest = 10,
            HeightRequest = 10,
            CornerRadius = 5,
            BackgroundColor = dotColor ?? Colors.Transparent,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
        };

        var cellContent = new VerticalStackLayout
        {
            Spacing = 0,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { dayLabel, dot }
        };

        var frame = new Frame
        {
            BackgroundColor = isSelected ? Color.FromArgb("#D6EAF8") : bgColor,
            BorderColor = isSelected ? Color.FromArgb("#3498DB") : borderColor,
            CornerRadius = 8,
            HeightRequest = 50,
            WidthRequest = 50,
            Padding = new Thickness(2),
            HasShadow = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = cellContent,
            Margin = new Thickness(3),
        };

        if (isSelected)
            _selectedFrame = (frame, bgColor, borderColor);

        var dateCopy = dateObj.Date;
        frame.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() =>
            {
                if (_selectedFrame.HasValue)
                {
                    _selectedFrame.Value.frame.BackgroundColor = _selectedFrame.Value.origBg;
                    _selectedFrame.Value.frame.BorderColor = _selectedFrame.Value.origBorder;
                }
                frame.BackgroundColor = Color.FromArgb("#D6EAF8");
                frame.BorderColor = Color.FromArgb("#3498DB");
                _selectedDate = dateCopy;
                _selectedFrame = (frame, bgColor, borderColor);

                ShowEventsForDate(dateCopy);
            })
        });
        frame.GestureRecognizers.Add(new SwipeGestureRecognizer
        {
            Direction = SwipeDirection.Left,
            Command = new Command(() => NavigateMonth(1))
        });
        frame.GestureRecognizers.Add(new SwipeGestureRecognizer
        {
            Direction = SwipeDirection.Right,
            Command = new Command(() => NavigateMonth(-1))
        });

        Grid.SetRow(frame, row);
        Grid.SetColumn(frame, col);
        CalendarGrid.Children.Add(frame);
    }
    // ── Navigation ────────────────────────────────────────────
    // Dùng chung cho ◀/▶, swipe — tự động select ngày phù hợp
    private void NavigateMonth(int delta)
    {
        _currentDate = _currentDate.AddMonths(delta);
        // Tháng chứa today → select today | Tháng khác → select ngày 1
        _selectedDate = GetDefaultDayForMonth(_currentDate);
        GenerateCalendar();
        UpdatePeriodLabel();
    }
    private void OnPreviousClicked(object sender, EventArgs e) => NavigateMonth(-1);
    private void OnNextClicked(object sender, EventArgs e) => NavigateMonth(1);
    private void OnTodayClicked(object sender, EventArgs e)
    {
        _currentDate = DateTime.Today;
        _selectedDate = DateTime.Today;
        GenerateCalendar();
        UpdatePeriodLabel();
    }
    // DatePicker
    private void OnCalendarPickerDateSelected(object sender, DateChangedEventArgs e)
    {
        if (_suppressMonthPickerEvent)
        {
            _suppressMonthPickerEvent = false;
            return;
        }

        _currentDate = e.NewDate;
        _selectedDate = e.NewDate;
        GenerateCalendar(); // sẽ tự ShowEventsForDate(_selectedDate) ở cuối
        UpdatePeriodLabel();
    }
    private void OnCalendarSwipedLeft(object sender, SwipedEventArgs e) => NavigateMonth(1);
    private void OnCalendarSwipedRight(object sender, SwipedEventArgs e) => NavigateMonth(-1);
    private void UpdatePeriodLabel()
    {
        _suppressMonthPickerEvent = false;
        PeriodBtn.Date = _currentDate;
        // _suppressMonthPickerEvent reset trong handler khi nó fire
    }
    private static DateTime GetMonday(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }
    // ── View Mode ─────────────────────────────────────────────
    private void OnMonthViewClicked(object sender, EventArgs e)
    {
        BtnMonthView.BackgroundColor = Color.FromArgb("#3498DB");
        BtnMonthView.TextColor = Colors.White;
        BtnWeekView.BackgroundColor = Color.FromArgb("#ECF0F1");
        BtnWeekView.TextColor = Color.FromArgb("#2C3E50");

        MonthCalendarContainer.IsVisible = true;
        LogPanel.IsVisible = true;
        WeekViewControl.IsVisible = false;
        NavBarRow.IsVisible = true;
        LegendRow.IsVisible = true;

        _vm.OnCalendarNeedsUpdate = () => GenerateCalendar();

        GenerateCalendar();
        UpdatePeriodLabel();
        Preferences.Set(PrefViewMode, "month");
    }
    private void OnWeekViewClicked(object sender, EventArgs e)
    {
        Preferences.Set(PrefViewMode, "week");

        MonthCalendarContainer.IsVisible = false;
        LogPanel.IsVisible = false;
        WeekViewControl.IsVisible = true;
        NavBarRow.IsVisible = false;
        LegendRow.IsVisible = false;

        BtnWeekView.BackgroundColor = Color.FromArgb("#3498DB");
        BtnWeekView.TextColor = Colors.White;
        BtnMonthView.BackgroundColor = Color.FromArgb("#ECF0F1");
        BtnMonthView.TextColor = Color.FromArgb("#2C3E50");

        _vm.OnCalendarNeedsUpdate = () => WeekViewControl.Refresh();

        if (!_weekViewInitialized)
        {
            WeekViewControl.Initialize(_vm, GetMonday(DateTime.Today));
            _weekViewInitialized = true;
        }
        else if (_isShowingWeekDetail)
        {
            WeekViewControl.Refresh();
        }
        else
        {
            WeekViewControl.GoToDay(_selectedDate);
        }
    }
    private void OnPreviousMonthClicked(object sender, EventArgs e)
    {
        _currentDate = _currentDate.AddMonths(-1);
        _selectedDay = -1;
        GenerateCalendar();
    }
    private void OnNextMonthClicked(object sender, EventArgs e)
    {
        _currentDate = _currentDate.AddMonths(1);
        _selectedDay = -1;
        GenerateCalendar();
    }
    // ── Radio Dialog ──────────────────────────────────────────
    private Task<string?> ShowRadioDialogAsync(string title, string[] options)
    {
        var tcs = new TaskCompletionSource<string?>();
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            string? selected = null;
            List<(Frame dot, Label lbl, Frame row)> items = new();
            var page = new ContentPage { BackgroundColor = Color.FromArgb("#80000000") };
            var dialogBox = new Frame
            {
                BackgroundColor = Colors.White,
                CornerRadius = 14,
                Padding = new Thickness(0),
                HasShadow = true,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                WidthRequest = 300
            };
            var dialogContent = new VerticalStackLayout { Spacing = 0 };
            dialogContent.Children.Add(new Label
            {
                Text = title,
                FontSize = 17,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#2C3E50"),
                HorizontalTextAlignment = TextAlignment.Center,
                Padding = new Thickness(20, 20, 20, 12)
            });
            dialogContent.Children.Add(new BoxView
            { HeightRequest = 1, BackgroundColor = Color.FromArgb("#E0E0E0") });

            void UpdateSelection(int chosenIdx)
            {
                selected = options[chosenIdx];
                for (int i = 0; i < items.Count; i++)
                {
                    bool active = i == chosenIdx;
                    items[i].dot.BackgroundColor = active
                        ? Color.FromArgb("#3498DB") : Colors.White;
                    items[i].dot.BorderColor = active
                        ? Color.FromArgb("#3498DB") : Color.FromArgb("#BDC3C7");
                    items[i].lbl.TextColor = active
                        ? Color.FromArgb("#3498DB") : Color.FromArgb("#2C3E50");
                }
            }

            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                var dot = new Frame
                {
                    WidthRequest = 20,
                    HeightRequest = 20,
                    CornerRadius = 10,
                    Padding = new Thickness(0),
                    BackgroundColor = Colors.White,
                    BorderColor = Color.FromArgb("#BDC3C7"),
                    HasShadow = false,
                    VerticalOptions = LayoutOptions.Center
                };
                var lbl = new Label
                {
                    Text = options[i],
                    FontSize = 15,
                    TextColor = Color.FromArgb("#2C3E50"),
                    VerticalOptions = LayoutOptions.Center
                };
                var rowLayout = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection
                    {
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = GridLength.Star }
                    },
                    ColumnSpacing = 12,
                    Padding = new Thickness(20, 14)
                };
                rowLayout.Add(dot, 0, 0);
                rowLayout.Add(lbl, 1, 0);
                var rowFrame = new Frame
                {
                    Padding = new Thickness(0),
                    BackgroundColor = Colors.White,
                    BorderColor = Colors.Transparent,
                    HasShadow = false,
                    Content = rowLayout
                };
                rowFrame.GestureRecognizers.Add(new TapGestureRecognizer
                { Command = new Command(() => UpdateSelection(idx)) });
                items.Add((dot, lbl, rowFrame));
                dialogContent.Children.Add(rowFrame);
                if (i < options.Length - 1)
                    dialogContent.Children.Add(new BoxView
                    {
                        HeightRequest = 1,
                        BackgroundColor = Color.FromArgb("#F0F0F0"),
                        Margin = new Thickness(20, 0)
                    });
            }

            dialogContent.Children.Add(new BoxView
            {
                HeightRequest = 1,
                BackgroundColor = Color.FromArgb("#E0E0E0"),
                Margin = new Thickness(0, 8, 0, 0)
            });

            var btnRow = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 0,
                Padding = new Thickness(16, 12)
            };
            var btnCancel = new Button
            {
                Text = "Hủy",
                BackgroundColor = Color.FromArgb("#ECF0F1"),
                TextColor = Color.FromArgb("#7F8C8D"),
                CornerRadius = 8,
                HeightRequest = 44,
                Margin = new Thickness(0, 0, 6, 0)
            };
            var btnConfirm = new Button
            {
                Text = "Tiếp tục",
                BackgroundColor = Color.FromArgb("#E74C3C"),
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 44,
                Margin = new Thickness(6, 0, 0, 0),
                IsEnabled = false,
                Opacity = 0.5
            };

            foreach (var (dot, lbl, rowFrame) in items)
            {
                var tap = (TapGestureRecognizer)rowFrame.GestureRecognizers[0];
                var origCmd = tap.Command;
                tap.Command = new Command(() =>
                {
                    origCmd.Execute(null);
                    btnConfirm.IsEnabled = true;
                    btnConfirm.Opacity = 1.0;
                });
            }

            btnCancel.Clicked += async (s, e) =>
            {
                await Navigation.PopModalAsync();
                tcs.TrySetResult(null);
            };
            btnConfirm.Clicked += async (s, e) =>
            {
                if (selected == null) return;
                string confirmMsg = selected switch
                {
                    var x when x.Contains("local") => "Xóa toàn bộ dữ liệu đã lưu trên máy?",
                    var x when x.Contains("server") => "Xóa toàn bộ dữ liệu trên server?",
                    _ => "Xóa toàn bộ dữ liệu local và server?"
                };
                await Navigation.PopModalAsync();
                var confirmed = await DisplayAlert("⚠️ Xác nhận xóa", confirmMsg, "Xóa", "Hủy");
                tcs.TrySetResult(confirmed ? selected : null);
            };

            btnRow.Add(btnCancel, 0, 0);
            btnRow.Add(btnConfirm, 1, 0);
            dialogContent.Children.Add(btnRow);
            dialogBox.Content = dialogContent;

            var overlay = new BoxView
            {
                BackgroundColor = Colors.Transparent,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };
            overlay.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    await Navigation.PopModalAsync();
                    tcs.TrySetResult(null);
                })
            });
            page.Content = new Grid { Children = { overlay, dialogBox } };
            await Navigation.PushModalAsync(page, animated: false);
        });
        return tcs.Task;
    }
    // ── Week event detail ─────────────────────────────────────
    private async Task ShowWeekEventDetailAsync(DateTime date)
    {
        var key = date.Date;
        if (!_vm.Events.ContainsKey(key)) return;
        var events = _vm.Events[key];

        _isShowingWeekDetail = true;

        var page = new ContentPage { BackgroundColor = Color.FromArgb("#F5F7FA") };
        var stack = new VerticalStackLayout { Spacing = 10, Padding = 15 };

        foreach (var evt in events)
        {
            var isExam = evt.Type == "exam";
            var frame = new Frame
            {
                BackgroundColor = Colors.White,
                BorderColor = isExam
                    ? Color.FromArgb("#E74C3C")
                    : Color.FromArgb("#27AE60"),
                CornerRadius = 8,
                Padding = 12,
                HasShadow = true
            };
            frame.Content = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label { Text = isExam ? "📝 THI" : "📚 HỌC",
                        FontSize = 12, FontAttributes = FontAttributes.Bold,
                        TextColor = isExam
                            ? Color.FromArgb("#E74C3C")
                            : Color.FromArgb("#27AE60") },
                    new Label { Text = evt.Title,
                        FontSize = 15, FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#2C3E50"),
                        LineBreakMode = LineBreakMode.WordWrap },
                    new Label { Text = $"⏰ {evt.Period}",
                        FontSize = 13, TextColor = Color.FromArgb("#7F8C8D") },
                    new Label { Text = $"📍 {evt.Location}",
                        FontSize = 13, TextColor = Color.FromArgb("#7F8C8D") },
                    new Label { Text = $"👨‍🏫 {evt.Teacher}",
                        FontSize = 13, TextColor = Color.FromArgb("#7F8C8D") },
                }
            };
            stack.Children.Add(frame);
        }

        var closeBtn = new Button
        {
            Text = "✕ Đóng",
            BackgroundColor = Color.FromArgb("#E74C3C"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 45,
            Margin = new Thickness(0, 10, 0, 0)
        };

        closeBtn.Clicked += async (s, e) =>
        {
            await Navigation.PopModalAsync();
            _isShowingWeekDetail = false;
            WeekViewControl.ResetSelectedFrame();
        };

        stack.Children.Add(closeBtn);
        page.Content = new ScrollView { Content = stack };
        await Navigation.PushModalAsync(page);
    }
    private async void OnViewAllClicked(object sender, EventArgs e)
    {
        var key = _selectedDate.Date;
        if (!_vm.Events.ContainsKey(key) || !_vm.Events[key].Any())
        {
            await DisplayAlert("Thông báo",
                $"Không có lịch ngày {_selectedDate:dd/MM/yyyy}", "OK");
            return;
        }
        await ShowWeekEventDetailAsync(_selectedDate);
    }
}