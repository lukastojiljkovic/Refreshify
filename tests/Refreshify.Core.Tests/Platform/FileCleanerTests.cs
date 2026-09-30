using System.Diagnostics;
using Refreshify.Core.Platform;

namespace Refreshify.Core.Tests.Platform;

public sealed class FileCleanerTests : IDisposable
{
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("refreshify-tests-");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        // Remove junctions first: a recursive delete would otherwise treat them as mount points.
        foreach (var junction in _root.EnumerateDirectories("*", SearchOption.AllDirectories).Where(d => d.Attributes.HasFlag(FileAttributes.ReparsePoint)))
            junction.Delete();
        _root.Delete(recursive: true);
    }

    private string CreateFile(string relativePath, int size = 100, TimeSpan? age = null)
    {
        var path = Path.Combine(_root.FullName, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[size]);
        if (age is { } value)
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow - value);
        return path;
    }

    private CleanupStats Clean(string pattern = "*", bool recursive = true, TimeSpan? minimumAge = null, Action<long>? deleted = null) =>
        FileCleaner.Clean(new CleanupTarget(_root.FullName, pattern, recursive, minimumAge is not null), minimumAge ?? TimeSpan.Zero, deleted, Ct);

    [Fact]
    public void Reports_each_deleted_file_as_it_goes_but_not_skipped_ones()
    {
        CreateFile("a.tmp", 10);
        CreateFile(@"sub\b.tmp", 20);
        using var locked = new FileStream(CreateFile("locked.tmp", 40), FileMode.Open, FileAccess.Read, FileShare.None);
        var reported = new List<long>();

        Clean(deleted: reported.Add);

        Assert.Equal([10, 20], reported.Order());
    }

    [Fact]
    public void Deletes_files_older_than_the_minimum_age_and_keeps_newer_ones()
    {
        var old = CreateFile("old.tmp", 300, age: 3 * Day);
        var fresh = CreateFile("fresh.tmp", 50);

        var stats = Clean(minimumAge: Day);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(fresh));
        Assert.Equal(new CleanupStats(300, 1, 0), stats);
    }

    [Fact]
    public void Deletes_everything_when_the_target_has_no_age_filter()
    {
        CreateFile("a.tmp", 10);
        CreateFile(@"sub\b.tmp", 20);

        var stats = Clean();

        Assert.Equal(new CleanupStats(30, 2, 0), stats);
        Assert.Empty(_root.EnumerateFileSystemInfos());
    }

    [Fact]
    public void Deletes_read_only_files()
    {
        var path = CreateFile("readonly.tmp");
        File.SetAttributes(path, FileAttributes.ReadOnly);

        Clean();

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Files_in_use_are_skipped_and_counted()
    {
        var path = CreateFile("locked.tmp");
        using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        var stats = Clean();

        Assert.True(File.Exists(path));
        Assert.Equal(new CleanupStats(0, 0, 1), stats);
    }

    [Fact]
    public void Only_files_matching_the_pattern_are_deleted_and_subfolders_are_left_alone_when_not_recursive()
    {
        var cache = CreateFile("thumbcache_256.db");
        var other = CreateFile("settings.db");
        var nested = CreateFile(@"sub\thumbcache_32.db");

        Clean("thumbcache_*.db", recursive: false);

        Assert.False(File.Exists(cache));
        Assert.True(File.Exists(other));
        Assert.True(File.Exists(nested));
    }

    [Fact]
    public void Junctions_are_never_followed()
    {
        var outside = Directory.CreateTempSubdirectory("refreshify-outside-");
        try
        {
            var precious = Path.Combine(outside.FullName, "precious.txt");
            File.WriteAllText(precious, "keep me");
            var junction = Path.Combine(_root.FullName, "link");
            using (var mklink = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junction}\" \"{outside.FullName}\"") { CreateNoWindow = true, UseShellExecute = false })!)
                mklink.WaitForExit();
            Assert.True(Directory.Exists(junction));

            Clean();

            Assert.True(File.Exists(precious));
        }
        finally
        {
            foreach (var junction in _root.EnumerateDirectories().Where(d => d.Attributes.HasFlag(FileAttributes.ReparsePoint)))
                junction.Delete();
            outside.Delete(recursive: true);
        }
    }

    [Fact]
    public void Empty_subfolders_are_removed_unless_they_are_newer_than_the_minimum_age()
    {
        var old = Directory.CreateDirectory(Path.Combine(_root.FullName, "old"));
        Directory.SetCreationTimeUtc(old.FullName, DateTime.UtcNow - 3 * Day);
        var fresh = Directory.CreateDirectory(Path.Combine(_root.FullName, "fresh"));

        Clean(minimumAge: Day);

        Assert.False(old.Exists);
        Assert.True(Directory.Exists(fresh.FullName));
        Assert.True(_root.Exists);
    }

    [Fact]
    public void A_single_file_target_is_deleted()
    {
        var path = CreateFile("MEMORY.DMP", 500);

        var stats = FileCleaner.Clean(new CleanupTarget(path), TimeSpan.Zero, null, Ct);

        Assert.False(File.Exists(path));
        Assert.Equal(500, stats.BytesFreed);
    }

    [Fact]
    public void A_missing_target_is_nothing_to_clean() =>
        Assert.Equal(CleanupStats.Empty, FileCleaner.Clean(new CleanupTarget(Path.Combine(_root.FullName, "missing")), TimeSpan.Zero, null, Ct));
}
