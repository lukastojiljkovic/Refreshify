using Refreshify.Core.Platform;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Tests.Tools;

public class ToolRunnerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ToolInfo TestInfo = new("test", "Test tool", ToolCategory.Cleanup, "\uE74D", "Does a test.", "test.exe", RunAs.User);

    private sealed class DelegateTool(Func<ToolContext, CancellationToken, Task<ToolResult>> run) : Tool(TestInfo)
    {
        public override Task<ToolResult> RunAsync(ToolContext context, CancellationToken cancellationToken) => run(context, cancellationToken);
    }

    private sealed class FakeRunner(params string[] lines) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onLine, CancellationToken cancellationToken)
        {
            foreach (var line in lines)
                onLine?.Invoke(line);
            return Task.FromResult(new ProcessResult(5, lines));
        }
    }

    private static ToolContext Context(IProcessRunner? runner = null, List<ToolEvent>? events = null) =>
        new(new ToolOptions(), new SynchronousProgress<ToolEvent>(e => events?.Add(e)), runner ?? new FakeRunner());

    [Fact]
    public async Task Exceptions_become_failed_results_with_their_hresult()
    {
        var tool = new DelegateTool((_, _) => throw new UnauthorizedAccessException("Access is denied."));

        var result = await ToolRunner.RunAsync(tool, Context(), Ct);

        Assert.Equal(ToolOutcome.Failed, result.Outcome);
        Assert.Equal("Access is denied.", result.Summary);
        Assert.Equal(unchecked((int)0x80070005), result.ErrorCode);
    }

    [Fact]
    public async Task Cancellation_becomes_a_cancelled_result()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var tool = new DelegateTool((_, token) => Task.FromCanceled<ToolResult>(token));

        var result = await ToolRunner.RunAsync(tool, Context(), cancellation.Token);

        Assert.Equal(ToolOutcome.Cancelled, result.Outcome);
    }

    [Fact]
    public async Task The_trace_records_commands_and_collapses_progress_updates()
    {
        var runner = new FakeRunner("Starting", "[==   10.0%   ]", "[====   20.0%   ]", "Done");
        var events = new List<ToolEvent>();
        var tool = new DelegateTool(async (context, token) =>
        {
            await context.RunAsync(new ProcessSpec(@"C:\Windows\System32\dism.exe", "/Online") { DisplayName = "dism.exe /Online" }, null, token);
            return ToolResult.Succeeded("ok");
        });

        var result = await ToolRunner.RunAsync(tool, Context(runner, events), Ct);

        Assert.Equal(["dism.exe /Online (exit code 5)"], result.Trace!.Commands);
        Assert.Equal(["Starting", "[====   20.0%   ]", "Done"], result.Trace.Output);
        Assert.Equal(4, events.Count(e => e.Kind == ToolEventKind.Output));
    }

    [Theory]
    [InlineData("Verification 45% complete.", "Verification 46% complete.", true)]
    [InlineData("[==   10.0%   ]", "[====   20.0%   ]", true)]
    [InlineData("Verification 45% complete.", "Verification complete.", false)]
    [InlineData("Starting", "Starting", false)]
    public void Progress_updates_are_recognized(string previous, string current, bool expected) =>
        Assert.Equal(expected, OutputLines.IsProgressUpdate(previous, current));

    [Fact]
    public void The_latest_lines_leave_out_blank_lines_and_script_messages() =>
        Assert.Equal(["Scanning", "Done"], OutputLines.Latest(["First", "", "Scanning", "@@{\"freed\":1}", "  ", "Done"], 2));
}
