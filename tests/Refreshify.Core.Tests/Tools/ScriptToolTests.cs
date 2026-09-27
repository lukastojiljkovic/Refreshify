using Refreshify.Core.Diagnostics;
using Refreshify.Core.Platform;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Tests.Tools;

public class ScriptToolTests
{
    private static readonly ToolInfo TestInfo = new(
        "test-script", "Test script", ToolCategory.Network, "", "Runs a script.", "script", RunAs.User);

    private sealed class CannedRunner(int exitCode, params string[] output) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onLine, CancellationToken cancellationToken)
        {
            foreach (var line in output)
                onLine?.Invoke(line);
            return Task.FromResult(new ProcessResult(exitCode, output));
        }
    }

    private static Task<ToolResult> RunAsync(ScriptTool tool, IProcessRunner runner) =>
        ToolRunner.RunAsync(tool, new ToolContext(new ToolOptions(), new SynchronousProgress<ToolEvent>(_ => { }), runner),
            TestContext.Current.CancellationToken);

    private static ScriptTool Tool(Func<int, string?>? issueFor = null) =>
        new(TestInfo, "Working", _ => PowerShell.Inline("Emit @{ state = 'done' }", "test"),
            messages => ToolResult.Succeeded($"State: {PowerShell.Find(messages, "state")!.Value.GetProperty("state").GetString()}"), issueFor);

    [Fact]
    public async Task Messages_are_interpreted_by_the_tool()
    {
        var result = await RunAsync(Tool(), new CannedRunner(0, "@@{\"state\":\"done\"}"));

        Assert.Equal((ToolOutcome.Succeeded, "State: done"), (result.Outcome, result.Summary));
    }

    [Fact]
    public async Task A_script_error_fails_with_its_hresult_and_known_issue()
    {
        var result = await RunAsync(Tool(), new CannedRunner(1, "@@{\"error\":\"There is not enough space on the disk.\",\"hresult\":-2147024784}"));

        Assert.Equal((ToolOutcome.Failed, ErrorCodes.DiskFull, KnownIssues.DiskFull, "There is not enough space on the disk."),
            (result.Outcome, result.ErrorCode, result.IssueId, result.Summary));
    }

    [Fact]
    public async Task A_tool_can_map_errors_to_its_own_issues()
    {
        var result = await RunAsync(Tool(code => code == ErrorCodes.ServiceDisabled ? KnownIssues.SystemProtectionOff : null),
            new CannedRunner(1, "@@{\"error\":\"Disabled.\",\"hresult\":-2147023838}"));

        Assert.Equal(KnownIssues.SystemProtectionOff, result.IssueId);
    }

    [Fact]
    public async Task A_script_can_skip_with_a_reason()
    {
        var result = await RunAsync(Tool(), new CannedRunner(0, "@@{\"skipped\":\"Another antivirus protects this PC.\"}"));

        Assert.Equal((ToolOutcome.Skipped, "Another antivirus protects this PC."), (result.Outcome, result.Summary));
    }

    [Fact]
    public async Task A_nonzero_exit_without_a_message_fails_with_the_exit_code()
    {
        var result = await RunAsync(Tool(), new CannedRunner(5));

        Assert.Equal((ToolOutcome.Failed, 5), (result.Outcome, result.ErrorCode));
    }
}
