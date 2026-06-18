using StandUpReminder.Models;

namespace StandUpReminder.Services;

public sealed class SettingsManager
{
    public AppSettings Load()
    {
        AppPaths.Ensure();
        var settings = JsonFile.Read<AppSettings>(AppPaths.SettingsFile) ?? new AppSettings();
        Normalize(settings);
        Save(settings);
        return settings;
    }

    public void Save(AppSettings settings)
    {
        Normalize(settings);
        JsonFile.Write(AppPaths.SettingsFile, settings);
    }

    private static void Normalize(AppSettings settings)
    {
        settings.IntervalMinutes = Math.Clamp(settings.IntervalMinutes, 5, 240);
        settings.BreakMinutes = Math.Clamp(settings.BreakMinutes, 1, 60);
        settings.DeferMinutes = settings.DeferMinutes is 5 or 10 or 15 ? settings.DeferMinutes : 10;

        settings.WorkPeriods = settings.WorkPeriods
            .Where(p => TimeOnly.TryParse(p.Start, out _) && TimeOnly.TryParse(p.End, out _) && p.StartTime <= p.EndTime)
            .OrderBy(p => p.StartTime)
            .ToList();

        if (settings.WorkPeriods.Count == 0)
        {
            settings.WorkPeriods.Add(new WorkPeriod { Start = "09:00", End = "18:00", Label = "工作" });
        }
    }
}
