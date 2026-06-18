using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using StandUpReminder.Models;
using StandUpReminder.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace StandUpReminder;

public enum ReminderResult
{
    Completed,
    EarlyDone,
    Deferred
}

public sealed partial class ReminderWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly DateTime _startedAt;
    private readonly int _plannedSeconds;
    private bool _allowClose;

    public ReminderWindow(PendingReminder reminder, int plannedSeconds)
    {
        InitializeComponent();
        Reminder = reminder;
        _plannedSeconds = plannedSeconds;
        _startedAt = DateTime.Now;
        DeferButton.Visibility = reminder.CanDefer ? Visibility.Visible : Visibility.Collapsed;
        if (!reminder.CanDefer)
        {
            ActionButtonsGrid.Columns = 1;
        }

        PositionNearCursor();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateCountdown();
        _timer.Start();
        UpdateCountdown();
    }

    public PendingReminder Reminder { get; }
    public ReminderResult Result { get; private set; } = ReminderResult.Completed;
    public int ActualRestSeconds { get; private set; }
    public int Score { get; private set; }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Activate();
            return;
        }

        base.OnClosing(e);
    }

    private void UpdateCountdown()
    {
        ActualRestSeconds = Math.Clamp((int)(DateTime.Now - _startedAt).TotalSeconds, 0, _plannedSeconds);
        var remaining = Math.Max(0, _plannedSeconds - ActualRestSeconds);
        CountdownText.Text = TimeSpan.FromSeconds(remaining).ToString(@"mm\:ss");
        Score = ScoreCalculator.Calculate(ActualRestSeconds, _plannedSeconds);
        ScoreHintText.Text = remaining == 0 ? "休息完成，已获得 100 分" : $"当前结束可获得 {Score} 分";

        if (remaining <= 0)
        {
            Result = ReminderResult.Completed;
            ActualRestSeconds = _plannedSeconds;
            Score = 100;
            CloseAllowed();
        }
    }

    private void DeferButton_Click(object sender, RoutedEventArgs e)
    {
        Result = ReminderResult.Deferred;
        CloseAllowed();
    }

    private void FinishButton_Click(object sender, RoutedEventArgs e)
    {
        ActualRestSeconds = Math.Clamp((int)(DateTime.Now - _startedAt).TotalSeconds, 0, _plannedSeconds);
        var rested = TimeSpan.FromSeconds(ActualRestSeconds);
        var result = MessageBox.Show(
            $"才休息了 {rested.Minutes} 分 {rested.Seconds} 秒，再坐会儿呗？",
            "确认提前结束",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        Result = ActualRestSeconds >= _plannedSeconds ? ReminderResult.Completed : ReminderResult.EarlyDone;
        Score = ScoreCalculator.Calculate(ActualRestSeconds, _plannedSeconds);
        CloseAllowed();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || (e.SystemKey == Key.F4 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)))
        {
            e.Handled = true;
        }
    }

    private void CloseAllowed()
    {
        _timer.Stop();
        _allowClose = true;
        Close();
    }

    private void PositionNearCursor()
    {
        var bounds = NativeMethods.GetCursorMonitorWorkArea();
        Width = Math.Max(480, Math.Min(720, bounds.Width * 0.45));
        Height = Math.Max(360, bounds.Height * 0.5);
        Left = bounds.Left + (bounds.Width - Width) / 2;
        Top = bounds.Top + (bounds.Height - Height) / 2;
    }
}
