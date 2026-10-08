using System.Diagnostics;
using Refreshify.Core.Space;

namespace Refreshify.Core.Tests.Space;

/// <summary>The scan against a real folder, which is the only test here that touches the disk.</summary>
public sealed class WindowsFileSystemWalkerTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("refreshify-space-");

    public void Dispose()
    {
        // Remove junctions first: a recursive delete would otherwise treat them as mount points.
        foreach (var link in _root.EnumerateDirectories("*", SearchOption.AllDirectories).Where(IsJunction))
            link.Delete();
        _root.Delete(recursive: true);
    }

    [Fact]
    public void A_real_folder_gives_the_right_sizes_and_junctions_are_not_followed()
    {
        Write("a.bin", 1000);
        Write(@"sub\b.bin", 2000);
        Write(@"sub\deep\c.bin", 4000);
        var outside = Directory.CreateTempSubdirectory("refreshify-space-outside-");
        try
        {
            File.WriteAllBytes(Path.Combine(outside.FullName, "outside.bin"), new byte[1_000_000]);
            var junction = Path.Combine(_root.FullName, "link");
            using (var mklink = Process.Start(new ProcessStartInfo(
                "cmd.exe", $"/c mklink /J \"{junction}\" \"{outside.FullName}\"")
            { CreateNoWindow = true, UseShellExecute = false })!)
            {
                mklink.WaitForExit();
            }
            Assert.True(Directory.Exists(junction));

            var scan = SpaceScanner.Scan(_root.FullName, new WindowsFileSystemWalker(), null, TestContext.Current.CancellationToken);

            Assert.False(scan.Partial);
            Assert.Equal(_root.FullName, scan.Root.Path);
            Assert.Equal(7000, scan.Root.Size);
            Assert.Equal(3, scan.Root.FileCount);
            var sub = Assert.Single(scan.Root.Folders);
            Assert.Equal("sub", sub.Name);
            Assert.Equal(6000, sub.Size);
            Assert.Equal(2, sub.FileCount);
        }
        finally
        {
            foreach (var link in _root.EnumerateDirectories().Where(IsJunction))
                link.Delete();
            outside.Delete(recursive: true);
        }
    }

    private void Write(string relativePath, int size)
    {
        var path = Path.Combine(_root.FullName, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[size]);
    }

    private static bool IsJunction(DirectoryInfo directory) => directory.Attributes.HasFlag(FileAttributes.ReparsePoint);
}
