using Refreshify.Core.Diagnostics;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Tests.Tools;

public class DismTests
{
    [Theory]
    [InlineData("[===                        5.9%                           ]", 5.9)]
    [InlineData("[==========================100.0%==========================] The restore operation completed successfully.", 100.0)]
    [InlineData("[=====                      10,0%                          ]", 10.0)]
    [InlineData("Image Version: 10.0.26100.1", null)]
    [InlineData("The operation completed successfully.", null)]
    public void Progress_is_read_from_the_bar(string line, double? percent) =>
        Assert.Equal(percent, Dism.ParseProgress(line));

    [Fact]
    public void Success_uses_the_tools_summary()
    {
        var result = Dism.Classify(0, "Done.");

        Assert.Equal((ToolOutcome.Succeeded, "Done.", false), (result.Outcome, result.Summary, result.RestartRequired));
    }

    [Fact]
    public void Reboot_required_is_a_success_that_asks_for_a_restart()
    {
        var result = Dism.Classify(ErrorCodes.SuccessRebootRequired, "Done.");

        Assert.Equal((ToolOutcome.Succeeded, true), (result.Outcome, result.RestartRequired));
    }

    [Theory]
    [InlineData(0x800F081F, KnownIssues.DismSourceUnavailable)]
    [InlineData(0x800F082F, KnownIssues.RestartPending)]
    [InlineData(0x80073712, KnownIssues.ComponentStoreDamaged)]
    [InlineData(87u, null)]
    public void Failures_keep_the_code_and_recognize_known_issues(uint code, string? issue)
    {
        var result = Dism.Classify(unchecked((int)code), "Done.");

        Assert.Equal((ToolOutcome.Failed, unchecked((int)code), issue), (result.Outcome, result.ErrorCode, result.IssueId));
    }
}
