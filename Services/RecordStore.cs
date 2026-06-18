using StandUpReminder.Models;

namespace StandUpReminder.Services;

public sealed class RecordStore
{
    public RecordStore()
    {
        AppPaths.Ensure();
    }

    public void CleanupOldRecords()
    {
        var cutoff = DateOnly.FromDateTime(DateTime.Today.AddDays(-29));
        foreach (var file in Directory.EnumerateFiles(AppPaths.Records, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (DateOnly.TryParse(name, out var date) && date < cutoff)
            {
                File.Delete(file);
            }
        }
    }

    public DayRecord LoadDay(DateOnly date)
    {
        var path = GetPath(date);
        return JsonFile.Read<DayRecord>(path) ?? new DayRecord { Date = date.ToString("yyyy-MM-dd") };
    }

    public void AddSession(DateTime dateTime, ReminderRecord session)
    {
        var date = DateOnly.FromDateTime(dateTime);
        var day = LoadDay(date);
        day.Sessions.Add(session);
        JsonFile.Write(GetPath(date), day);
    }

    public void ClearAll()
    {
        foreach (var file in Directory.EnumerateFiles(AppPaths.Records, "*.json"))
        {
            File.Delete(file);
        }
    }

    public DailyStats GetStats(DateOnly date)
    {
        var sessions = LoadDay(date).Sessions;
        if (sessions.Count == 0)
        {
            return new DailyStats();
        }

        return new DailyStats
        {
            AverageScore = (int)Math.Round(sessions.Average(s => s.Score)),
            ReminderCount = sessions.Count,
            CompletedCount = sessions.Count(s => s.Status == "completed"),
            DeferredCount = sessions.Count(s => s.Deferred)
        };
    }

    private static string GetPath(DateOnly date) => Path.Combine(AppPaths.Records, $"{date:yyyy-MM-dd}.json");
}
