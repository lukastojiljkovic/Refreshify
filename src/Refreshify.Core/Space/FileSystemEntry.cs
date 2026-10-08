namespace Refreshify.Core.Space;

/// <summary>A folder or file as a walker found it, with the attributes the scan decides on.</summary>
/// <param name="Size">The logical file size in bytes. Zero for a folder, whose size comes from what it holds.</param>
/// <param name="ReparseTag">The reparse tag when <paramref name="Attributes"/> has <see cref="FileAttributes.ReparsePoint"/>.</param>
public sealed record FileSystemEntry(
    string Name, string Path, long Size, bool IsDirectory, FileAttributes Attributes, uint ReparseTag = 0)
{
    /// <summary><c>FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS</c>, which <see cref="FileAttributes"/> doesn't name.</summary>
    public const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;

    /// <summary>The tag bit of reparse points that stand for another name: junctions, symbolic links, mount points.</summary>
    private const uint NameSurrogate = 0x20000000;

    /// <summary>
    /// A junction, symbolic link or mount point: never followed and never counted. Other reparse points, such as
    /// OneDrive folders and files kept on this PC or compressed system files, hold their own data and are scanned.
    /// </summary>
    public bool IsLink => Attributes.HasFlag(FileAttributes.ReparsePoint) && (ReparseTag & NameSurrogate) != 0;

    /// <summary>A cloud file that isn't on this PC, so it takes no space here and isn't counted.</summary>
    public bool IsOnlineOnly =>
        Attributes.HasFlag(FileAttributes.Offline) || Attributes.HasFlag(RecallOnDataAccess);
}

/// <summary>
/// Lists one folder at a time, which is what makes the scan testable with a fake tree. The real walker reads the
/// folder with <c>FindFirstFileExW</c>.
/// </summary>
public interface IFileSystemWalker
{
    /// <summary>The entries directly in the folder at <paramref name="path"/>, in any order.</summary>
    /// <exception cref="UnauthorizedAccessException">The folder couldn't be read.</exception>
    /// <exception cref="IOException">The folder couldn't be read.</exception>
    IReadOnlyList<FileSystemEntry> List(string path, CancellationToken cancellationToken);
}
