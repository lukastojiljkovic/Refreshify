using System.Text.Json;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Platform;

namespace Refreshify.Core.Tools;

/// <summary>Runs a PowerShell script and turns its JSON messages into a result.</summary>
/// <param name="interpret">Reads the messages of a script that finished without an error or a skip.</param>
/// <param name="issueFor">Maps a script error's HRESULT to a known issue; <see cref="KnownIssues.FromHResult"/> by default.</param>
public sealed class ScriptTool(
    ToolInfo info,
    string status,
    Func<ToolContext, ProcessSpec> script,
    Func<IReadOnlyList<JsonElement>, ToolResult> interpret,
    Func<int, string?>? issueFor = null) : Tool(info)
{
    public override async Task<ToolResult> RunAsync(ToolContext context, CancellationToken cancellationToken)
    {
        context.Status(status);
        var result = await context.RunAsync(script(context), null, cancellationToken);
        return Evaluate(result, interpret, issueFor);
    }

    /// <summary>
    /// A script reports a failure as <c>{ error, hresult }</c> and a reason not to run as <c>{ skipped }</c>; anything else
    /// is up to the tool.
    /// </summary>
    public static ToolResult Evaluate(
        ProcessResult result, Func<IReadOnlyList<JsonElement>, ToolResult> interpret, Func<int, string?>? issueFor = null)
    {
        var messages = PowerShell.Messages(result.Output);
        if (PowerShell.Error(messages) is { } error)
            return ToolResult.Failed(error.Message.Trim(), error.HResult, (issueFor ?? KnownIssues.FromHResult)(error.HResult));
        if (PowerShell.Find(messages, "skipped") is { } skipped)
            return ToolResult.Skipped(skipped.GetProperty("skipped").GetString() ?? string.Empty);
        return result.ExitCode == 0
            ? interpret(messages)
            : ToolResult.Failed($"It stopped with error {ErrorCodes.Format(result.ExitCode)}.", result.ExitCode);
    }
}
