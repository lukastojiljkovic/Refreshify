namespace Refreshify.Core.Platform;

public sealed record CleanupStats(long BytesFreed, int FilesDeleted, int FilesSkipped)
{
    public static readonly CleanupStats Empty = new(0, 0, 0);

    public static CleanupStats operator +(CleanupStats a, CleanupStats b) =>
        new(a.BytesFreed + b.BytesFreed, a.FilesDeleted + b.FilesDeleted, a.FilesSkipped + b.FilesSkipped);
}

/// <param name="Path">A folder whose contents are cleaned (never the folder itself), or a single file.</param>
/// <param name="UseAgeFilter">Only delete files last written before the minimum age, such as in temp folders that apps use.</param>
public sealed record CleanupTarget(string Path, string Pattern = "*", bool Recursive = true, bool UseAgeFilter = false);

public static class FileCleaner
{
    /// <summary>
    /// Deletes what <paramref name="target"/> describes. Files in use are skipped and counted, and reparse points
    /// (junctions, symbolic links) are never followed or deleted, so cleanup can't reach outside the target.
    /// </summary>
    /// <param name="deleted">Called with the size of each file as it's deleted, for live progress.</param>
    public static CleanupStats Clean(CleanupTarget target, TimeSpan minimumAge, Action<long>? deleted, CancellationToken cancellationToken)
    {
        var cutoff = target.UseAgeFilter ? DateTime.UtcNow - minimumAge : DateTime.MaxValue;
        if (File.Exists(target.Path))
            return Delete(new FileInfo(target.Path), cutoff, deleted);

        var directory = new DirectoryInfo(target.Path);
        return directory.Exists ? CleanDirectory(directory, target, cutoff, deleted, cancellationToken) : CleanupStats.Empty;
    }

    private static CleanupStats CleanDirectory(
        DirectoryInfo directory, CleanupTarget target, DateTime cutoff, Action<long>? deleted, CancellationToken cancellationToken)
    {
        var stats = CleanupStats.Empty;
        foreach (var file in Enumerate(() => directory.EnumerateFiles(target.Pattern)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                stats += Delete(file, cutoff, deleted);
        }

        if (!target.Recursive)
            return stats;

        foreach (var subdirectory in Enumerate(directory.EnumerateDirectories))
        {
            if (subdirectory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;

            stats += CleanDirectory(subdirectory, target, cutoff, deleted, cancellationToken);
            TryRemoveEmpty(subdirectory, cutoff);
        }

        return stats;
    }

    private static CleanupStats Delete(FileInfo file, DateTime cutoff, Action<long>? deleted)
    {
        try
        {
            if (file.LastWriteTimeUtc > cutoff)
                return CleanupStats.Empty;

            var length = file.Length;
            if (file.IsReadOnly)
                file.IsReadOnly = false;
            file.Delete();
            deleted?.Invoke(length);
            return new CleanupStats(length, 1, 0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new CleanupStats(0, 0, 1);
        }
    }

    /// <summary>
    /// The creation time decides, because deleting a folder's files updates its last write time. A folder an app has just
    /// created and not filled yet is left alone.
    /// </summary>
    private static void TryRemoveEmpty(DirectoryInfo directory, DateTime cutoff)
    {
        try
        {
            directory.Refresh();
            if (directory.CreationTimeUtc <= cutoff && !directory.EnumerateFileSystemInfos().Any())
                directory.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // In use or protected; leave it.
        }
    }

    /// <summary>Enumerates eagerly so an access error on one folder doesn't abort the whole cleanup.</summary>
    private static IReadOnlyList<T> Enumerate<T>(Func<IEnumerable<T>> source)
    {
        try
        {
            return [.. source()];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
