namespace StandUpReminder.Services;

public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StandUpReminder");

    public static string Records { get; } = Path.Combine(Root, "records");
    public static string SettingsFile { get; } = Path.Combine(Root, "settings.json");
    public static string StateFile { get; } = Path.Combine(Root, "state.json");

    public static void Ensure()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Records);
    }
}
