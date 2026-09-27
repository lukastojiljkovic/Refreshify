using Refreshify.Core.Diagnostics;
using Refreshify.Core.Platform;

namespace Refreshify.Core.Tools;

/// <param name="reportsFreedSpace">Reports the growth of free space on the system drive, for cleanups.</param>
/// <param name="acceptsWindowsImage">Repairs from <see cref="ToolOptions.WindowsImagePath"/> when the user chose one.</param>
public sealed class DismTool(
    ToolInfo info, string arguments, string status, string summary, bool reportsFreedSpace = false, bool acceptsWindowsImage = false) : Tool(info)
{
    public override async Task<ToolResult> RunAsync(ToolContext context, CancellationToken cancellationToken)
    {
        if (!acceptsWindowsImage || context.Options.WindowsImagePath is not { } image)
            return await RunDismAsync(context, arguments, cancellationToken);

        context.Status("Opening the Windows image");
        var parameters = new Dictionary<string, string> { ["Path"] = image };
        var mount = await context.RunAsync(PowerShell.Script("MountImage", parameters), null, cancellationToken);
        var messages = PowerShell.Messages(mount.Output);
        try
        {
            var mounted = ScriptTool.Evaluate(mount, _ => ToolResult.Succeeded(string.Empty));
            if (mounted.Outcome != ToolOutcome.Succeeded)
                return mounted;

            var state = PowerShell.Find(messages, "state")!.Value;
            return state.GetProperty("state").GetString() switch
            {
                "ready" => await RunDismAsync(context, $"{arguments} /Source:{state.GetProperty("source").GetString()} /LimitAccess", cancellationToken),
                "noedition" => ToolResult.Failed($"This Windows image doesn't contain your edition of Windows ({state.GetProperty("edition").GetString()})."),
                _ => ToolResult.Failed("This file doesn't contain a Windows installation image."),
            };
        }
        finally
        {
            if (PowerShell.Find(messages, "mounted") is { } entry && entry.GetProperty("mounted").GetBoolean())
            {
                await context.RunAsync(
                    PowerShell.Inline("Dismount-DiskImage -ImagePath $Path | Out-Null", "powershell.exe Dismount-DiskImage", parameters),
                    null, CancellationToken.None);
            }
        }
    }

    private async Task<ToolResult> RunDismAsync(ToolContext context, string dismArguments, CancellationToken cancellationToken)
    {
        context.Status(status);
        var freeBefore = reportsFreedSpace ? SystemState.FreeSpace : 0;
        var result = await context.RunAsync(Dism.CleanupImage(dismArguments), line =>
        {
            if (Dism.ParseProgress(line) is { } percent)
                context.Status(status, percent);
        }, cancellationToken);

        var outcome = Dism.Classify(result.ExitCode, summary);
        var freed = reportsFreedSpace ? SystemState.FreeSpace - freeBefore : 0;
        return outcome.Outcome == ToolOutcome.Succeeded && freed > 0
            ? outcome with { Summary = $"{summary} Freed {Format.Bytes(freed)}.", BytesFreed = freed }
            : outcome;
    }
}
