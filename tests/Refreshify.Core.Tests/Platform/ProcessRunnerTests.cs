using System.Diagnostics;
using System.Text;
using Refreshify.Core.Platform;

namespace Refreshify.Core.Tests.Platform;

public class ProcessRunnerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ProcessSpec PowerShellCommand(string command, Encoding? encoding = null) =>
        new(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"), $"-NoProfile -NonInteractive -Command \"{command}\"")
        {
            Encoding = encoding ?? Encoding.UTF8,
        };

    [Fact]
    public async Task Splits_output_on_carriage_returns_as_well_as_newlines()
    {
        var spec = PowerShellCommand("[Console]::Out.Write('10%' + [char]13 + '20%' + [char]13 + 'done' + [char]13 + [char]10)");

        var result = await ProcessRunner.Default.RunAsync(spec, null, Ct);

        Assert.Equal(["10%", "20%", "done"], result.Output);
    }

    [Fact]
    public async Task Decodes_utf16_output_when_asked()
    {
        var spec = PowerShellCommand(
            "$bytes = [Text.Encoding]::Unicode.GetBytes('Verification 45% complete.' + [char]13 + [char]10); $out = [Console]::OpenStandardOutput(); $out.Write($bytes, 0, $bytes.Length); $out.Flush()",
            Encoding.Unicode);

        var result = await ProcessRunner.Default.RunAsync(spec, null, Ct);

        Assert.Equal(["Verification 45% complete."], result.Output);
    }

    [Fact]
    public async Task Reports_every_line_to_the_callback_and_returns_the_exit_code()
    {
        var spec = new ProcessSpec(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/c echo first& echo second& exit 3");
        var seen = new List<string>();

        var result = await ProcessRunner.Default.RunAsync(spec, seen.Add, Ct);

        Assert.Equal(3, result.ExitCode);
        Assert.Equal(["first", "second"], seen);
    }

    [Fact]
    public async Task Cancellation_kills_the_whole_process_tree()
    {
        // ping runs as a child of cmd and inherits the output pipes; the call can only return early if ping is killed too.
        var spec = new ProcessSpec(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/c ping -n 30 127.0.0.1");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        cancellation.CancelAfter(TimeSpan.FromSeconds(1));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProcessRunner.Default.RunAsync(spec, null, cancellation.Token));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Took {stopwatch.Elapsed}");
    }
}
