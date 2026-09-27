using Refreshify.Core.Platform;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Tests.Tools;

public class CommandToolTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ToolInfo NetworkReset = new(
        "network-reset", "Network stack reset", ToolCategory.Troubleshooting, "", "Resets networking.", "netsh", RunAs.Administrator,
        Traits: ToolTraits.RestartRequired);

    /// <summary>Returns the queued exit codes in order and records what was started.</summary>
    private sealed class ScriptedRunner(params int[] exitCodes) : IProcessRunner
    {
        private readonly Queue<int> _exitCodes = new(exitCodes);

        public List<ProcessSpec> Started { get; } = [];

        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onLine, CancellationToken cancellationToken)
        {
            Started.Add(spec);
            return Task.FromResult(new ProcessResult(_exitCodes.Dequeue(), []));
        }
    }

    private static Task<ToolResult> RunAsync(Tool tool, IProcessRunner runner) =>
        ToolRunner.RunAsync(tool, new ToolContext(new ToolOptions(), new SynchronousProgress<ToolEvent>(_ => { }), runner), Ct);

    [Fact]
    public async Task Runs_every_command_and_reports_the_tools_restart_requirement()
    {
        var runner = new ScriptedRunner(0, 0);
        var tool = new CommandTool(NetworkReset, [
            new Command(@"%SystemRoot%\System32\netsh.exe", "winsock reset", "Resetting Winsock"),
            new Command(@"%SystemRoot%\System32\netsh.exe", "int ip reset", "Resetting TCP/IP"),
        ], "Network settings were reset.");

        var result = await RunAsync(tool, runner);

        Assert.Equal(ToolOutcome.Succeeded, result.Outcome);
        Assert.Equal("Network settings were reset.", result.Summary);
        Assert.True(result.RestartRequired);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\netsh.exe"), runner.Started[0].FileName, ignoreCase: true);
        Assert.Equal(["winsock reset", "int ip reset"], runner.Started.Select(spec => spec.Arguments));
    }

    [Fact]
    public async Task A_failing_required_command_stops_the_tool_with_its_exit_code()
    {
        var runner = new ScriptedRunner(1, 0);
        var tool = new CommandTool(NetworkReset, [
            new Command(@"%SystemRoot%\System32\netsh.exe", "winsock reset", "Resetting Winsock"),
            new Command(@"%SystemRoot%\System32\netsh.exe", "int ip reset", "Resetting TCP/IP"),
        ], "Network settings were reset.");

        var result = await RunAsync(tool, runner);

        Assert.Equal(ToolOutcome.Failed, result.Outcome);
        Assert.Equal(1, result.ErrorCode);
        Assert.Single(runner.Started);
    }

    [Fact]
    public async Task A_failing_optional_command_turns_the_result_into_a_warning()
    {
        var runner = new ScriptedRunner(1, 0);
        var tool = new CommandTool(NetworkReset, [
            new Command(@"%SystemRoot%\System32\ipconfig.exe", "/release", "Releasing", Required: false),
            new Command(@"%SystemRoot%\System32\ipconfig.exe", "/renew", "Renewing"),
        ], "Your PC asked for a new IP address.");

        var result = await RunAsync(tool, runner);

        Assert.Equal(ToolOutcome.Warning, result.Outcome);
        Assert.Equal(2, runner.Started.Count);
    }
}
