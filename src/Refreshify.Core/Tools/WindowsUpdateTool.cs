using System.Text.Json;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Platform;

namespace Refreshify.Core.Tools;

/// <summary>
/// Installs Windows updates one at a time. Cancelling lets the current update finish: the script checks a stop file
/// between updates instead of being killed halfway through an installation.
/// </summary>
public sealed class WindowsUpdateTool(ToolInfo info) : Tool(info)
{
    public override async Task<ToolResult> RunAsync(ToolContext context, CancellationToken cancellationToken)
    {
        context.Status("Checking for updates");
        var stopFile = Path.Combine(Path.GetTempPath(), $"refreshify-{Guid.NewGuid():N}.stop");
        try
        {
            await using var stop = cancellationToken.Register(() => RequestStop(stopFile));
            var script = PowerShell.Script("WindowsUpdate", new Dictionary<string, string> { ["StopFile"] = stopFile });
            var result = await context.RunAsync(script, line => ReportProgress(context, line), CancellationToken.None);
            return ScriptTool.Evaluate(result, Interpret, KnownIssues.FromWindowsUpdate);
        }
        finally
        {
            File.Delete(stopFile);
        }
    }

    public static ToolResult Interpret(IReadOnlyList<JsonElement> messages)
    {
        var installed = messages
            .Where(message => message.TryGetProperty("installed", out _))
            .Select(message => message.GetProperty("installed").GetString() ?? string.Empty)
            .ToList();
        var failed = messages
            .Where(message => message.TryGetProperty("failed", out _))
            .Select(message => (Title: message.GetProperty("failed").GetString() ?? string.Empty, HResult: message.GetProperty("hresult").GetInt32()))
            .ToList();
        string[] details =
        [
            .. installed.Select(title => $"Installed: {title}"),
            .. failed.Select(update => $"Failed ({ErrorCodes.Format(update.HResult)}): {update.Title}"),
        ];
        var restart = PowerShell.Find(messages, "done") is { } done && done.GetProperty("restart").GetBoolean();
        var installedText = Format.Count(installed.Count, "update");

        if (PowerShell.Find(messages, "stopped") is not null)
            return new ToolResult(ToolOutcome.Cancelled, $"Stopped after installing {installedText}.") { Details = details, RestartRequired = restart };
        if (installed.Count == 0 && failed.Count == 0)
            return ToolResult.Succeeded("Windows is up to date.");
        if (failed.Count == 0)
            return ToolResult.Succeeded($"Installed {installedText}.") with { Details = details, RestartRequired = restart };

        var code = failed[0].HResult;
        var notInstalled = $"{Format.Count(failed.Count, "update")} couldn't be installed.";
        var partly = installed.Count > 0;
        return new ToolResult(partly ? ToolOutcome.Warning : ToolOutcome.Failed, partly ? $"Installed {installedText}; {notInstalled}" : notInstalled)
        {
            ErrorCode = code,
            IssueId = KnownIssues.FromWindowsUpdate(code),
            Details = details,
            RestartRequired = restart,
        };
    }

    private static void RequestStop(string stopFile)
    {
        try
        {
            File.WriteAllText(stopFile, string.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The run then ends when the updates are done, as if it hadn't been cancelled.
        }
    }

    private static void ReportProgress(ToolContext context, string line)
    {
        if (PowerShell.Messages([line]) is not [var message] || !message.TryGetProperty("update", out var title))
            return;

        var index = message.GetProperty("index").GetInt32();
        var count = message.GetProperty("count").GetInt32();
        var verb = message.GetProperty("phase").GetString() == "downloading" ? "Downloading" : "Installing";
        context.Status($"{verb} update {index} of {count}: {title.GetString()}", 100.0 * (index - 1) / count);
    }
}
