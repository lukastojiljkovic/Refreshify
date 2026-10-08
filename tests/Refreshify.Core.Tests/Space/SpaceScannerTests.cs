using Refreshify.Core.Space;

namespace Refreshify.Core.Tests.Space;

public sealed class SpaceScannerTests
{
    private const string Root = @"C:\tree";

    /// <summary><c>IO_REPARSE_TAG_SYMLINK</c>.</summary>
    private const uint SymbolicLinkTag = 0xA000000C;

    /// <summary><c>IO_REPARSE_TAG_CLOUD</c>, which OneDrive puts on its folders and files.</summary>
    private const uint CloudTag = 0x9000001A;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SpaceScan Scan(FakeTree tree) => SpaceScanner.Scan(Root, tree, null, Ct);

    private static FileSystemEntry FileEntry(string parent, string name, long size) =>
        new(name, Path.Combine(parent, name), size, false, FileAttributes.Archive);

    private static FileSystemEntry FolderEntry(string parent, string name) =>
        new(name, Path.Combine(parent, name), 0, true, FileAttributes.Directory);

    [Fact]
    public void Sizes_and_file_counts_roll_up_to_the_root()
    {
        var sub = Path.Combine(Root, "sub");
        var deep = Path.Combine(sub, "deep");
        var tree = new FakeTree()
            .Folder(Root, FileEntry(Root, "a.txt", 100), FolderEntry(Root, "sub"))
            .Folder(sub, FileEntry(sub, "b.bin", 300), FolderEntry(sub, "deep"))
            .Folder(deep, FileEntry(deep, "c.bin", 600));

        var scan = Scan(tree);

        Assert.Equal(1000, scan.Root.Size);
        Assert.Equal(3, scan.Root.FileCount);
        Assert.Equal("sub", Assert.Single(scan.Root.Folders).Name);
        Assert.Equal(900, scan.Root.Folders[0].Size);
        Assert.Equal(2, scan.Root.Folders[0].FileCount);
        Assert.False(scan.Partial);
    }

    [Fact]
    public void Children_are_the_folders_and_files_together_sorted_by_size()
    {
        var big = Path.Combine(Root, "big");
        var tree = new FakeTree()
            .Folder(Root, FileEntry(Root, "small.bin", 10), FileEntry(Root, "large.bin", 40), FolderEntry(Root, "big"))
            .Folder(big, FileEntry(big, "inside.bin", 30));

        var page = Scan(tree).Root.Children();

        Assert.Equal(["large.bin", "big", "small.bin"], page.Rows.Select(row => row.Name));
        Assert.Equal([50, 38, 13], page.Rows.Select(row => row.Share));
        Assert.True(page.Rows[1].IsFolder);
        Assert.Equal(1, page.Rows[1].FileCount);
        Assert.Equal(0, page.RemainingCount);
        Assert.Equal(0, page.RemainingSize);
    }

    [Fact]
    public void The_list_stops_at_two_hundred_rows_and_summarizes_the_rest()
    {
        var files = Enumerable.Range(1, 250).Select(index => FileEntry(Root, $"file{index:000}.bin", index)).ToArray();
        var tree = new FakeTree().Folder(Root, files);

        var root = Scan(tree).Root;
        var page = root.Children();

        Assert.Equal(200, page.Rows.Count);
        Assert.Equal("file250.bin", page.Rows[0].Name);
        Assert.Equal("file051.bin", page.Rows[^1].Name);
        Assert.Equal(50, page.RemainingCount);
        Assert.Equal(Enumerable.Range(1, 50).Sum(value => (long)value), page.RemainingSize);
        Assert.Equal(50, root.OtherFileCount);
        Assert.Equal(page.RemainingSize, root.OtherFileSize);
    }

    [Fact]
    public void The_cut_counts_folders_and_files_together()
    {
        var entries = Enumerable.Range(1, 250).Select(index => FileEntry(Root, $"file{index:000}.bin", index)).ToList();
        var tree = new FakeTree();
        for (var index = 0; index < 3; index++)
        {
            var folder = Path.Combine(Root, $"folder{index}");
            entries.Add(FolderEntry(Root, $"folder{index}"));
            tree.Folder(folder, FileEntry(folder, "inside.bin", 1000));
        }

        tree.Folder(Root, [.. entries]);
        var root = Scan(tree).Root;
        var page = root.Children();

        Assert.Equal(200, page.Rows.Count);
        Assert.Equal(53, page.RemainingCount);
        Assert.Equal(root.Size - page.Rows.Sum(row => row.Size), page.RemainingSize);
    }

    [Fact]
    public void A_folder_keeps_its_hundred_largest_files()
    {
        var left = Path.Combine(Root, "left");
        var right = Path.Combine(Root, "right");
        var tree = new FakeTree()
            .Folder(Root, FolderEntry(Root, "left"), FolderEntry(Root, "right"))
            .Folder(left, [.. Enumerable.Range(1, 80).Select(index => FileEntry(left, $"l{index:000}.bin", index * 10))])
            .Folder(right, [.. Enumerable.Range(1, 80).Select(index => FileEntry(right, $"r{index:000}.bin", (index * 10) + 5))]);

        var root = Scan(tree).Root;

        Assert.Equal(100, root.LargestFiles.Count);
        Assert.Equal(805, root.LargestFiles[0].Size);
        Assert.Equal(310, root.LargestFiles[^1].Size);
        Assert.Equal(80, root.Folders[0].LargestFiles.Count);
        Assert.True(root.LargestFiles.SequenceEqual(root.LargestFiles.OrderByDescending(file => file.Size)));
    }

    [Fact]
    public void Folders_that_cannot_be_read_are_counted_and_named()
    {
        var denied = Path.Combine(Root, "denied");
        var second = Path.Combine(Root, "second");
        var tree = new FakeTree()
            .Folder(Root, FolderEntry(Root, "denied"), FolderEntry(Root, "second"), FileEntry(Root, "ok.bin", 10))
            .Denied(denied)
            .Denied(second);

        var scan = Scan(tree);

        Assert.Equal(2, scan.UnreadableFolders);
        Assert.Equal([denied, second], scan.UnreadablePaths.Order());
        Assert.Equal(2, scan.Root.Folders.Count);
        Assert.All(scan.Root.Folders, folder => Assert.Equal(0, folder.Size));
        Assert.Equal(10, scan.Root.Size);
    }

    [Fact]
    public void Only_the_first_fifty_unreadable_folders_are_named()
    {
        var entries = Enumerable.Range(0, 60).Select(index => FolderEntry(Root, $"denied{index:00}")).ToArray();
        var tree = new FakeTree().Folder(Root, entries);
        foreach (var entry in entries)
            tree.Denied(entry.Path);

        var scan = Scan(tree);

        Assert.Equal(60, scan.UnreadableFolders);
        Assert.Equal(SpaceScanner.ReportedUnreadablePaths, scan.UnreadablePaths.Count);
        Assert.All(scan.UnreadablePaths, path => Assert.StartsWith(Root, path));
    }

    [Fact]
    public void Share_is_the_percent_of_the_folder_rounded_half_away_from_zero()
    {
        var midpoint = Scan(new FakeTree().Folder(Root, FileEntry(Root, "a.bin", 7), FileEntry(Root, "b.bin", 1))).Root.Children();
        var thirds = Scan(new FakeTree().Folder(
            Root, FileEntry(Root, "a.bin", 1), FileEntry(Root, "b.bin", 1), FileEntry(Root, "c.bin", 1))).Root.Children();

        Assert.Equal([88, 13], midpoint.Rows.Select(row => row.Share));
        Assert.Equal([33, 33, 33], thirds.Rows.Select(row => row.Share));
    }

    [Fact]
    public void Online_only_files_are_skipped_and_counted()
    {
        var offline = new FileSystemEntry("cloud.bin", Path.Combine(Root, "cloud.bin"), 5000, false, FileAttributes.Archive | FileAttributes.Offline);
        var placeholder = new FileSystemEntry("online.bin", Path.Combine(Root, "online.bin"), 9000, false,
            FileAttributes.Archive | FileSystemEntry.RecallOnDataAccess);
        var tree = new FakeTree().Folder(Root, offline, placeholder, FileEntry(Root, "here.bin", 100));

        var scan = Scan(tree);

        Assert.Equal(2, scan.OnlineOnlyFiles);
        Assert.Equal(100, scan.Root.Size);
        Assert.Equal(1, scan.Root.FileCount);
        Assert.Equal(["here.bin"], scan.Root.Files.Select(file => file.Name));
    }

    [Fact]
    public void Reparse_points_are_not_followed_or_counted()
    {
        var link = Path.Combine(Root, "link");
        var tree = new FakeTree()
            .Folder(Root, new FileSystemEntry("link", link, 0, true, FileAttributes.Directory | FileAttributes.ReparsePoint, SymbolicLinkTag),
                FileEntry(Root, "here.bin", 100))
            .Folder(link, FileEntry(link, "outside.bin", 1_000_000));

        var scan = Scan(tree);

        Assert.Equal(100, scan.Root.Size);
        Assert.Empty(scan.Root.Folders);
        Assert.DoesNotContain(link, tree.Listed);
    }

    [Fact]
    public void Folders_deeper_than_the_limit_are_counted_as_unreadable()
    {
        var tree = new FakeTree();
        var path = Root;
        for (var depth = 1; depth <= SpaceScanner.MaxDepth + 5; depth++)
        {
            tree.Folder(path, FolderEntry(path, "d"));
            path = Path.Combine(path, "d");
        }

        var scan = Scan(tree);

        Assert.Equal(1, scan.UnreadableFolders);
        Assert.False(scan.Partial);
    }

    [Fact]
    public void Cloud_folders_and_files_kept_on_this_pc_are_counted()
    {
        var oneDrive = Path.Combine(Root, "OneDrive");
        var tree = new FakeTree()
            .Folder(Root, new FileSystemEntry("OneDrive", oneDrive, 0, true, FileAttributes.Directory | FileAttributes.ReparsePoint, CloudTag))
            .Folder(oneDrive, new FileSystemEntry("kept.bin", Path.Combine(oneDrive, "kept.bin"), 500, false, FileAttributes.ReparsePoint, CloudTag));

        var scan = Scan(tree);

        Assert.Equal(500, scan.Root.Size);
        Assert.Equal("OneDrive", Assert.Single(scan.Root.Folders).Name);
    }

    [Fact]
    public void Cancelling_mid_scan_returns_the_partial_tree()
    {
        var one = Path.Combine(Root, "one");
        var two = Path.Combine(one, "two");
        var three = Path.Combine(one, "three");
        using var cancellation = new CancellationTokenSource();
        var tree = new FakeTree { OnList = path => { if (path.Equals(two, StringComparison.OrdinalIgnoreCase)) cancellation.Cancel(); } }
            .Folder(Root, FolderEntry(Root, "one"))
            .Folder(one, FileEntry(one, "a.bin", 500), FolderEntry(one, "two"), FolderEntry(one, "three"))
            .Folder(two, FileEntry(two, "b.bin", 700))
            .Folder(three, FileEntry(three, "c.bin", 900));

        var scan = SpaceScanner.Scan(Root, tree, null, cancellation.Token);

        Assert.True(scan.Partial);
        Assert.Equal(500, scan.Root.Size);
        Assert.DoesNotContain(three, tree.Listed);
        Assert.Equal(["two"], scan.Root.Folders[0].Folders.Select(folder => folder.Name));
    }

    [Fact]
    public void Hidden_space_is_what_windows_uses_beyond_what_the_scan_counted()
    {
        Assert.Equal(600, SpaceScanner.UnseenBytes(1000, 400));
        Assert.Null(SpaceScanner.UnseenBytes(400, 400));
        Assert.Null(SpaceScanner.UnseenBytes(100, 400));
    }

    /// <summary>The folders and files a scan reads instead of the disk.</summary>
    private sealed class FakeTree : IFileSystemWalker
    {
        private readonly Dictionary<string, IReadOnlyList<FileSystemEntry>> _folders = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _denied = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Listed { get; } = [];

        /// <summary>Called with every path as it is listed, so a test can cancel the scan in the middle of it.</summary>
        public Action<string>? OnList { get; init; }

        public FakeTree Folder(string path, params FileSystemEntry[] entries)
        {
            _folders[path] = entries;
            return this;
        }

        public FakeTree Denied(string path)
        {
            _denied.Add(path);
            return this;
        }

        public IReadOnlyList<FileSystemEntry> List(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (Listed)
                Listed.Add(path);
            OnList?.Invoke(path);
            return _denied.Contains(path)
                ? throw new UnauthorizedAccessException($"Access to '{path}' was denied.")
                : _folders.TryGetValue(path, out var entries) ? entries : [];
        }
    }
}
