using StandUpReminder.Models;

namespace StandUpReminder.Services;

public sealed class StateManager
{
    public RuntimeState Load()
    {
        AppPaths.Ensure();
        return JsonFile.Read<RuntimeState>(AppPaths.StateFile) ?? new RuntimeState();
    }

    public void Save(RuntimeState state)
    {
        state.LastActiveDate = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        JsonFile.Write(AppPaths.StateFile, state);
    }

    public void Clear()
    {
        if (File.Exists(AppPaths.StateFile))
        {
            File.Delete(AppPaths.StateFile);
        }
    }
}
