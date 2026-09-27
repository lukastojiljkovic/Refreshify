using Refreshify.Core.Diagnostics;
using Refreshify.Core.Platform;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Tests.Tools;

public class WindowsUpdateToolTests
{
    private static ToolResult Interpret(params string[] lines) => WindowsUpdateTool.Interpret(PowerShell.Messages(lines.Select(line => "@@" + line)));

    [Fact]
    public void Nothing_found_means_up_to_date()
    {
        var result = Interpret("""{"found":0}""", """{"done":true,"restart":false}""");

        Assert.Equal((ToolOutcome.Succeeded, "Windows is up to date."), (result.Outcome, result.Summary));
    }

    [Fact]
    public void Installed_updates_are_counted_and_listed()
    {
        var result = Interpret("""{"found":2}""", """{"installed":"Update A"}""", """{"installed":"Update B"}""", """{"done":true,"restart":true}""");

        Assert.Equal((ToolOutcome.Succeeded, "Installed 2 updates.", true), (result.Outcome, result.Summary, result.RestartRequired));
        Assert.Equal(["Installed: Update A", "Installed: Update B"], result.Details);
    }

    [Fact]
    public void Some_failures_make_a_warning_with_the_first_failures_issue()
    {
        var result = Interpret("""{"found":2}""", """{"installed":"Update A"}""", """{"failed":"Update B","hresult":-2145091577}""", """{"done":true,"restart":false}""");

        Assert.Equal((ToolOutcome.Warning, "Installed 1 update; 1 update couldn't be installed.", KnownIssues.WuCacheDamaged),
            (result.Outcome, result.Summary, result.IssueId));
        Assert.Equal(unchecked((int)0x80248007), result.ErrorCode);
        Assert.Contains("Failed (0x80248007): Update B", result.Details);
    }

    [Fact]
    public void Only_failures_make_a_failure()
    {
        var result = Interpret("""{"found":1}""", """{"failed":"Update B","hresult":-2145107924}""", """{"done":true,"restart":false}""");

        Assert.Equal((ToolOutcome.Failed, "1 update couldn't be installed.", KnownIssues.NoConnection), (result.Outcome, result.Summary, result.IssueId));
    }

    [Fact]
    public void Stopping_between_updates_is_a_cancellation()
    {
        var result = Interpret("""{"found":3}""", """{"installed":"Update A"}""", """{"stopped":true}""");

        Assert.Equal((ToolOutcome.Cancelled, "Stopped after installing 1 update."), (result.Outcome, result.Summary));
    }
}
