using System.Runtime.InteropServices;

namespace Refreshify.Core.Space;

/// <summary>
/// Reads one folder with <c>FindFirstFileExW</c>. <c>FindExInfoBasic</c> skips the short names Windows otherwise builds
/// for every entry, and <c>FIND_FIRST_EX_LARGE_FETCH</c> returns a large folder in fewer calls. The search goes
/// through a <c>\\?\</c> name, so paths longer than 260 characters work.
/// </summary>
public sealed partial class WindowsFileSystemWalker : IFileSystemWalker
{
    private const int FindExInfoBasic = 1;

    private const int FindExSearchNameMatch = 0;

    /// <summary><c>FIND_FIRST_EX_LARGE_FETCH</c>: ask for the whole folder in bigger batches.</summary>
    private const uint FindFirstExLargeFetch = 0x2;

    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;
    private const int ErrorDirectory = 267;

    public IReadOnlyList<FileSystemEntry> List(string path, CancellationToken cancellationToken)
    {
        var entries = new List<FileSystemEntry>();
        var handle = FindFirstFileExW(LongPath(Path.Combine(path, "*")), FindExInfoBasic, out var data, FindExSearchNameMatch, 0, FindFirstExLargeFetch);
        if (handle == -1)
        {
            var error = Marshal.GetLastWin32Error();
            if (error is ErrorFileNotFound or ErrorPathNotFound or ErrorDirectory)
                return entries;
            throw error == ErrorAccessDenied
                ? new UnauthorizedAccessException($"Access to '{path}' was denied.")
                : new IOException($"'{path}' couldn't be listed. (Windows error {error})");
        }

        try
        {
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = Name(ref data);
                if (name is not ("." or ".."))
                    entries.Add(Entry(path, name, ref data));
            }
            while (FindNextFileW(handle, out data));
        }
        finally
        {
            FindClose(handle);
        }

        return entries;
    }

    /// <summary>A folder or file from one search result, with its size in bytes and its Windows attributes.</summary>
    private static FileSystemEntry Entry(string parent, string name, ref Win32FindData data)
    {
        var attributes = (FileAttributes)data.FileAttributes;
        var directory = attributes.HasFlag(FileAttributes.Directory);
        var size = directory ? 0 : ((long)data.FileSizeHigh << 32) | data.FileSizeLow;
        // dwReserved0 holds the reparse tag whenever the entry is a reparse point.
        var tag = attributes.HasFlag(FileAttributes.ReparsePoint) ? data.Reserved0 : 0;
        return new FileSystemEntry(name, Path.Combine(parent, name), size, directory, attributes, tag);
    }

    private static string Name(ref Win32FindData data)
    {
        unsafe
        {
            fixed (char* name = data.FileName)
                return new string(name);
        }
    }

    /// <summary>
    /// The <c>\\?\</c> form of a path: the API stores it as it is, so no 260 character limit and no short-name rules.
    /// </summary>
    private static string LongPath(string path)
    {
        var full = Path.GetFullPath(path);
        if (full.StartsWith(@"\\?\", StringComparison.Ordinal))
            return full;
        return full.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + full[2..] : @"\\?\" + full;
    }

    /// <summary>The <c>WIN32_FIND_DATAW</c> one search result fills in. Only the fields the scan uses are read.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct Win32FindData
    {
        public uint FileAttributes;
        public uint CreationTimeLow;
        public uint CreationTimeHigh;
        public uint LastAccessTimeLow;
        public uint LastAccessTimeHigh;
        public uint LastWriteTimeLow;
        public uint LastWriteTimeHigh;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint Reserved0;
        public uint Reserved1;
        public fixed char FileName[260];
        public fixed char AlternateFileName[14];
    }

    [LibraryImport("kernel32.dll", EntryPoint = "FindFirstFileExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint FindFirstFileExW(
        string fileName, int infoLevel, out Win32FindData data, int searchOp, nint searchFilter, uint flags);

    [LibraryImport("kernel32.dll", EntryPoint = "FindNextFileW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FindNextFileW(nint handle, out Win32FindData data);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FindClose(nint handle);
}
