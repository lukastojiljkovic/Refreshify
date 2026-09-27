using Refreshify.Core.Diagnostics;
using Refreshify.Core.Platform;

namespace Refreshify.Core.Tools;

/// <summary>
/// Deletes files from a list of targets, optionally with the services that hold them stopped. Tools with
/// <see cref="ToolTraits.RestartsExplorer"/> close File Explorer while they clean.
/// </summary>
/// <param name="targets">Resolved when the tool runs, in the context (user or worker) it runs in.</param>
/// <param name="stopServices">Stopped before cleaning; those that were running are started again afterwards.</param>
/// <param name="precondition">A result that ends the tool before it deletes anything, such as a skip.</param>
/// <param name="summary">Replaces the measured summary, for tools whose purpose isn't freeing space.</param>
public sealed class CleanupTool(
    ToolInfo info,
    Func<IEnumerable<CleanupTarget>> targets,
    IReadOnlyList<string>? stopServices = null,
    Func<ToolResult?>? precondition = null,
    string? summary = null) : Tool(info)
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

            context.Status(Info.Has(ToolTraits.RestartsExplorer) ? "Closing File Explorer and deleting files" : "Deleting files");
            var minimumAge = TimeSpan.FromHours(context.Options.TempFileAgeHours);
            CleanupStats Clean() => targets().Aggregate(CleanupStats.Empty,
                (total, target) => total + FileCleaner.Clean(target, minimumAge, cancellationToken));
            var stats = await Task.Run(() => Info.Has(ToolTraits.RestartsExplorer) ? ExplorerRestarter.Restart(Clean) : Clean(), cancellationToken);

            var result = summary is null ? Summarize(stats) : ToolResult.Succeeded(summary) with { BytesFreed = stats.BytesFreed };
            return result with { RestartRequired = Info.Has(ToolTraits.RestartRequired) };
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

        var text = $"Freed {Format.Bytes(stats.BytesFreed)} ({Format.Count(stats.FilesDeleted, "file")}).";
        if (stats.FilesSkipped > 0)
            text += $" Skipped {Format.Count(stats.FilesSkipped, "file")} that {(stats.FilesSkipped == 1 ? "is" : "are")} in use.";
        return ToolResult.Succeeded(text) with { BytesFreed = stats.BytesFreed };
    }
}
