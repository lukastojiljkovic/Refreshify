using System.Runtime.InteropServices;

namespace Refreshify.Core.Platform;

/// <summary>The Recycle Bin of every drive, for the signed-in user.</summary>
public static partial class RecycleBin
{
    private const uint NoConfirmation = 0x1;
    private const uint NoProgressUi = 0x2;
    private const uint NoSound = 0x4;
    private const int Unexpected = unchecked((int)0x8000FFFF);

    public static (long Bytes, long Items) Query()
    {
        var info = new QueryInfo { Size = Marshal.SizeOf<QueryInfo>() };
        Marshal.ThrowExceptionForHR(SHQueryRecycleBinW(null, ref info));
        return (info.Bytes, info.Items);
    }

    public static void Empty()
    {
        var result = SHEmptyRecycleBinW(0, null, NoConfirmation | NoProgressUi | NoSound);

        // E_UNEXPECTED means it was already empty.
        if (result != Unexpected)
            Marshal.ThrowExceptionForHR(result);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct QueryInfo
    {
        public int Size;
        public long Bytes;
        public long Items;
    }

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHQueryRecycleBinW(string? rootPath, ref QueryInfo info);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHEmptyRecycleBinW(nint window, string? rootPath, uint flags);
}
