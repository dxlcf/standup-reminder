using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StandUpReminder.Models;
using StandUpReminder.Services;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace StandUpReminder;

public sealed partial class MainWindow : Window
{
    private readonly SettingsManager _settingsManager = new();
    private readonly StateManager _stateManager = new();
    private readonly RecordStore _recordStore = new();
    private readonly ObservableCollection<WorkPeriod> _workPeriods = [];
    private readonly Brush _accentBrush;
    private readonly Brush _accentSoftBrush;
    private readonly Brush _mutedBrush;
    private readonly Brush _transparentBrush = Brushes.Transparent;
    private AppSettings _settings;
    private Scheduler _scheduler;
    private TrayManager? _trayManager;
    private DateOnly _recordDate = DateOnly.FromDateTime(DateTime.Today);
    private bool _isExiting;

    public MainWindow()
    {
        InitializeComponent();
        _accentBrush = (Brush)FindResource("AccentBrush");
        _accentSoftBrush = (Brush)FindResource("AccentSoftBrush");
        _mutedBrush = (Brush)FindResource("MutedBrush");

        _recordStore.CleanupOldRecords();
        _settings = _settingsManager.Load();
        if (!HasCommandLineSwitch("--skip-autostart-sync"))
        {
            ApplyAutoStartSetting(showError: false);
        }
        _scheduler = new Scheduler(_settings, _stateManager);
        _scheduler.ReminderDue += ShowReminder;
        _scheduler.SnapshotChanged += UpdateSnapshot;
        _scheduler.Start();

        WorkPeriodsList.ItemsSource = _workPeriods;
        LoadSettingsIntoView();
        LoadStats();
        LoadRecords();
        ShowDashboard();
        OpenRequestedView();

        _trayManager = new TrayManager(
            OpenMainWindow,
            () => Dispatcher.Invoke(() => _scheduler.TriggerNow()),
            () => Dispatcher.Invoke(PauseForThirtyMinutes),
            () => Dispatcher.Invoke(ShowRecords),
            () => Dispatcher.Invoke(ExitApplication));
    }

    private void ShowReminder(PendingReminder reminder)
    {
        Dispatcher.Invoke(() =>
        {
            var window = new ReminderWindow(reminder, _settings.BreakMinutes * 60);
            window.ShowDialog();

            if (window.Result == ReminderResult.Deferred)
            {
                _scheduler.Defer(reminder);
                _trayManager?.ShowBalloon("已延后提醒", $"{_settings.DeferMinutes} 分钟后再叫你起身。");
                return;
            }

            var actualPopupTime = DateTime.Now;
            var status = window.Result == ReminderResult.Completed ? "completed" : "early_done";
            _recordStore.AddSession(reminder.OriginalDueTime, new ReminderRecord
            {
                OriginalDueTime = reminder.OriginalDueTime.ToString("HH:mm"),
                ActualPopupTime = actualPopupTime.ToString("HH:mm"),
                PlannedRestSeconds = _settings.BreakMinutes * 60,
                ActualRestSeconds = window.ActualRestSeconds,
                Score = window.Score,
                Status = status,
                Deferred = reminder.Deferred
            });

            _scheduler.Complete(reminder);
            LoadStats();
            if (_recordDate == DateOnly.FromDateTime(reminder.OriginalDueTime))
            {
                LoadRecords();
            }
        });
    }

    private void UpdateSnapshot(SchedulerSnapshot snapshot)
    {
        Dispatcher.Invoke(() =>
        {
            CountdownRing.Progress = snapshot.Progress;

            if (snapshot.Status == "paused")
            {
                StatusDot.Fill = Brushes.Goldenrod;
                StatusText.Text = "已暂停";
                PeriodText.Text = snapshot.PauseUntil is null ? "暂停中" : $"暂停至 {snapshot.PauseUntil:HH:mm}";
                RingStatusLabel.Text = "暂停剩余";
                RemainingText.Text = FormatCountdown(snapshot.PauseUntil - snapshot.Now);
                ExpectedText.Text = "暂停结束后重新计算提醒";
                return;
            }

            if (snapshot.CurrentPeriod is null)
            {
                StatusDot.Fill = _mutedBrush;
                StatusText.Text = "非工作时间";
                var nextStart = GetNextWorkPeriodStart(snapshot.Now);
                PeriodText.Text = nextStart is null ? "暂无工作时段" : $"下个工作时段 {nextStart.Value:HH:mm} 开始";
                RingStatusLabel.Text = "当前为非工作时间";
                RemainingText.Text = nextStart is null ? "--:--" : FormatCountdown(nextStart.Value - snapshot.Now);
                ExpectedText.Text = snapshot.NextReminderTime is null ? "不会触发提醒" : $"预计 {snapshot.NextReminderTime:HH:mm} 提醒";
                return;
            }

            StatusDot.Fill = _accentBrush;
            StatusText.Text = "工作中";
            PeriodText.Text = $"{snapshot.CurrentPeriod.Label}时段 {snapshot.CurrentPeriod.Start} - {snapshot.CurrentPeriod.End}";
            RingStatusLabel.Text = "距离下次提醒";
            RemainingText.Text = FormatCountdown(snapshot.NextReminderTime - snapshot.Now);
            ExpectedText.Text = snapshot.NextReminderTime is null ? "暂无下一次提醒" : $"预计 {snapshot.NextReminderTime:HH:mm} 提醒";
        });
    }

    private void LoadSettingsIntoView()
    {
        _workPeriods.Clear();
        foreach (var period in _settings.WorkPeriods)
        {
            _workPeriods.Add(new WorkPeriod { Start = period.Start, End = period.End, Label = period.Label });
        }

        IntervalTextBox.Text = _settings.IntervalMinutes.ToString();
        BreakTextBox.Text = _settings.BreakMinutes.ToString();
        AutoStartCheckBox.IsChecked = _settings.AutoStartEnabled;
        foreach (ComboBoxItem item in DeferComboBox.Items)
        {
            if ((string)item.Content == _settings.DeferMinutes.ToString())
            {
                DeferComboBox.SelectedItem = item;
                break;
            }
        }
    }

    private void LoadStats()
    {
        var stats = _recordStore.GetStats(DateOnly.FromDateTime(DateTime.Today));
        TodayScoreText.Text = stats.AverageScore.ToString();
        TodayReminderCountText.Text = stats.ReminderCount.ToString();
        TodayCompletionRateText.Text = $"{stats.CompletionRate}%";
    }

    private void LoadRecords()
    {
        var stats = _recordStore.GetStats(_recordDate);
        var day = _recordStore.LoadDay(_recordDate);
        var today = DateOnly.FromDateTime(DateTime.Today);
        RecordDateText.Text = _recordDate == today ? $"{_recordDate:yyyy-MM-dd} 今天" : $"{_recordDate:yyyy-MM-dd}";
        RecordScoreText.Text = stats.AverageScore.ToString();
        RecordCountText.Text = stats.ReminderCount.ToString();
        RecordCompletedText.Text = stats.CompletedCount.ToString();
        RecordDeferredText.Text = stats.DeferredCount.ToString();
        RecordsGrid.ItemsSource = day.Sessions.Select(s => new RecordRow(s)).ToList();
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromView();
        MessageBox.Show("设置已保存，下一次提醒时间已重新计算。", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ResetDefaultsButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "恢复默认设置会重置工作时段、提醒间隔、休息时长、延后时间和开机自启，不会删除休息记录。确认恢复吗？",
            "恢复默认设置",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _settings = new AppSettings();
        _settingsManager.Save(_settings);
        _settings = _settingsManager.Load();
        ApplyAutoStartSetting(showError: true);
        LoadSettingsIntoView();
        _scheduler.RefreshSettings(_settings);
        MessageBox.Show("已恢复默认设置。", "恢复完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ClearDataButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "清空数据会删除所有休息记录和运行状态，但会保留当前设置。此操作不可撤销，确认清空吗？",
            "清空数据",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _recordStore.ClearAll();
        _stateManager.Clear();
        _scheduler.RefreshSettings(_settings);
        LoadStats();
        LoadRecords();
        MessageBox.Show("休息记录和运行状态已清空。", "清空完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveSettingsFromView()
    {
        var interval = ParsePositiveInt(IntervalTextBox.Text, _settings.IntervalMinutes);
        var breakMinutes = ParsePositiveInt(BreakTextBox.Text, _settings.BreakMinutes);
        var defer = DeferComboBox.SelectedItem is ComboBoxItem selected
            ? ParsePositiveInt(selected.Content?.ToString() ?? "10", 10)
            : 10;

        _settings = new AppSettings
        {
            WorkPeriods = _workPeriods.Select(p => new WorkPeriod { Start = p.Start.Trim(), End = p.End.Trim(), Label = string.IsNullOrWhiteSpace(p.Label) ? "工作" : p.Label.Trim() }).ToList(),
            IntervalMinutes = interval,
            BreakMinutes = breakMinutes,
            DeferMinutes = defer,
            AutoStartEnabled = AutoStartCheckBox.IsChecked == true
        };

        _settingsManager.Save(_settings);
        _settings = _settingsManager.Load();
        ApplyAutoStartSetting(showError: true);
        LoadSettingsIntoView();
        _scheduler.RefreshSettings(_settings);
    }

    private void AddPeriodButton_Click(object sender, RoutedEventArgs e)
    {
        _workPeriods.Add(new WorkPeriod { Start = "18:30", End = "20:00", Label = "加班" });
    }

    private void DeletePeriodButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WorkPeriod period })
        {
            _workPeriods.Remove(period);
        }
    }

    private void ImmediateRestButton_Click(object sender, RoutedEventArgs e)
    {
        _scheduler.TriggerNow();
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        PauseForThirtyMinutes();
    }

    private void PauseForThirtyMinutes()
    {
        _scheduler.Pause(TimeSpan.FromMinutes(30));
        _trayManager?.ShowBalloon("提醒已暂停", "30 分钟后恢复。");
    }

    private void DashboardNavButton_Click(object sender, RoutedEventArgs e) => ShowDashboard();
    private void RecordsNavButton_Click(object sender, RoutedEventArgs e) => ShowRecords();
    private void SettingsNavButton_Click(object sender, RoutedEventArgs e) => ShowSettings();

    private void PreviousDateButton_Click(object sender, RoutedEventArgs e)
    {
        var min = DateOnly.FromDateTime(DateTime.Today.AddDays(-29));
        if (_recordDate > min)
        {
            _recordDate = _recordDate.AddDays(-1);
            LoadRecords();
        }
    }

    private void NextDateButton_Click(object sender, RoutedEventArgs e)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (_recordDate < today)
        {
            _recordDate = _recordDate.AddDays(1);
            LoadRecords();
        }
    }

    private void ShowDashboard()
    {
        DashboardView.Visibility = Visibility.Visible;
        SettingsView.Visibility = Visibility.Collapsed;
        RecordsView.Visibility = Visibility.Collapsed;
        MarkNavigation(DashboardNavButton);
        LoadStats();
    }

    private void ShowSettings()
    {
        DashboardView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Visible;
        RecordsView.Visibility = Visibility.Collapsed;
        MarkNavigation(SettingsNavButton);
    }

    private void OpenRequestedView()
    {
        var viewArg = Environment.GetCommandLineArgs()
            .FirstOrDefault(arg => arg.StartsWith("--view=", StringComparison.OrdinalIgnoreCase));
        var view = viewArg?["--view=".Length..].Trim().ToLowerInvariant();

        if (view == "records")
        {
            ShowRecords();
        }
        else if (view == "settings")
        {
            ShowSettings();
        }
    }

    private static bool HasCommandLineSwitch(string switchName)
    {
        return Environment.GetCommandLineArgs()
            .Any(arg => string.Equals(arg, switchName, StringComparison.OrdinalIgnoreCase));
    }

    private void ShowRecords()
    {
        DashboardView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        RecordsView.Visibility = Visibility.Visible;
        MarkNavigation(RecordsNavButton);
        _recordDate = DateOnly.FromDateTime(DateTime.Today);
        LoadRecords();
        OpenMainWindow();
    }

    private void MarkNavigation(Button active)
    {
        foreach (var button in new[] { DashboardNavButton, RecordsNavButton, SettingsNavButton })
        {
            button.Background = ReferenceEquals(button, active) ? _accentSoftBrush : _transparentBrush;
            button.Foreground = ReferenceEquals(button, active) ? _accentBrush : _mutedBrush;
        }
    }

    private void OpenMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExiting)
        {
            _trayManager?.Dispose();
            return;
        }

        e.Cancel = true;
        Hide();
        _trayManager?.ShowBalloon("久坐提醒仍在运行", "我会继续在托盘里提醒你休息。");
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _trayManager?.Dispose();
        Application.Current.Shutdown();
    }

    private void ApplyAutoStartSetting(bool showError)
    {
        try
        {
            StartupManager.SetEnabled(_settings.AutoStartEnabled);
        }
        catch (Exception ex)
        {
            if (showError)
            {
                MessageBox.Show($"开机自启设置失败：{ex.Message}", "设置未完全保存", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private DateTime? GetNextWorkPeriodStart(DateTime now)
    {
        for (var dayOffset = 0; dayOffset <= 1; dayOffset++)
        {
            var date = DateOnly.FromDateTime(now.Date.AddDays(dayOffset));
            foreach (var period in _settings.WorkPeriods.OrderBy(p => p.StartTime))
            {
                var start = date.ToDateTime(period.StartTime);
                if (start > now)
                {
                    return start;
                }
            }
        }

        return null;
    }

    private static int ParsePositiveInt(string text, int fallback)
    {
        return int.TryParse(text, out var value) && value > 0 ? value : fallback;
    }

    private static string FormatCountdown(TimeSpan? span)
    {
        if (span is null)
        {
            return "--:--";
        }

        var value = span.Value <= TimeSpan.Zero ? TimeSpan.Zero : span.Value;
        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours:00}:{value.Minutes:00}"
            : $"{value.Minutes:00}:{value.Seconds:00}";
    }

    private sealed class RecordRow
    {
        public RecordRow(ReminderRecord record)
        {
            OriginalDueTime = record.OriginalDueTime;
            ActualPopupTime = record.ActualPopupTime;
            Duration = TimeSpan.FromSeconds(record.ActualRestSeconds).ToString(@"m\:ss");
            StatusText = record.Status == "completed" ? "完成" : "提前结束";
            DeferredText = record.Deferred ? "是" : "否";
            Score = record.Score;
        }

        public string OriginalDueTime { get; }
        public string ActualPopupTime { get; }
        public string Duration { get; }
        public string StatusText { get; }
        public string DeferredText { get; }
        public int Score { get; }
    }
}
