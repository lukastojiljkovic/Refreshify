using Refreshify.Core.Diagnostics;
using Refreshify.Core.Platform;

namespace Refreshify.Core.Tools;

/// <summary>Deletes files from a list of targets, optionally with the services that hold them stopped.</summary>
/// <param name="targets">Resolved when the tool runs, in the context (user or worker) it runs in.</param>
/// <param name="stopServices">Stopped before cleaning; those that were running are started again afterwards.</param>
/// <param name="precondition">A result that ends the tool before it deletes anything, such as a skip.</param>
public sealed class CleanupTool(
    ToolInfo info,
    Func<IEnumerable<CleanupTarget>> targets,
    IReadOnlyList<string>? stopServices = null,
    Func<ToolResult?>? precondition = null) : Tool(info)
{
    public override async Task<ToolResult> RunAsync(ToolContext context, CancellationToken cancellationToken)
    {
        if (precondition?.Invoke() is { } early)
            return early;

        var restart = new List<string>();
        try
        {
            foreach (var service in stopServices ?? [])
            {
                context.Status("Pausing the services that use these files");
                restart.AddRange(await Services.StopAsync(service, cancellationToken));
            }

            context.Status("Deleting files");
            var minimumAge = TimeSpan.FromHours(context.Options.TempFileAgeHours);
            var stats = await Task.Run(() => targets().Aggregate(CleanupStats.Empty,
                (total, target) => total + FileCleaner.Clean(target, minimumAge, cancellationToken)), cancellationToken);
            return Summarize(stats);
        }
        finally
        {
            foreach (var service in restart)
                await Services.StartAsync(service, CancellationToken.None);
        }
    }

    public static ToolResult Summarize(CleanupStats stats)
    {
        if (stats is { FilesDeleted: 0, FilesSkipped: 0 })
            return ToolResult.Succeeded("There was nothing to clean up.");

        var summary = $"Freed {Format.Bytes(stats.BytesFreed)} ({Format.Count(stats.FilesDeleted, "file")}).";
        if (stats.FilesSkipped > 0)
            summary += $" Skipped {Format.Count(stats.FilesSkipped, "file")} that {(stats.FilesSkipped == 1 ? "is" : "are")} in use.";
        return ToolResult.Succeeded(summary) with { BytesFreed = stats.BytesFreed };
    }
}
