using Refreshify.Core.Diagnostics;
using Refreshify.Core.Platform;

namespace Refreshify.Core.Tools;

public abstract class Tool(ToolInfo info)
{
    public ToolInfo Info { get; } = info;

    public abstract Task<ToolResult> RunAsync(ToolContext context, CancellationToken cancellationToken);
}

/// <summary>What a running tool can use: its options, progress reporting and a process runner that records a trace.</summary>
public sealed class ToolContext(ToolOptions options, IProgress<ToolEvent> progress, IProcessRunner processes)
{
    private const int MaxTraceLines = 60;

    private readonly List<string> _commands = [];
    private readonly List<string> _output = [];
    private readonly Lock _lock = new();

    public ToolOptions Options { get; } = options;

    /// <summary>Relevant lines from a log file, such as the SFC entries in CBS.log.</summary>
    public string? LogExcerpt { get; set; }

    public void Status(string text, double? percent = null) => progress.Report(new ToolEvent(ToolEventKind.Status, text, percent));

    public async Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onLine, CancellationToken cancellationToken)
    {
        var result = await processes.RunAsync(spec, line =>
        {
            AddOutput(line);
            onLine?.Invoke(line);
        }, cancellationToken);

        lock (_lock)
            _commands.Add($"{spec.CommandLine} (exit code {ErrorCodes.Format(result.ExitCode)})");
        return result;
    }

    public ToolTrace BuildTrace()
    {
        lock (_lock)
            return new ToolTrace([.. _commands], [.. _output], LogExcerpt);
    }

    private void AddOutput(string line)
    {
        lock (_lock)
        {
            if (_output.Count > 0 && OutputLines.IsProgressUpdate(_output[^1], line))
                _output[^1] = line;
            else
                _output.Add(line);

            if (_output.Count > MaxTraceLines)
                _output.RemoveAt(0);
        }

        progress.Report(new ToolEvent(ToolEventKind.Output, line));
    }
}

public static class ToolRunner
{
    /// <summary>Runs a tool and always returns a result with its trace, turning exceptions into failures.</summary>
    public static async Task<ToolResult> RunAsync(Tool tool, ToolContext context, CancellationToken cancellationToken)
    {
        ToolResult result;
        try
        {
            result = await tool.RunAsync(context, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result = new ToolResult(ToolOutcome.Cancelled, "Stopped before it finished.");
        }
        catch (Exception ex)
        {
            result = ToolResult.Failed(ex.Message, ex.HResult);
        }

        return result with { Trace = context.BuildTrace() };
    }
}

public static class OutputLines
{
    /// <summary>
    /// Whether <paramref name="current"/> only updates the percentage shown by <paramref name="previous"/>, as DISM, SFC
    /// and chkdsk do many times per second. Such lines replace each other instead of piling up.
    /// </summary>
    public static bool IsProgressUpdate(string previous, string current) =>
        previous.Contains('%') && current.Contains('%') && StripProgress(previous) == StripProgress(current);

    /// <summary>The last <paramref name="count"/> lines a person can read: no blank lines and no JSON messages from scripts.</summary>
    public static IReadOnlyList<string> Latest(IEnumerable<string> lines, int count) =>
        [.. lines.Where(line => !string.IsNullOrWhiteSpace(line) && !PowerShell.IsMessage(line)).TakeLast(count)];

    private static string StripProgress(string line) =>
        new(line.Where(c => !char.IsDigit(c) && c is not ('.' or ',' or '=' or ' ')).ToArray());
}

/// <summary>An <see cref="IProgress{T}"/> that reports on the calling thread, unlike <see cref="Progress{T}"/>.</summary>
public sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
