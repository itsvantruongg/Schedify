using Microsoft.Maui.Controls.Shapes;
using Test.Models;
using Test.ViewModels;

namespace Test.Views;

public partial class WeekView : ContentView
{
    private MainViewModel _vm;
    private DateTime _currentWeekStart;
    private DateTime _selectedDay;

    private const string PrefScrollY = "week_scroll_y";
    private const int SubRowsPerHour = 2;
    private const int RowHeightPx = 30;   // cao hơn để event card đẹp
    private const int TotalHours = 24;
    private const int TotalSubRows = TotalHours * SubRowsPerHour;

    private Border? _selectedWeekFrame = null;
    private Color? _selectedWeekOriginalColor = null;

    // FIX: dùng flag bool thay vì reset ngay lập tức
    private bool _suppressDatePickerEvent = false;
    private BoxView? _redLineDot = null;
    private BoxView? _redLineBar = null;
    private bool _staticGridBuilt = false;

    // Timer cập nhật đường đỏ mỗi phút
    private IDispatcherTimer? _currentTimeTimer;

    // Callback để show event detail — MainPage xử lý
    public Func<DateTime, Task>? OnShowEventDetail { get; set; }
    public WeekView()
    {
        InitializeComponent();
        WeekScrollView.Scrolled += (s, e) =>
            Preferences.Set(PrefScrollY, (float)e.ScrollY);
    }
    public void Initialize(MainViewModel vm, DateTime weekStart)
    {
        _vm = vm;
        _selectedDay = DateTime.Today;
        _currentWeekStart = GetMonday(DateTime.Today);
        _staticGridBuilt = false; // reset để build lưới tĩnh lại
        GenerateWeekView();
        UpdateLabels();
        StartCurrentTimeTimer();
    }

    public void Refresh()
    {
        if (_vm == null) return;
        RebuildHeader();
        RebuildDynamicBody(); // không rebuild lưới tĩnh
    }
    // ── Timer đường đỏ ────────────────────────────────────────
    private void StartCurrentTimeTimer()
    {
        _currentTimeTimer?.Stop();
        _currentTimeTimer = Dispatcher.CreateTimer();
        _currentTimeTimer.Interval = TimeSpan.FromMinutes(1);
        _currentTimeTimer.Tick += (s, e) => UpdateCurrentTimeLine();
        _currentTimeTimer.Start();
    }
    private void UpdateCurrentTimeLine()
    {
        MainThread.BeginInvokeOnMainThread(() => DrawCurrentTimeLine());
    }
    private void DrawCurrentTimeLine()
    {
        RemoveCurrentTimeLine(); // dùng tham chiếu, không LINQ

        if (_selectedDay.Date != DateTime.Today) return;

        var now = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Local);
        double totalMins = now.Hour * 60.0 + now.Minute;
        double rowsPerMin = SubRowsPerHour / 60.0;
        int subRow = (int)Math.Round(totalMins * rowsPerMin);
        subRow = Math.Max(0, Math.Min(subRow, TotalSubRows - 1));

        _redLineDot = new BoxView
        {
            Color = Color.FromArgb("#E53935"),
            BackgroundColor = Colors.Transparent,
            CornerRadius = 5,
            WidthRequest = 10,
            HeightRequest = 10,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, (RowHeightPx - 10) / 2.0, 1, 0),
            InputTransparent = true
        };
        WeekBodyGrid.Add(_redLineDot, 0, subRow);

        _redLineBar = new BoxView
        {
            Color = Color.FromArgb("#E53935"),
            HeightRequest = 2,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, (RowHeightPx - 2) / 2.0, 0, 0),
            InputTransparent = true
        };
        WeekBodyGrid.Add(_redLineBar, 1, subRow);
    }
    // ── Navigation ────────────────────────────────────────────
    private void OnPreviousWeekClicked(object sender, EventArgs e)
    {
        _currentWeekStart = _currentWeekStart.AddDays(-7);
        _selectedDay = _selectedDay.AddDays(-7);
        GenerateWeekView(); UpdateLabels();
    }
    private void OnNextWeekClicked(object sender, EventArgs e)
    {
        _currentWeekStart = _currentWeekStart.AddDays(7);
        _selectedDay = _selectedDay.AddDays(7);
        GenerateWeekView(); UpdateLabels();
    }
    private void OnTodayClicked(object sender, EventArgs e)
    {
        _currentWeekStart = GetMonday(DateTime.Today);
        _selectedDay = DateTime.Today;
        GenerateWeekView(); UpdateLabels();
    }
    // FIX: Handler chỉ chạy khi _suppressDatePickerEvent = false
    // UpdateLabels set flag = true TRƯỚC khi thay đổi Date,
    // handler sẽ đọc flag, nếu true thì reset về false và return (bỏ qua).
    private void OnWeekDatePickerSelected(object sender, DateChangedEventArgs e)
    {
        if (_suppressDatePickerEvent)
        {
            _suppressDatePickerEvent = false; // reset tại đây — cho lần sau
            return;
        }

        // Đây là lựa chọn thực sự của user
        _selectedDay = e.NewDate;                   // FIX: dùng đúng ngày user chọn
        _currentWeekStart = GetMonday(e.NewDate);   // tuần chứa ngày đó
        GenerateWeekView();
        UpdateLabels();
    }
    private void UpdateLabels()
    {
        // FIX: set flag TRƯỚC khi gán Date — đảm bảo handler bị suppress
        _suppressDatePickerEvent = true;
        WeekDatePicker.Date = _currentWeekStart;
        // KHÔNG reset _suppressDatePickerEvent ở đây —
        // handler OnWeekDatePickerSelected sẽ reset khi nó chạy.
        // Nếu Date không thay đổi (event không fire), flag vô hại vì
        // lần tương tác tiếp theo của user sẽ là một Date khác.

        var weekEnd = _currentWeekStart.AddDays(6);
        WeekMonthLabel.Text = _selectedDay.ToString("'Tháng' M'/' yyyy");

        var thisMonday = GetMonday(DateTime.Today);
        int diffWeeks = (int)Math.Round((_currentWeekStart - thisMonday).TotalDays / 7.0);

        WeekSubLabel.Text = diffWeeks switch
        {
            0 => "Tuần này",
            1 => "Tuần sau",
            -1 => "Tuần trước",
            _ => $"{_currentWeekStart:dd/MM} – {weekEnd:dd/MM}"
        };
    }
    private static DateTime GetMonday(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }
    // ── Chọn ngày từ header ────────────────────────────────────
    private void SelectDay(DateTime day)
    {
        _selectedDay = day;
        RebuildHeader(); // cần cập nhật pill selected
        RebuildDynamicBody(); // chỉ rebuild events + đường đỏ
    }
    // ── GenerateWeekView ──────────────────────────────────────
    private void GenerateWeekView()
    {
        _selectedWeekFrame = null;
        _selectedWeekOriginalColor = null;
        RebuildHeader();
        RebuildBody();
    }
    // ── Header 7 ngày ─────────────────────────────────────────
    private void RebuildHeader()
    {
        WeekHeaderGrid.Children.Clear();
        WeekHeaderGrid.RowDefinitions.Clear();
        WeekHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
        WeekHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = 1 });

        var dayNames = new[] { "T2", "T3", "T4", "T5", "T6", "T7", "CN" };

        for (int d = 0; d < 7; d++)
        {
            var dayDate = _currentWeekStart.AddDays(d);
            bool isToday = dayDate.Date == DateTime.Today;
            bool isSelected = dayDate.Date == _selectedDay.Date;

            var pillBg = isSelected ? Color.FromArgb("#E3F2FD") : Colors.Transparent;
            var textColor = isSelected ? Color.FromArgb("#5f6368")
                          : isToday ? Color.FromArgb("#3498DB")
                          : Color.FromArgb("#5f6368");

            var dayCopy = dayDate;

            // ── Tên thứ ────────────────────────────────────────────
            var nameLbl = new Label
            {
                Text = dayNames[d],
                FontSize = 11,
                TextColor = textColor,
                HorizontalTextAlignment = TextAlignment.Center,
                HorizontalOptions = LayoutOptions.Fill,
                InputTransparent = true,  // bubble tap lên cellGrid
                Padding = new Thickness(0, 0, 0, 0)
            };

            // ── Số ngày: dùng Label trực tiếp, không dùng Frame ────
            // Frame trên Android chặn tap gesture của parent
            var numLbl = new Label
            {
                Text = dayDate.Day.ToString(),
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = textColor,
                BackgroundColor = pillBg,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                WidthRequest = 34,
                HeightRequest = 34,
                Padding = new Thickness(0, 0, 0, 0),
                InputTransparent = true   // bubble lên cellGrid
            };

            // Bo tròn pill cho numLbl bằng Border 
            var numBorder = new Border
            {
                BackgroundColor = pillBg,
                StrokeThickness = 0,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                WidthRequest = 34,
                HeightRequest = 34,
                InputTransparent = true,   // ← bubble tap lên cellGrid
                Content = numLbl,
                StrokeShape = new RoundRectangle { CornerRadius = 17 }
            };

            var dayStack = new VerticalStackLayout
            {
                Spacing = 0,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center,
                InputTransparent = true,   // ← bubble lên cellGrid
                Children = { nameLbl, numBorder }
            };

            // ── Chấm event theo loại lịch ──────────────────────────
            bool hasEvents = _vm?.Events.ContainsKey(dayDate.Date) == true
                             && _vm.Events[dayDate.Date].Count > 0;

            if (hasEvents && !isSelected)
            {
                var types = _vm.Events[dayDate.Date].Select(e => e.Type).ToList();
                bool hasClass = types.Contains("class");
                bool hasExam = types.Contains("exam");

                var dotRow = new HorizontalStackLayout
                {
                    Spacing = 3,
                    HorizontalOptions = LayoutOptions.Center,
                    Padding = new Thickness(0, -5, 0, 0),
                    InputTransparent = true
                };

                if (hasClass) dotRow.Children.Add(MakeDot("#2196F3")); // xanh
                if (hasExam) dotRow.Children.Add(MakeDot("#E67E22")); // cam

                dayStack.Children.Add(dotRow);
            }

            // ── Cell Grid: ĐÂY là nơi bắt tap duy nhất ─────────────
            var cellGrid = new Grid
            {
                BackgroundColor = Colors.Transparent,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Padding = new Thickness(2, 4)
            };
            cellGrid.Children.Add(dayStack);

            // Tap CHỈ gắn trên cellGrid — các con đều InputTransparent=true
            cellGrid.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() => SelectDay(dayCopy))
            });

            WeekHeaderGrid.Add(cellGrid, d, 0);
        }

        // Gạch dưới
        var separator = new BoxView
        {
            Color = Color.FromArgb("#dadce0"),
            HeightRequest = 1,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = true
        };
        WeekHeaderGrid.Add(separator, 0, 1);
        Grid.SetColumnSpan(separator, 7);
    }
    // ── Body: cột giờ + ngày được chọn ────────────────────────
    private void RebuildBody()
    {
        if (!_staticGridBuilt)
        {
            WeekBodyGrid.Children.Clear();
            WeekBodyGrid.RowDefinitions.Clear();

            for (int r = 0; r < TotalSubRows; r++)
                WeekBodyGrid.RowDefinitions.Add(new RowDefinition { Height = RowHeightPx });

            // Nhãn giờ + đường kẻ ngang
            for (int h = 0; h < TotalHours; h++)
            {
                int subRow = h * SubRowsPerHour;
                if (h >= 1)
                {
                    var timeLbl = new Label
                    {
                        Text = $"{h:D2}:00",
                        FontSize = 10,
                        TextColor = Color.FromArgb("#70757a"),
                        HorizontalTextAlignment = TextAlignment.End,
                        VerticalTextAlignment = TextAlignment.Center,
                        BackgroundColor = Colors.Transparent,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        Padding = new Thickness(0, 0, 6, 0)
                    };
                    WeekBodyGrid.Add(timeLbl, 0, subRow - 1);
                    Grid.SetRowSpan(timeLbl, 2);

                    var hLine = new BoxView
                    {
                        Color = Color.FromArgb("#e8eaed"),
                        HeightRequest = 1,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Start
                    };
                    WeekBodyGrid.Add(hLine, 1, subRow);
                }
            }

            // Đường dọc phân cách cột giờ
            var vLine = new BoxView
            {
                Color = Color.FromArgb("#dadce0"),
                WidthRequest = 1,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Fill
            };
            WeekBodyGrid.Add(vLine, 0, 0);
            Grid.SetRowSpan(vLine, TotalSubRows);

            // TỐI ƯU: Swipe chỉ gắn 1 lần trên grid, không gắn trên từng View con
            AddDaySwipe(WeekBodyGrid);

            _staticGridBuilt = true;
        }

        RebuildDynamicBody();
    }
    private void RebuildDynamicBody()
    {
        // Xóa đường đỏ cũ (bằng tham chiếu)
        RemoveCurrentTimeLine();

        // Xóa chỉ event cards (Border có Margin nhận dạng)
        var toRemove = WeekBodyGrid.Children
            .OfType<Border>()
            .ToList();
        foreach (var v in toRemove)
            WeekBodyGrid.Children.Remove(v);

        // Vẽ lại events của ngày được chọn
        if (_vm?.Events.TryGetValue(_selectedDay.Date, out var dayEvents) == true)
        {
            foreach (var evt in dayEvents)
            {
                var (startDt, endDt) = PeriodTimes.ParsePeriodToDateTime(evt.Period, _selectedDay);
                double startMins = startDt.Hour * 60.0 + startDt.Minute;
                double endMins = endDt.Hour * 60.0 + endDt.Minute;
                double rowsPerMin = SubRowsPerHour / 60.0;

                int subRowStart = (int)Math.Round(startMins * rowsPerMin);
                int subRowEnd = (int)Math.Round(endMins * rowsPerMin);
                int rowSpan = Math.Max(2, subRowEnd - subRowStart);
                subRowStart = Math.Max(0, Math.Min(subRowStart, TotalSubRows - 1));
                rowSpan = Math.Max(1, Math.Min(rowSpan, TotalSubRows - subRowStart));

                bool isExam = evt.Type == "exam";
                var evtCopy = evt;

                var bgColor = isExam ? Color.FromArgb("#FFF3E0") : Color.FromArgb("#E3F2FD");
                var accentColor = isExam ? Color.FromArgb("#E67E22") : Color.FromArgb("#2196F3");
                var tagBgColor = isExam ? Color.FromArgb("#FFE0B2") : Color.FromArgb("#BBDEFB");
                var tagText = isExam ? "Lịch thi" : "Lịch học";

                var tagLabel = new Label
                {
                    Text = tagText,
                    FontSize = 9,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = accentColor,
                    BackgroundColor = tagBgColor,
                    Padding = new Thickness(5, 2),
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                };
                var tagFrame = new Frame
                {
                    BackgroundColor = tagBgColor,
                    BorderColor = Colors.Transparent,
                    CornerRadius = 10,
                    Padding = new Thickness(0),
                    HasShadow = false,
                    Content = tagLabel,
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Start
                };

                var titleLbl = new Label
                {
                    Text = evt.Title,
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#1A237E"),
                    LineBreakMode = LineBreakMode.WordWrap,
                    HorizontalOptions = LayoutOptions.Fill
                };

                var headerGrid = new Grid
                {
                    ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                    ColumnSpacing = 4
                };
                headerGrid.Add(titleLbl, 0, 0);
                headerGrid.Add(tagFrame, 1, 0);

                var timeStr = $"{startDt:HH:mm}  –  {endDt:HH:mm}";
                var timeRow = new HorizontalStackLayout
                {
                    Spacing = 4,
                    Children =
                {
                    new Label { Text = "🕐", FontSize = 11, TextColor = Color.FromArgb("#78909C"), VerticalOptions = LayoutOptions.Center },
                    new Label { Text = timeStr, FontSize = 11, TextColor = Color.FromArgb("#455A64"), VerticalOptions = LayoutOptions.Center }
                }
                };

                var hasLocation = !string.IsNullOrWhiteSpace(evt.Location);
                var locationRow = hasLocation ? new HorizontalStackLayout
                {
                    Spacing = 4,
                    Children =
                {
                    new Label { Text = "📍", FontSize = 11, TextColor = Color.FromArgb("#78909C"), VerticalOptions = LayoutOptions.Center },
                    new Label { Text = evt.Location, FontSize = 11, TextColor = Color.FromArgb("#455A64"), LineBreakMode = LineBreakMode.TailTruncation, VerticalOptions = LayoutOptions.Center }
                }
                } : null;

                var hasTeacher = !string.IsNullOrWhiteSpace(evt.Teacher);
                var teacherRow = hasTeacher ? new HorizontalStackLayout
                {
                    Spacing = 4,
                    Children =
                {
                    new Label { Text = "👨‍🏫", FontSize = 11, TextColor = Color.FromArgb("#78909C"), VerticalOptions = LayoutOptions.Center },
                    new Label { Text = evt.Teacher, FontSize = 11, TextColor = Color.FromArgb("#455A64"), LineBreakMode = LineBreakMode.TailTruncation, VerticalOptions = LayoutOptions.Center }
                }
                } : null;

                var contentStack = new VerticalStackLayout { Spacing = 3, Children = { headerGrid, timeRow } };
                if (locationRow != null) contentStack.Children.Add(locationRow);
                if (teacherRow != null) contentStack.Children.Add(teacherRow);

                var contentPad = new ContentView
                {
                    Padding = new Thickness(15),
                    Content = contentStack,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill
                };

                var mainCard = new Border
                {
                    BackgroundColor = bgColor,
                    StrokeThickness = 0,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Margin = new Thickness(3, 0, 0, 0),
                    Content = contentPad,
                    StrokeShape = new RoundRectangle { CornerRadius = 30 }
                };
                mainCard.Shadow = new Shadow
                {
                    Brush = new SolidColorBrush(Color.FromArgb("#25000000")),
                    Offset = new Point(2, 2),
                    Radius = 6
                };

                var accentCard = new Border
                {
                    BackgroundColor = accentColor,
                    StrokeThickness = 0,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    StrokeShape = new RoundRectangle { CornerRadius = 30 }
                };

                var stackGrid = new Grid
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    IsClippedToBounds = false
                };
                stackGrid.Add(accentCard);
                stackGrid.Add(mainCard);

                var capturedFrame = new Border
                {
                    BackgroundColor = Colors.Transparent,
                    StrokeThickness = 0,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Margin = new Thickness(6, 3, 10, 3),
                    Content = stackGrid,
                    StrokeShape = new RoundRectangle { CornerRadius = 30 }
                };

                var originalBg = bgColor;
                capturedFrame.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(async () =>
                    {
                        if (_selectedWeekFrame != null && _selectedWeekOriginalColor != null)
                            _selectedWeekFrame.BackgroundColor = _selectedWeekOriginalColor;
                        _selectedWeekFrame = mainCard;
                        _selectedWeekOriginalColor = originalBg;
                        mainCard.BackgroundColor = accentColor.WithAlpha(0.35f);

                        if (OnShowEventDetail != null)
                            await OnShowEventDetail(evtCopy.StartTime.Date);
                    })
                });
                AddDaySwipe(capturedFrame);

                WeekBodyGrid.Add(capturedFrame, 1, subRowStart);
                if (rowSpan > 1) Grid.SetRowSpan(capturedFrame, rowSpan);
            }
        }

        DrawCurrentTimeLine();

        float savedScrollY = Preferences.Get(PrefScrollY, (float)(6 * SubRowsPerHour * RowHeightPx));
        _ = Task.Delay(150).ContinueWith(_ =>
            MainThread.BeginInvokeOnMainThread(async () =>
                await WeekScrollView.ScrollToAsync(0, savedScrollY, false)));
    }
    // ── Swipe trái/phải để đổi ngày ───────────────────────────
    private void AddDaySwipe(View view)
    {
        view.GestureRecognizers.Add(new SwipeGestureRecognizer
        {
            Direction = SwipeDirection.Left,
            Command = new Command(() =>
            {
                var next = _selectedDay.AddDays(1);
                if (next >= _currentWeekStart.AddDays(7))
                    _currentWeekStart = _currentWeekStart.AddDays(7);
                _selectedDay = next;
                GenerateWeekView();
                UpdateLabels();
            })
        });

        view.GestureRecognizers.Add(new SwipeGestureRecognizer
        {
            Direction = SwipeDirection.Right,
            Command = new Command(() =>
            {
                var prev = _selectedDay.AddDays(-1);
                if (prev < _currentWeekStart)
                    _currentWeekStart = _currentWeekStart.AddDays(-7);
                _selectedDay = prev;
                _staticGridBuilt = false; // reset khi đổi tuần
                GenerateWeekView();
                UpdateLabels();
            })
        });
    }
    public void ResetSelectedFrame()
    {
        if (_selectedWeekFrame != null && _selectedWeekOriginalColor != null)
        {
            _selectedWeekFrame.BackgroundColor = _selectedWeekOriginalColor;
            _selectedWeekFrame = null;
            _selectedWeekOriginalColor = null;
        }
    }
    // Thêm method mới — đi đến ngày bất kỳ
    public void GoToDay(DateTime day)
    {
        _selectedDay = day.Date;
        _currentWeekStart = GetMonday(day.Date);
        GenerateWeekView();
        UpdateLabels();
    }
    // Giữ nguyên GoToToday() — giờ chỉ gọi GoToDay(DateTime.Today)
    public void GoToToday()
    {
        GoToDay(DateTime.Today);
    }
    // Thêm helper method tạo chấm tròn — đặt ngoài RebuildHeader
    private static Border MakeDot(string hexColor)
    {
        return new Border
        {
            BackgroundColor = Color.FromArgb(hexColor),
            WidthRequest = 7,
            HeightRequest = 7,
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
            StrokeShape = new Ellipse()  // ← hình tròn thật sự
        };
    }
    private void RemoveCurrentTimeLine()
    {
        if (_redLineDot != null)
        {
            WeekBodyGrid.Children.Remove(_redLineDot);
            _redLineDot = null;
        }
        if (_redLineBar != null)
        {
            WeekBodyGrid.Children.Remove(_redLineBar);
            _redLineBar = null;
        }
    }
}