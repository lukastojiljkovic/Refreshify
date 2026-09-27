using Refreshify.Core.Diagnostics;

namespace Refreshify.Core.Tools;

public sealed class AppUpdatesTool(ToolInfo info) : Tool(info)
{
    public override async Task<ToolResult> RunAsync(ToolContext context, CancellationToken cancellationToken)
    {
        if (!File.Exists(Winget.Executable))
            return ToolResult.Failed("winget isn't available.", issueId: KnownIssues.WingetMissing);

        context.Status("Checking for app updates");
        var result = await context.RunAsync(Winget.UpgradeAll(), line =>
        {
            if (Winget.ParseCounter(line) is var (index, count))
                context.Status($"Updating app {index} of {count}", 100.0 * (index - 1) / count);
        }, cancellationToken);
        return Winget.Classify(result.ExitCode, result.Output);
    }
}
