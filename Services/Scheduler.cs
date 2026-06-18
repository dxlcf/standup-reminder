using System.Windows.Threading;
using StandUpReminder.Models;

namespace StandUpReminder.Services;

public sealed class Scheduler
{
    private readonly StateManager _stateManager;
    private readonly DispatcherTimer _timer;
    private RuntimeState _state;
    private AppSettings _settings;
    private bool _popupOpen;
    private DateTime? _pauseUntil;
    private PendingReminder? _deferredReminder;

    public Scheduler(AppSettings settings, StateManager stateManager)
    {
        _settings = settings;
        _stateManager = stateManager;
        _state = stateManager.Load();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
    }

    public event Action<PendingReminder>? ReminderDue;
    public event Action<SchedulerSnapshot>? SnapshotChanged;

    public DateTime? NextReminderTime => _state.NextReminderTime;

    public void Start()
    {
        if (_state.NextReminderTime is null || _state.NextReminderTime.Value.Date != DateTime.Today)
        {
            _state.NextReminderTime = CalculateNextReminder(DateTime.Now);
            SaveState("waiting");
        }

        _timer.Start();
        Tick();
    }

    public void RefreshSettings(AppSettings settings)
    {
        _settings = settings;
        if (!_popupOpen)
        {
            _state.NextReminderTime = CalculateNextReminder(DateTime.Now);
            SaveState("waiting");
        }
    }

    public void TriggerNow()
    {
        if (_popupOpen)
        {
            return;
        }

        var now = DateTime.Now;
        _popupOpen = true;
        SaveState("popup");
        ReminderDue?.Invoke(new PendingReminder(now, now, true, false));
    }

    public void Pause(TimeSpan duration)
    {
        _pauseUntil = DateTime.Now.Add(duration);
        SaveState("paused");
        Tick();
    }

    public void Defer(PendingReminder reminder)
    {
        _popupOpen = false;
        var due = DateTime.Now.AddMinutes(_settings.DeferMinutes);
        _deferredReminder = reminder with { ActualPopupTime = due, CanDefer = false, Deferred = true };
        _state.NextReminderTime = due;
        SaveState("deferred");
        Tick();
    }

    public void Complete(PendingReminder reminder, bool resetFromNow = false)
    {
        _popupOpen = false;
        _deferredReminder = null;
        _state.NextReminderTime = resetFromNow
            ? CalculateNextReminder(DateTime.Now)
            : CalculateNextReminderAfter(reminder.OriginalDueTime);
        SaveState("waiting");
        Tick();
    }

    public WorkPeriod? GetCurrentPeriod(DateTime now)
    {
        var current = TimeOnly.FromDateTime(now);
        return _settings.WorkPeriods.FirstOrDefault(p => current >= p.StartTime && current <= p.EndTime);
    }

    public DateTime? CalculateNextReminder(DateTime from)
    {
        var candidate = CalculateNextReminderAfter(from);
        return candidate < from ? null : candidate;
    }

    public DateTime? CalculateNextReminderAfter(DateTime after)
    {
        for (var dayOffset = 0; dayOffset <= 1; dayOffset++)
        {
            var date = DateOnly.FromDateTime(after.Date.AddDays(dayOffset));
            foreach (var period in _settings.WorkPeriods.OrderBy(p => p.StartTime))
            {
                var start = date.ToDateTime(period.StartTime);
                var end = date.ToDateTime(period.EndTime);
                for (var due = start.AddMinutes(_settings.IntervalMinutes); due <= end; due = due.AddMinutes(_settings.IntervalMinutes))
                {
                    if (due > after)
                    {
                        return due;
                    }
                }
            }
        }

        return null;
    }

    private void Tick()
    {
        var now = DateTime.Now;
        var currentPeriod = GetCurrentPeriod(now);
        var isPaused = _pauseUntil is not null && _pauseUntil > now;

        if (_pauseUntil is not null && _pauseUntil <= now)
        {
            _pauseUntil = null;
            _state.NextReminderTime = CalculateNextReminder(now);
            SaveState("waiting");
        }

        if (!_popupOpen && !isPaused && currentPeriod is not null)
        {
            _state.NextReminderTime ??= CalculateNextReminder(now);
            if (_state.NextReminderTime is not null && now >= _state.NextReminderTime)
            {
                var due = _deferredReminder ?? new PendingReminder(_state.NextReminderTime.Value, now, true, false);
                _popupOpen = true;
                SaveState("popup");
                ReminderDue?.Invoke(due);
            }
        }
        else if (!_popupOpen && !isPaused && currentPeriod is null)
        {
            _state.NextReminderTime = CalculateNextReminder(now);
        }

        var progress = CalculateProgress(now, currentPeriod);
        var status = _popupOpen ? "popup" : isPaused ? "paused" : currentPeriod is null ? "outside_work_time" : "waiting";
        SnapshotChanged?.Invoke(new SchedulerSnapshot(status, now, _state.NextReminderTime, currentPeriod, _pauseUntil, progress));
    }

    private double CalculateProgress(DateTime now, WorkPeriod? currentPeriod)
    {
        if (currentPeriod is null || _state.NextReminderTime is null)
        {
            return 0;
        }

        var previous = _state.NextReminderTime.Value.AddMinutes(-_settings.IntervalMinutes);
        var total = (_state.NextReminderTime.Value - previous).TotalSeconds;
        var elapsed = (now - previous).TotalSeconds;
        return total <= 0 ? 0 : Math.Clamp(elapsed / total, 0, 1);
    }

    private void SaveState(string status)
    {
        _state.CurrentStatus = status;
        _stateManager.Save(_state);
    }
}
