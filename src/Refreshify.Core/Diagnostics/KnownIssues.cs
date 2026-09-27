using static Refreshify.Core.Diagnostics.ErrorCodes;

namespace Refreshify.Core.Diagnostics;

public enum RemedyKind
{
    /// <summary>Runs the fix tools, then retries the failed tool once. Asks first when automatic fixes are turned off.</summary>
    Automatic,

    /// <summary>A system change the user has to agree to.</summary>
    AskFirst,

    /// <summary>Offers to restart Windows.</summary>
    Restart,

    /// <summary>The user picks a Windows ISO for DISM to repair from.</summary>
    WindowsImage,

    /// <summary>Explains what to do; the user can retry.</summary>
    Manual,
}

/// <param name="FixToolIds">Catalog tools that fix the issue, run in order.</param>
/// <param name="FixLabel">The button that runs the fix.</param>
/// <param name="RetryAfterFix">Whether the failed tool runs again after the fix.</param>
public sealed record KnownIssue(
    string Id,
    string Title,
    string Explanation,
    RemedyKind Remedy,
    IReadOnlyList<string> FixToolIds,
    string? FixLabel = null,
    bool RetryAfterFix = true);

public static class KnownIssues
{
    public const string RestartPending = "restart-pending";
    public const string SfcUnrepairable = "sfc-unrepairable";
    public const string SfcServiceDisabled = "sfc-service-disabled";
    public const string DismSourceUnavailable = "dism-source-unavailable";
    public const string WuServiceDisabled = "wu-service-disabled";
    public const string WuCacheDamaged = "wu-cache-damaged";
    public const string ComponentStoreDamaged = "component-store-damaged";
    public const string WuBusy = "wu-busy";
    public const string DiskFull = "disk-full";
    public const string NoConnection = "no-connection";
    public const string WingetMissing = "winget-missing";
    public const string WingetSources = "winget-sources";
    public const string AppsInUse = "apps-in-use";
    public const string SystemProtectionOff = "system-protection-off";
    public const string DiskErrors = "disk-errors";

    public static IReadOnlyList<KnownIssue> All { get; } =
    [
        new(RestartPending, "Windows needs to restart",
            "Windows is waiting for a restart to finish installing or repairing something, and some steps can't run until then. Restart your PC, then run them again.",
            RemedyKind.Restart, [], "Restart now", RetryAfterFix: false),
        new(SfcUnrepairable, "Some system files couldn't be repaired",
            "System file check found damaged files it couldn't fix, because the copies it repairs from are damaged too. Refreshify repairs those copies with System image repair, then checks again.",
            RemedyKind.Automatic, ["dism-restorehealth"], "Repair and check again"),
        new(SfcServiceDisabled, "The repair service is turned off",
            "System file check needs the Windows Modules Installer service, which is disabled on this PC. Refreshify sets it back to start when needed, as Windows does by default, then checks again.",
            RemedyKind.Automatic, ["fix-enable-trustedinstaller"], "Turn it on and check again"),
        new(DismSourceUnavailable, "Windows couldn't get its repair files",
            "System image repair needs clean copies of Windows files and couldn't download them from Windows Update. This is common on PCs whose updates are managed by an organization. You can repair from a Windows installation image (ISO) of the same version and language instead.",
            RemedyKind.WindowsImage, ["dism-restorehealth"], "Choose a Windows image"),
        new(WuServiceDisabled, "The Windows Update service is turned off",
            "Windows Update can't run while its service is disabled. Refreshify sets it back to start when needed, as Windows does by default, then tries again.",
            RemedyKind.Automatic, ["fix-enable-wuauserv"], "Turn it on and try again"),
        new(WuCacheDamaged, "The Windows Update cache is damaged",
            "Windows Update keeps its downloads and records in a cache that is missing files or damaged. Refreshify resets it, Windows builds a new one, and then it tries again. Your update history may look empty afterwards; installed updates aren't affected.",
            RemedyKind.Automatic, ["reset-windows-update"], "Reset it and try again"),
        new(ComponentStoreDamaged, "Windows' repair store is damaged",
            "Windows keeps the files it needs for updates and repairs in its component store, and part of it is damaged. Refreshify repairs it with System image repair, then tries again.",
            RemedyKind.Automatic, ["dism-restorehealth"], "Repair and try again"),
        new(WuBusy, "Windows Update is busy",
            "Another update is being installed, or Windows Update is waiting for a restart. Let it finish or restart your PC, then try again.",
            RemedyKind.Manual, []),
        new(DiskFull, "Not enough free space",
            "The drive is too full to finish. Refreshify cleans up temporary files and caches, then tries again. If that isn't enough, uninstall apps or move files you don't need, then try again.",
            RemedyKind.Automatic, ["temp-files", "windows-temp", "delivery-optimization", "crash-dumps"], "Free up space and try again"),
        new(NoConnection, "No internet connection",
            "This step needs the internet and couldn't reach Microsoft's servers. Check that you're online and that a VPN or firewall isn't blocking the connection, then try again.",
            RemedyKind.Manual, []),
        new(WingetMissing, "The app updater isn't available",
            "App updates use winget, which comes with Microsoft's App Installer, and it isn't registered for your account. Refreshify can register the App Installer that's already on your PC, then try again.",
            RemedyKind.AskFirst, ["fix-register-app-installer"], "Register it and try again"),
        new(WingetSources, "The app update sources are damaged",
            "winget couldn't read its list of apps. Refreshify resets its sources to the defaults, then tries again.",
            RemedyKind.Automatic, ["fix-winget-source-reset"], "Reset them and try again"),
        new(AppsInUse, "Some apps couldn't update",
            "Some apps couldn't be updated, usually because they were open. Close them and try again; the technical details list what winget reported.",
            RemedyKind.Manual, []),
        new(SystemProtectionOff, "System Protection is off",
            "Refreshify creates a restore point before it changes anything, so you can undo the changes. Restore points need System Protection, which is turned off for the system drive. Refreshify can turn it on; Windows then reserves some disk space for restore points, which you can change in System Properties.",
            RemedyKind.AskFirst, ["fix-enable-system-protection"], "Turn it on and try again"),
        new(DiskErrors, "The disk needs a repair at the next restart",
            "Disk check found file system problems that can't be fixed while Windows is running. Refreshify can schedule a repair that runs the next time you restart your PC; that restart takes longer than usual.",
            RemedyKind.AskFirst, ["fix-schedule-disk-repair"], "Schedule the repair", RetryAfterFix: false),
    ];

    private static readonly Dictionary<string, KnownIssue> ById = All.ToDictionary(issue => issue.Id);

    public static KnownIssue? Find(string? id) => id is not null && ById.TryGetValue(id, out var issue) ? issue : null;

    public static KnownIssue Get(string id) => ById[id];

    /// <summary>Codes that mean the same thing for every tool.</summary>
    public static string? FromHResult(int code) => code switch
    {
        ErrorCodes.DiskFull or WingetDiskFull => DiskFull,
        UpdateNameNotResolved or InternetTimeout or InternetNameNotResolved or InternetCannotConnect or WingetNoNetwork => NoConnection,
        ComponentStoreCorrupt => ComponentStoreDamaged,
        PendingOperations => RestartPending,
        SourceFilesNotFound or SourceDownloadFailed or SourceBlockedByPolicy => DismSourceUnavailable,
        _ => null,
    };

    /// <summary>Windows Update results, where generic file errors point at its own cache.</summary>
    public static string? FromWindowsUpdate(int code) => code switch
    {
        ServiceDisabled => WuServiceDisabled,
        FileNotFound or PathNotFound or UpdateDataStoreNoData or UpdateNotDownloaded => WuCacheDamaged,
        UpdateInstallNotAllowed => WuBusy,
        _ => FromHResult(code),
    };
}
