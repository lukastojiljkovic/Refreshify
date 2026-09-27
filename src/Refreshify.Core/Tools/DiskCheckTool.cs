using Refreshify.Core.Diagnostics;
using Refreshify.Core.Platform;

namespace Refreshify.Core.Tools;

/// <summary>An online scan of the system drive. The result comes from chkdsk's documented exit codes.</summary>
public sealed class DiskCheckTool(ToolInfo info) : Tool(info)
{
    public override async Task<ToolResult> RunAsync(ToolContext context, CancellationToken cancellationToken)
    {
        var drive = SystemState.SystemDrive.TrimEnd('\\');
        context.Status($"Checking drive {drive}");
        var result = await context.RunAsync(ProcessSpec.System32("chkdsk.exe", $"{drive} /scan"), null, cancellationToken);
        return Classify(result.ExitCode);
    }

    public static ToolResult Classify(int exitCode) => exitCode switch
    {
        0 or 2 => ToolResult.Succeeded("No problems were found."),
        1 => ToolResult.Succeeded("Found problems and fixed them."),
        3 => ToolResult.Failed("Found problems that can't be fixed while Windows is running.", exitCode, KnownIssues.DiskErrors),
        _ => ToolResult.Failed($"It stopped with error {ErrorCodes.Format(exitCode)}.", exitCode),
    };
}
