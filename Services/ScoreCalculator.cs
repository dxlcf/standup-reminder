namespace StandUpReminder.Services;

public static class ScoreCalculator
{
    public static int Calculate(int actualRestSeconds, int plannedRestSeconds)
    {
        if (plannedRestSeconds <= 0)
        {
            return 0;
        }

        var score = (int)Math.Round(actualRestSeconds * 100d / plannedRestSeconds);
        return Math.Clamp(score, 0, 100);
    }
}
