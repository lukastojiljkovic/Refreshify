using System.ComponentModel;
using System.Diagnostics;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Platform;

namespace Refreshify.Core.Tools;

public sealed class RecycleBinTool(ToolInfo info) : Tool(info)
{
    public override Task<ToolResult> RunAsync(ToolContext context, CancellationToken cancellationToken) => StaThread.Run(() =>
    {
        var before = RecycleBin.Query();
        if (before.Items == 0)
            return ToolResult.Succeeded("The Recycle Bin was already empty.");

        context.Status("Emptying the Recycle Bin");
        RecycleBin.Empty();
        var after = RecycleBin.Query();
        var freed = Math.Max(0, before.Bytes - after.Bytes);
        return ToolResult.Succeeded($"Freed {Format.Bytes(freed)} ({Format.Count(before.Items - after.Items, "item")}).") with { BytesFreed = freed };
    });
}

/// <summary>Restarts File Explorer and ends the Start menu and search hosts, which Windows starts again on its own.</summary>
public sealed class ShellRestartTool(ToolInfo info) : Tool(info)
{
    private static readonly string[] ShellHosts = ["StartMenuExperienceHost", "ShellExperienceHost", "SearchHost"];

    public override Task<ToolResult> RunAsync(ToolContext context, CancellationToken cancellationToken) => Task.Run(() =>
    {
        context.Status("Restarting File Explorer, the Start menu and search");
        ExplorerRestarter.Restart(() =>
        {
            foreach (var name in ShellHosts)
                EndInSession(name);
            return true;
        });
        return ToolResult.Succeeded("Restarted File Explorer, the Start menu and search.");
    }, cancellationToken);

    private static void EndInSession(string name)
    {
        using var current = Process.GetCurrentProcess();
        foreach (var process in Process.GetProcessesByName(name))
        {
            using (process)
            {
                try
                {
                    if (process.SessionId == current.SessionId)
                        process.Kill();
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    // Already exited.
                }
            }
        }
    }
}
