using Microsoft.Win32;

namespace Refreshify.Core.Platform;

public static class SystemState
{
    /// <summary>The root of the drive Windows is installed on, such as <c>C:\</c>.</summary>
    public static string SystemDrive { get; } = Path.GetPathRoot(Environment.SystemDirectory)!;

    public static long FreeSpace => new DriveInfo(SystemDrive).AvailableFreeSpace;

    /// <summary>
    /// Windows servicing is waiting for a restart to finish installing or repairing components. Until then DISM and SFC
    /// can't repair, and files staged in the update cache are still needed.
    /// </summary>
    public static bool IsRestartPending =>
        KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") ||
        KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") ||
        File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "WinSxS", "pending.xml"));

    private static bool KeyExists(string path)
    {
        using var key = Registry.LocalMachine.OpenSubKey(path);
        return key is not null;
    }
}
