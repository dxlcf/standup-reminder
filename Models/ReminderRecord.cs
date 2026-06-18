namespace StandUpReminder.Models;

public sealed class ReminderRecord
{
    public string OriginalDueTime { get; set; } = "";
    public string ActualPopupTime { get; set; } = "";
    public int PlannedRestSeconds { get; set; }
    public int ActualRestSeconds { get; set; }
    public int Score { get; set; }
    public string Status { get; set; } = "completed";
    public bool Deferred { get; set; }
}

public sealed class DayRecord
{
    public string Date { get; set; } = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
    public List<ReminderRecord> Sessions { get; set; } = [];
}

public sealed class DailyStats
{
    public int AverageScore { get; init; }
    public int ReminderCount { get; init; }
    public int CompletedCount { get; init; }
    public int DeferredCount { get; init; }
    public int CompletionRate => ReminderCount == 0 ? 0 : (int)Math.Round(CompletedCount * 100d / ReminderCount);
}
