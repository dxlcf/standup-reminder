namespace StandUpReminder.Models;

public sealed class RuntimeState
{
    public DateTime? NextReminderTime { get; set; }
    public string CurrentStatus { get; set; } = "waiting";
    public string LastActiveDate { get; set; } = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
}

public sealed record PendingReminder(DateTime OriginalDueTime, DateTime ActualPopupTime, bool CanDefer, bool Deferred);

public sealed record SchedulerSnapshot(
    string Status,
    DateTime Now,
    DateTime? NextReminderTime,
    WorkPeriod? CurrentPeriod,
    DateTime? PauseUntil,
    double Progress);
