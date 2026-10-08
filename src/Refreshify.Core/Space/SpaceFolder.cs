namespace Refreshify.Core.Space;

/// <summary>A file the scan counted: its name, its full path and its logical size.</summary>
public sealed record SpaceFile(string Name, string Path, long Size);

/// <summary>One child of a folder in the list: a folder, or a file.</summary>
/// <param name="FileCount">Files in the folder, directly and below it. Zero for a file.</param>
/// <param name="Share">The child's share of the folder it is in, as a whole percent.</param>
public sealed record SpaceChild(string Name, string Path, long Size, bool IsFolder, long FileCount, int Share);

/// <summary>The rows a list shows, cut to the page, and what the "smaller items" line reports.</summary>
public sealed record SpaceChildPage(IReadOnlyList<SpaceChild> Rows, long RemainingCount, long RemainingSize);

/// <summary>
/// A folder in the scan tree. It knows what everything below it holds and, for the list, which of its own children are
/// worth showing: the largest <see cref="SpaceScanner.PageRows"/> direct files, and the largest
/// <see cref="SpaceScanner.LargestFileCount"/> files anywhere below it. The smaller files are only counted, so a tree
/// with millions of files doesn't grow the scan in memory.
/// </summary>
public sealed class SpaceFolder
{
    internal SpaceFolder(string name, string path)
    {
        Name = name;
        Path = path;
    }

    public string Name { get; }

    public string Path { get; }

    /// <summary>Bytes in this folder and everything below it.</summary>
    public long Size { get; internal set; }

    /// <summary>Files in this folder and everything below it.</summary>
    public long FileCount { get; internal set; }

    /// <summary>The folders directly inside this one, largest first.</summary>
    public IReadOnlyList<SpaceFolder> Folders { get; internal set; } = [];

    /// <summary>The largest files directly inside this one, largest first.</summary>
    public IReadOnlyList<SpaceFile> Files { get; internal set; } = [];

    /// <summary>Files directly inside this one that are too small to be listed.</summary>
    public long OtherFileCount { get; internal set; }

    /// <summary>What the files counted by <see cref="OtherFileCount"/> take together.</summary>
    public long OtherFileSize { get; internal set; }

    /// <summary>The largest files anywhere below this folder, largest first, up to
    /// <see cref="SpaceScanner.LargestFileCount"/> of them.</summary>
    public IReadOnlyList<SpaceFile> LargestFiles { get; internal set; } = [];

    /// <summary>
    /// The children to list: the folders and the largest files directly inside this one, together, largest first. At
    /// most <paramref name="page"/> rows come back; the rest are counted and sized for the "smaller items" line.
    /// </summary>
    public SpaceChildPage Children(int page = SpaceScanner.PageRows)
    {
        var rows = new List<SpaceChild>(Folders.Count + Files.Count);
        rows.AddRange(Folders.Select(folder => Row(folder.Name, folder.Path, folder.Size, isFolder: true, folder.FileCount)));
        rows.AddRange(Files.Select(file => Row(file.Name, file.Path, file.Size, isFolder: false, 0)));
        rows.Sort(Compare);

        var shown = rows.Count > page ? rows[..page] : rows;
        var total = Folders.Count + Files.Count + OtherFileCount;
        return new SpaceChildPage(shown, total - shown.Count, Size - shown.Sum(row => row.Size));
    }

    private SpaceChild Row(string name, string path, long size, bool isFolder, long fileCount) =>
        new(name, path, size, isFolder, fileCount, Share(size));

    /// <summary>Rounded the way the app's percent formatting rounds, half away from zero.</summary>
    private int Share(long size) => Size <= 0 ? 0 : (int)Math.Round(100.0 * size / Size, MidpointRounding.AwayFromZero);

    private static int Compare(SpaceChild left, SpaceChild right)
    {
        if (left.Size != right.Size)
            return right.Size.CompareTo(left.Size);
        return left.IsFolder != right.IsFolder ? (left.IsFolder ? -1 : 1) : string.CompareOrdinal(left.Name, right.Name);
    }

    public override string ToString() => Path;
}
