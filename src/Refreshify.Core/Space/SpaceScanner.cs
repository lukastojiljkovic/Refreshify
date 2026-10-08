using System.Diagnostics;

namespace Refreshify.Core.Space;

/// <summary>
/// Builds the folder tree below a path: what every folder holds, where the space is and which files are the largest.
/// Read-only, nothing is opened for writing. A cancelled scan returns what it counted so far, and a folder that can't
/// be read is counted and skipped rather than failing the scan.
/// </summary>
public static class SpaceScanner
{
    /// <summary>The rows a list shows before the rest are summarized as smaller items.</summary>
    public const int PageRows = 200;

    /// <summary>The largest files the second view lists below the current folder.</summary>
    public const int LargestFileCount = 100;

    /// <summary>How many unreadable folders are named before the rest are only counted.</summary>
    public const int ReportedUnreadablePaths = 50;

    /// <summary>
    /// How deep below the scanned path folders are read. The scan recurses once per level and long paths allow
    /// thousands of levels, so deeper folders are counted as unreadable instead of running out of stack.
    /// </summary>
    public const int MaxDepth = 512;

    /// <summary>Reads <paramref name="path"/> with <paramref name="walker"/>.</summary>
    /// <param name="progress">Called at most ten times a second with how far the scan has got.</param>
    public static SpaceScan Scan(
        string path, IFileSystemWalker walker, IProgress<SpaceProgress>? progress, CancellationToken cancellationToken) =>
        new Reader(walker, progress, cancellationToken).Read(path);

    /// <summary>
    /// For a scanned drive: the space Windows says is used that the scan didn't count, such as other users' files and
    /// system files. Null when that is nothing, so the line isn't shown.
    /// </summary>
    public static long? UnseenBytes(long usedBytes, long countedBytes) =>
        usedBytes > countedBytes ? usedBytes - countedBytes : null;

    private sealed class Reader(IFileSystemWalker walker, IProgress<SpaceProgress>? progress, CancellationToken cancellationToken)
    {
        /// <summary>The UI never needs more than ten progress updates a second.</summary>
        private const int ReportMilliseconds = 100;

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly object _gate = new();
        private readonly List<string> _unreadablePaths = [];

        private long _folders;
        private long _bytes;
        private long _onlineOnly;
        private long _unreadable;
        private int _lastReport = -ReportMilliseconds;
        private volatile bool _stopped;

        public SpaceScan Read(string path)
        {
            var full = Path.GetFullPath(path);
            Report(force: true);
            var root = ReadFolder(RootName(full), full, depth: 0);
            return new SpaceScan(root, _unreadable, [.. _unreadablePaths], _onlineOnly, _stopped);
        }

        private SpaceFolder ReadFolder(string name, string path, int depth)
        {
            var folder = new SpaceFolder(name, path);
            if (_stopped)
                return folder;
            if (depth > MaxDepth)
            {
                CountUnreadable(path);
                return folder;
            }

            IReadOnlyList<FileSystemEntry> entries;
            try
            {
                entries = walker.List(path, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _stopped = true;
                return folder;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                CountUnreadable(path);
                return folder;
            }

            Interlocked.Increment(ref _folders);
            Report();

            var files = new LargestFiles(PageRows);
            var largest = new LargestFiles(LargestFileCount);
            var subfolders = new List<FileSystemEntry>();
            long fileCount = 0;
            long fileSize = 0;
            foreach (var entry in entries)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _stopped = true;
                    break;
                }

                if (entry.IsLink)
                    continue;
                if (entry.IsDirectory)
                {
                    subfolders.Add(entry);
                    continue;
                }
                if (entry.IsOnlineOnly)
                {
                    Interlocked.Increment(ref _onlineOnly);
                    continue;
                }

                files.Add(new SpaceFile(entry.Name, entry.Path, entry.Size));
                fileCount++;
                fileSize += entry.Size;
                Interlocked.Add(ref _bytes, entry.Size);
                Report();
            }

            folder.Files = files.ToArray();
            folder.OtherFileCount = fileCount - folder.Files.Count;
            folder.OtherFileSize = fileSize - files.Size;
            largest.AddRange(folder.Files);

            long size = fileSize;
            long count = fileCount;
            var nodes = new List<SpaceFolder>(subfolders.Count);
            foreach (var child in ReadChildren(subfolders, depth + 1))
            {
                nodes.Add(child);
                size += child.Size;
                count += child.FileCount;
                largest.AddRange(child.LargestFiles);
            }

            nodes.Sort(CompareFolders);
            folder.Folders = nodes;
            folder.Size = size;
            folder.FileCount = count;
            folder.LargestFiles = largest.ToArray();
            return folder;
        }

        /// <summary>
        /// Reads a folder's subfolders. At the top of a scan they are read in parallel, bounded to the processor
        /// count, because their listings are what a scan spends its time on; below that the scan stays sequential.
        /// </summary>
        private List<SpaceFolder> ReadChildren(List<FileSystemEntry> folders, int depth)
        {
            var nodes = new SpaceFolder?[folders.Count];
            void Read(int index)
            {
                if (_stopped || cancellationToken.IsCancellationRequested)
                {
                    _stopped = true;
                    return;
                }

                nodes[index] = ReadFolder(folders[index].Name, folders[index].Path, depth);
                Report();
            }

            if (depth == 1 && folders.Count > 1)
                Parallel.For(0, folders.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, Read);
            else
                for (var index = 0; index < folders.Count; index++)
                    Read(index);

            var children = new List<SpaceFolder>(folders.Count);
            foreach (var node in nodes)
            {
                if (node is null)
                    _stopped = true;
                else
                    children.Add(node);
            }

            return children;
        }

        private void CountUnreadable(string path)
        {
            Interlocked.Increment(ref _unreadable);
            lock (_gate)
            {
                if (_unreadablePaths.Count < ReportedUnreadablePaths)
                    _unreadablePaths.Add(path);
            }
        }

        private void Report(bool force = false)
        {
            if (progress is null)
                return;

            var now = (int)_clock.ElapsedMilliseconds;
            if (!force && now - _lastReport < ReportMilliseconds)
                return;

            lock (_gate)
            {
                if (!force && now - _lastReport < ReportMilliseconds)
                    return;
                _lastReport = now;
            }

            progress.Report(new SpaceProgress(Interlocked.Read(ref _folders), Interlocked.Read(ref _bytes)));
        }

        /// <summary>The name the root shows, which is its folder name, or the path itself for a drive root.</summary>
        private static string RootName(string full)
        {
            var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFileName(trimmed) is { Length: > 0 } name ? name : full;
        }

        private static int CompareFolders(SpaceFolder left, SpaceFolder right) =>
            left.Size != right.Size ? right.Size.CompareTo(left.Size) : string.CompareOrdinal(left.Name, right.Name);
    }

    /// <summary>The largest files seen so far, capped so that a folder with millions of files can't grow the scan.</summary>
    private sealed class LargestFiles(int limit)
    {
        private static readonly IComparer<SpaceFile> Comparison = Comparer<SpaceFile>.Create((left, right) =>
            left.Size != right.Size ? right.Size.CompareTo(left.Size) : string.CompareOrdinal(left.Name, right.Name));

        private readonly List<SpaceFile> _files = [];

        /// <summary>What the files kept here take together.</summary>
        public long Size { get; private set; }

        public void Add(SpaceFile file)
        {
            var index = _files.BinarySearch(file, Comparison);
            _files.Insert(index < 0 ? ~index : index, file);
            Size += file.Size;
            if (_files.Count > limit)
            {
                Size -= _files[^1].Size;
                _files.RemoveAt(_files.Count - 1);
            }
        }

        public void AddRange(IEnumerable<SpaceFile> files)
        {
            foreach (var file in files)
                Add(file);
        }

        public IReadOnlyList<SpaceFile> ToArray() => [.. _files];
    }
}
