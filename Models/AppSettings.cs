namespace StandUpReminder.Models;

public sealed class AppSettings
{
    public List<WorkPeriod> WorkPeriods { get; set; } =
    [
        new() { Start = "09:00", End = "12:00", Label = "上午" },
        new() { Start = "13:00", End = "18:00", Label = "下午" }
    ];

    public int IntervalMinutes { get; set; } = 60;
    public int BreakMinutes { get; set; } = 5;
    public int DeferMinutes { get; set; } = 10;
    public bool AutoStartEnabled { get; set; } = true;
}

public sealed class WorkPeriod
{
    public string Start { get; set; } = "09:00";
    public string End { get; set; } = "18:00";
    public string Label { get; set; } = "";

    public TimeOnly StartTime => TimeOnly.TryParse(Start, out var value) ? value : new TimeOnly(9, 0);
    public TimeOnly EndTime => TimeOnly.TryParse(End, out var value) ? value : new TimeOnly(18, 0);
}
