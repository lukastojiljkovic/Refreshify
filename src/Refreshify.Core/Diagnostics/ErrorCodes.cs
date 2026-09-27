using System.Globalization;
using System.Text.RegularExpressions;

namespace Refreshify.Core.Diagnostics;

/// <summary>Documented error codes the tools recognize. Values are HRESULTs unless noted.</summary>
public static partial class ErrorCodes
{
    /// <summary>Win32 <c>ERROR_CANCELLED</c>: the user declined the UAC prompt.</summary>
    public const int ElevationCancelled = 1223;

    /// <summary>Win32 <c>ERROR_SUCCESS_REBOOT_REQUIRED</c>, returned by DISM as its exit code.</summary>
    public const int SuccessRebootRequired = 3010;

    public const int FileNotFound = unchecked((int)0x80070002);
    public const int PathNotFound = unchecked((int)0x80070003);
    public const int DiskFull = unchecked((int)0x80070070);
    public const int ServiceDisabled = unchecked((int)0x80070422);
    public const int Timeout = unchecked((int)0x800705B4);
    public const int ComponentStoreCorrupt = unchecked((int)0x80073712);

    // DISM / CBS
    public const int SourceFilesNotFound = unchecked((int)0x800F081F);
    public const int SourceDownloadFailed = unchecked((int)0x800F0906);
    public const int SourceBlockedByPolicy = unchecked((int)0x800F0907);
    public const int PendingOperations = unchecked((int)0x800F082F);

    // Windows Update Agent and WinINet
    public const int UpdateInstallNotAllowed = unchecked((int)0x80240016);
    public const int UpdateNotDownloaded = unchecked((int)0x80246007);
    public const int UpdateDataStoreNoData = unchecked((int)0x80248007);
    public const int UpdateNameNotResolved = unchecked((int)0x8024402C);
    public const int InternetTimeout = unchecked((int)0x80072EE2);
    public const int InternetNameNotResolved = unchecked((int)0x80072EE7);
    public const int InternetCannotConnect = unchecked((int)0x80072EFD);

    // winget (AppInstallerErrors.h)
    public const int WingetSourcesInvalid = unchecked((int)0x8A15000B);
    public const int WingetSourceDataMissing = unchecked((int)0x8A15000F);
    public const int WingetNoApplicationsFound = unchecked((int)0x8A150014);
    public const int WingetUpdateNotApplicable = unchecked((int)0x8A15002B);
    public const int WingetUpdateAllHasFailure = unchecked((int)0x8A15002C);
    public const int WingetSourceOpenFailed = unchecked((int)0x8A150045);
    public const int WingetPackageInUse = unchecked((int)0x8A150101);
    public const int WingetFileInUse = unchecked((int)0x8A150103);
    public const int WingetDiskFull = unchecked((int)0x8A150105);
    public const int WingetNoNetwork = unchecked((int)0x8A150107);
    public const int WingetRebootRequiredToFinish = unchecked((int)0x8A150109);
    public const int WingetPackageInUseByApplication = unchecked((int)0x8A150111);

    /// <summary>Small exit codes in decimal, HRESULTs in hex, as Microsoft documents them.</summary>
    public static string Format(int code) =>
        code is >= 0 and < 0x10000 ? code.ToString(CultureInfo.InvariantCulture) : $"0x{code:X8}";

    /// <summary>The first HRESULT written as <c>0x8XXXXXXX</c> in <paramref name="text"/>, for tools that print their error.</summary>
    public static int? FindHResult(IEnumerable<string> text)
    {
        foreach (var line in text)
        {
            var match = HResultPattern().Match(line);
            if (match.Success)
                return unchecked((int)uint.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        return null;
    }

    [GeneratedRegex(@"0x([89A-Fa-f][0-9A-Fa-f]{7})\b")]
    private static partial Regex HResultPattern();
}
