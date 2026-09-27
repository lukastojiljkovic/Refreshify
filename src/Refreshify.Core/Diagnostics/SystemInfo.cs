using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Refreshify.Core.Diagnostics;

public sealed record SystemInfo(string Edition, string Version, string Build, string Architecture, string DisplayLanguage, string AppVersion)
{
    private const int FirstWindows11Build = 22000;

    public static SystemInfo Current(string appVersion)
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var build = int.TryParse(key?.GetValue("CurrentBuildNumber") as string, CultureInfo.InvariantCulture, out var number) ? number : 0;
        var revision = key?.GetValue("UBR") is int ubr ? ubr : 0;

        return new SystemInfo(
            ProductName(key?.GetValue("ProductName") as string ?? "Windows", build),
            key?.GetValue("DisplayVersion") as string ?? key?.GetValue("ReleaseId") as string ?? "unknown",
            $"{build}.{revision}",
            RuntimeInformation.OSArchitecture.ToString(),
            CultureInfo.CurrentUICulture.Name,
            appVersion);
    }

    /// <summary>Windows 11 still says "Windows 10" in its registry product name; the build number tells them apart.</summary>
    public static string ProductName(string registryName, int build) =>
        build >= FirstWindows11Build ? registryName.Replace("Windows 10", "Windows 11", StringComparison.Ordinal) : registryName;
}
