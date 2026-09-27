using Refreshify.Core.Engine;

namespace Refreshify.Core.Tests.Engine;

public sealed class ReminderScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_off_reminder_is_never_due() =>
        Assert.False(ReminderSchedule.IsDue(0, Now, lastFullRefresh: null, lastReminder: null));

    [Fact]
    public void It_is_due_on_a_PC_that_was_never_refreshed() =>
        Assert.True(ReminderSchedule.IsDue(1, Now, lastFullRefresh: null, lastReminder: null));

    [Fact]
    public void It_waits_the_interval_after_the_last_full_refresh()
    {
        Assert.False(ReminderSchedule.IsDue(3, Now, lastFullRefresh: Now.AddMonths(-3).AddDays(1), lastReminder: null));
        Assert.True(ReminderSchedule.IsDue(3, Now, lastFullRefresh: Now.AddMonths(-3), lastReminder: null));
    }

    [Fact]
    public void A_reminder_starts_the_interval_again() =>
        Assert.False(ReminderSchedule.IsDue(1, Now, lastFullRefresh: Now.AddMonths(-5), lastReminder: Now.AddDays(-10)));
}
