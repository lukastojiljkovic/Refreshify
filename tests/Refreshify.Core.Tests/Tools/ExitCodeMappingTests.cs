using Refreshify.Core.Diagnostics;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Tests.Tools;

public class ExitCodeMappingTests
{
    [Theory]
    [InlineData(0, ToolOutcome.Succeeded, null)]
    [InlineData(1, ToolOutcome.Succeeded, null)]
    [InlineData(2, ToolOutcome.Succeeded, null)]
    [InlineData(3, ToolOutcome.Failed, KnownIssues.DiskErrors)]
    [InlineData(5, ToolOutcome.Failed, null)]
    public void Chkdsk_exit_codes(int exitCode, ToolOutcome outcome, string? issue)
    {
        var result = DiskCheckTool.Classify(exitCode);

        Assert.Equal((outcome, issue), (result.Outcome, result.IssueId));
    }

    [Fact]
    public void Winget_counts_the_apps_it_updated()
    {
        string[] output = ["(1/2) Found Git [Git.Git] Version 2.51.0", "Successfully installed", "(2/2) Found 7-Zip [7zip.7zip] Version 25.01", "Successfully installed"];

        var result = Winget.Classify(0, output);

        Assert.Equal((ToolOutcome.Succeeded, "Updated 2 apps."), (result.Outcome, result.Summary));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0x8A150014)]
    [InlineData(0x8A15002B)]
    public void Winget_with_nothing_to_update(uint exitCode) =>
        Assert.Equal("Your apps are up to date.", Winget.Classify(unchecked((int)exitCode), []).Summary);

    [Fact]
    public void Winget_reboot_required_is_a_success_that_asks_for_a_restart()
    {
        var result = Winget.Classify(ErrorCodes.WingetRebootRequiredToFinish, ["(1/1) Found Git [Git.Git] Version 2.51.0"]);

        Assert.Equal((ToolOutcome.Succeeded, true), (result.Outcome, result.RestartRequired));
    }

    [Theory]
    [InlineData(0x8A15002C, ToolOutcome.Warning, KnownIssues.AppsInUse)]
    [InlineData(0x8A150101, ToolOutcome.Warning, KnownIssues.AppsInUse)]
    [InlineData(0x8A150103, ToolOutcome.Warning, KnownIssues.AppsInUse)]
    [InlineData(0x8A150111, ToolOutcome.Warning, KnownIssues.AppsInUse)]
    [InlineData(0x8A15000B, ToolOutcome.Failed, KnownIssues.WingetSources)]
    [InlineData(0x8A15000F, ToolOutcome.Failed, KnownIssues.WingetSources)]
    [InlineData(0x8A150045, ToolOutcome.Failed, KnownIssues.WingetSources)]
    [InlineData(0x8A150105, ToolOutcome.Failed, KnownIssues.DiskFull)]
    [InlineData(0x8A150107, ToolOutcome.Failed, KnownIssues.NoConnection)]
    [InlineData(0x8A150001, ToolOutcome.Failed, null)]
    public void Winget_failures(uint exitCode, ToolOutcome outcome, string? issue)
    {
        var result = Winget.Classify(unchecked((int)exitCode), []);

        Assert.Equal((outcome, issue, unchecked((int)exitCode)), (result.Outcome, result.IssueId, result.ErrorCode));
    }
}
