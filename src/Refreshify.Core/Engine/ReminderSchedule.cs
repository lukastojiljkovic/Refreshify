namespace Refreshify.Core.Engine;

/// <summary>When the reminder to run <i>Run all</i> is due.</summary>
public static class ReminderSchedule
{
    /// <summary>
    /// Due once <paramref name="months"/> have passed since the last full refresh or the last reminder, whichever is
    /// later. A PC that was never refreshed is due right away. Zero months turns the reminder off.
    /// </summary>
    public static bool IsDue(int months, DateTimeOffset now, DateTimeOffset? lastFullRefresh, DateTimeOffset? lastReminder)
    {
        if (months <= 0)
            return false;

        DateTimeOffset?[] times = [lastFullRefresh, lastReminder];
        return times.Max() is not { } since || now >= since.AddMonths(months);
    }
}
