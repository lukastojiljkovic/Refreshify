using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Refreshify.Core.Platform;

public static partial class SystemState
{
    /// <summary>The System Restore client in <c>SPP\Clients</c>; its value lists the protected volumes.</summary>
    private const string SystemRestoreClient = "{09F7EDC5-294E-4180-AF6A-FB0E6A0E9513}";

    /// <summary>The root of the drive Windows is installed on, such as <c>C:\</c>.</summary>
    public static string SystemDrive { get; } = Path.GetPathRoot(Environment.SystemDirectory)!;

    public static long FreeSpace => new DriveInfo(SystemDrive).AvailableFreeSpace;

    /// <summary>
    /// Component servicing is waiting for a restart to finish installing or repairing something. Until then DISM and SFC
    /// can't repair.
    /// </summary>
    public static bool IsComponentRestartPending =>
        KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") ||
        File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "WinSxS", "pending.xml"));

    /// <summary>Servicing or Windows Update is waiting for a restart; files staged in the update cache are still needed.</summary>
    public static bool IsRestartPending =>
        IsComponentRestartPending ||
        KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");

    /// <summary>Whether System Protection (restore points) is on for the system drive.</summary>
    public static bool IsSystemProtectionOn
    {
        get
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SPP\Clients");
            return key?.GetValue(SystemRestoreClient) is string[] volumes && VolumeName(SystemDrive) is { } volume &&
                volumes.Any(entry => entry.StartsWith(volume, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static bool KeyExists(string path)
    {
        using var key = Registry.LocalMachine.OpenSubKey(path);
        return key is not null;
    }

    /// <summary>The volume GUID path, such as <c>\\?\Volume{…}\</c>, which identifies a volume even if its letter changes.</summary>
    private static unsafe string? VolumeName(string mountPoint)
    {
        const int Length = 50;
        var buffer = stackalloc char[Length];
        return GetVolumeNameForVolumeMountPointW(mountPoint, buffer, Length) ? new string(buffer) : null;
    }

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool GetVolumeNameForVolumeMountPointW(string mountPoint, char* volumeName, int length);
}
