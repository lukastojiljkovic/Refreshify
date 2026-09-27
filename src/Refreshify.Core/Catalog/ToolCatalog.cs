using System.Text.Json;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Platform;
using Refreshify.Core.Tools;
using static Refreshify.Core.Tools.ToolCategory;
using static Refreshify.Core.Tools.ToolTraits;

namespace Refreshify.Core.Catalog;

/// <summary>Every tool, in run order: restore point, Cleanup, Repair, Network, Updates &amp; security, Troubleshooting.</summary>
public static class ToolCatalog
{
    public static IReadOnlyList<Tool> All { get; } = Create();

    private static readonly Dictionary<string, Tool> ById = All.ToDictionary(tool => tool.Info.Id, StringComparer.Ordinal);

    public static Tool? Find(string id) => ById.GetValueOrDefault(id);

    private static string Windows => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    private static string LocalLow => Path.GetFullPath(Path.Combine(LocalAppData, @"..\LocalLow"));

    private static List<Tool> Create() =>
    [
        // Safety
        new ScriptTool(
            new("restore-point", "Restore point", Safety, "\uE777",
                "Creates a restore point before anything changes, so you can undo the changes with System Restore.",
                "Checkpoint-Computer, skipped when a restore point from the last 24 hours exists", RunAs.Administrator, Hidden: true),
            "Creating a restore point",
            _ => PowerShell.Script("RestorePoint", new Dictionary<string, string> { ["Description"] = "Refreshify" }),
            messages => State(messages) switch
            {
                "created" => ToolResult.Succeeded("Created a restore point."),
                "recent" => ToolResult.Skipped("A restore point from the last 24 hours already exists."),
                _ when !SystemState.IsSystemProtectionOn =>
                    ToolResult.Failed("System Protection is off, so no restore point was created.", issueId: KnownIssues.SystemProtectionOff),
                _ => ToolResult.Failed("Windows didn't create a restore point."),
            },
            code => code == ErrorCodes.ServiceDisabled || !SystemState.IsSystemProtectionOn
                ? KnownIssues.SystemProtectionOff
                : KnownIssues.FromHResult(code)),

        // Cleanup
        new CleanupTool(
            new("temp-files", "Your temporary files", Cleanup, "\uE7C3",
                "Deletes temporary files that apps left behind in your account. Files newer than the age set in Settings are kept, because apps may still need them.",
                @"%TEMP%, %LOCALAPPDATA%\Microsoft\Windows\INetCache, %LOCALAPPDATA%\CrashDumps", RunAs.User, IncludedByDefault: true),
            () =>
            [
                new(Path.GetTempPath(), UseAgeFilter: true),
                new(Path.Combine(LocalAppData, @"Microsoft\Windows\INetCache"), UseAgeFilter: true),
                new(Path.Combine(LocalAppData, "CrashDumps")),
            ]),
        new CleanupTool(
            new("windows-temp", "Windows temporary files", Cleanup, "\uE8B7",
                "Deletes temporary files that Windows and installers left behind. Files newer than the age set in Settings are kept.",
                @"%SystemRoot%\Temp", RunAs.Administrator, IncludedByDefault: true),
            () => [new(Path.Combine(Windows, "Temp"), UseAgeFilter: true)]),
        new CleanupTool(
            new("explorer-caches", "Thumbnail and icon caches", Cleanup, "\uE8B9",
                "Clears saved thumbnails and icons, which fixes blank or outdated previews and icons. File Explorer closes for a moment and reopens, and Windows rebuilds the caches as you browse.",
                @"Restart Manager closes File Explorer; deletes %LOCALAPPDATA%\Microsoft\Windows\Explorer\thumbcache_*.db and iconcache_*.db, and %LOCALAPPDATA%\IconCache.db",
                RunAs.User, IncludedByDefault: true, Traits: RestartsExplorer),
            () =>
            [
                new(Path.Combine(LocalAppData, @"Microsoft\Windows\Explorer"), "thumbcache_*.db", Recursive: false),
                new(Path.Combine(LocalAppData, @"Microsoft\Windows\Explorer"), "iconcache_*.db", Recursive: false),
                new(Path.Combine(LocalAppData, "IconCache.db")),
            ]),
        new CleanupTool(
            new("update-downloads", "Windows Update downloads", Cleanup, "\uE896",
                "Deletes the files Windows Update keeps after downloading updates. Windows downloads again anything it still needs. Skipped while updates are waiting for a restart.",
                @"Stops wuauserv and bits, empties %SystemRoot%\SoftwareDistribution\Download, starts the services again", RunAs.Administrator,
                IncludedByDefault: true),
            () => [new(Path.Combine(Windows, @"SoftwareDistribution\Download"))],
            stopServices: ["wuauserv", "bits"],
            precondition: () => SystemState.IsRestartPending
                ? ToolResult.Skipped("Updates are waiting for a restart, so their files were kept.")
                : null),
        new ScriptTool(
            new("delivery-optimization", "Delivery Optimization cache", Cleanup, "\uE753",
                "Deletes the update and app files Windows keeps to share with other PCs. It's only a cache; nothing you need is removed.",
                "Delete-DeliveryOptimizationCache -Force", RunAs.Administrator, IncludedByDefault: true),
            "Deleting files",
            _ => PowerShell.Script("DeliveryOptimization"),
            messages => PowerShell.Find(messages, "freed")?.GetProperty("freed").GetInt64() is > 0 and var freed
                ? ToolResult.Succeeded($"Freed {Format.Bytes(freed)}.") with { BytesFreed = freed }
                : ToolResult.Succeeded("The cache is empty now.")),
        new CleanupTool(
            new("crash-dumps", "Error reports and crash dumps", Cleanup, "\uE7BA",
                "Deletes saved error reports and memory dumps from past crashes. They can be large and are only useful for investigating a crash.",
                @"%ProgramData%\Microsoft\Windows\WER\ReportArchive, ReportQueue and Temp; %SystemRoot%\Minidump, MEMORY.DMP and LiveKernelReports",
                RunAs.Administrator, IncludedByDefault: true),
            () =>
            [
                new(Path.Combine(ProgramData, @"Microsoft\Windows\WER\ReportArchive")),
                new(Path.Combine(ProgramData, @"Microsoft\Windows\WER\ReportQueue")),
                new(Path.Combine(ProgramData, @"Microsoft\Windows\WER\Temp")),
                new(Path.Combine(Windows, "Minidump")),
                new(Path.Combine(Windows, "MEMORY.DMP")),
                new(Path.Combine(Windows, "LiveKernelReports")),
            ]),
        new RecycleBinTool(
            new("recycle-bin", "Empty Recycle Bin", Cleanup, "\uE74D",
                "Permanently deletes everything in the Recycle Bin on all drives.",
                "SHEmptyRecycleBin", RunAs.User)),

        // Repair
        new DismTool(
            new("dism-restorehealth", "System image repair", Repair, "\uE90F",
                "Checks the Windows component store, which Windows repairs itself from, and downloads clean copies of anything damaged. This can take a while.",
                "DISM /Online /Cleanup-Image /RestoreHealth /English", RunAs.Administrator, IncludedByDefault: true,
                Traits: LongRunning | NotInterruptible | NeedsInternet),
            "/RestoreHealth", "Repairing the Windows image", "The Windows image is healthy.", acceptsWindowsImage: true),
        new SfcTool(
            new("sfc", "System file check", Repair, "\uE9D9",
                "Checks every protected Windows file and replaces damaged ones with clean copies. Runs after System image repair, which it repairs from.",
                "sfc /scannow", RunAs.Administrator, IncludedByDefault: true, Traits: LongRunning | NotInterruptible)),
        new DismTool(
            new("component-cleanup", "Component store cleanup", Repair, "\uE8F1",
                "Removes old versions of Windows components that newer updates replaced. Installed updates can still be uninstalled.",
                "DISM /Online /Cleanup-Image /StartComponentCleanup /English", RunAs.Administrator, IncludedByDefault: true,
                Traits: LongRunning | NotInterruptible),
            "/StartComponentCleanup", "Cleaning up the component store", "The component store is cleaned up.", reportsFreedSpace: true),
        new DiskCheckTool(
            new("disk-check", "Disk check", Repair, "\uEDA2",
                "Scans the system drive for file system errors while Windows keeps running, and fixes what it can.",
                "chkdsk C: /scan", RunAs.Administrator, IncludedByDefault: true, Traits: LongRunning)),

        // Network
        new CommandTool(
            new("network-caches", "Network caches", Network, "\uE774",
                "Clears the saved addresses of websites and nearby devices, which fixes sites that don't load after a network change.",
                "ipconfig /flushdns; netsh interface ip delete arpcache", RunAs.Administrator, IncludedByDefault: true),
            [
                new(@"%SystemRoot%\System32\ipconfig.exe", "/flushdns", "Clearing the DNS cache"),
                new(@"%SystemRoot%\System32\netsh.exe", "interface ip delete arpcache", "Clearing the ARP cache", Required: false),
            ],
            "Network caches are cleared."),
        new TimeSyncTool(
            new("time-sync", "Time sync", Network, "\uE823",
                "Syncs your clock with the internet time server. A wrong clock breaks secure websites and updates.",
                "w32tm /resync", RunAs.Administrator, IncludedByDefault: true, Traits: NeedsInternet)),

        // Updates & security
        new ScriptTool(
            new("defender-definitions", "Defender definitions", UpdatesAndSecurity, "\uEA18",
                "Downloads the latest threat definitions for Microsoft Defender Antivirus. Skipped when another antivirus protects your PC.",
                "Update-MpSignature", RunAs.Administrator, IncludedByDefault: true, Traits: NeedsInternet),
            "Updating definitions",
            _ => PowerShell.Script("Defender", new Dictionary<string, string> { ["Action"] = "update" }),
            messages => ToolResult.Succeeded($"Definitions are up to date (version {PowerShell.Find(messages, "version")?.GetProperty("version").GetString()}).")),
        new WindowsUpdateTool(
            new("windows-update", "Windows Update", UpdatesAndSecurity, "\uE895",
                "Downloads and installs available Windows updates, one at a time. Optional updates and updates that need your input are left for Settings.",
                "Windows Update Agent API: IsInstalled=0 and IsHidden=0 and BrowseOnly=0", RunAs.Administrator, IncludedByDefault: true,
                Traits: LongRunning | NeedsInternet)),
        new AppUpdatesTool(
            new("app-updates", "App updates", UpdatesAndSecurity, "\uE71D",
                "Updates your installed apps with winget, the Windows package manager. Apps that are being updated may close.",
                "winget upgrade --all --silent --accept-source-agreements --accept-package-agreements --disable-interactivity",
                RunAs.Administrator, IncludedByDefault: true, Traits: LongRunning | MayCloseApps | NeedsInternet)),
        new ScriptTool(
            new("defender-scan", "Defender quick scan", UpdatesAndSecurity, "\uE730",
                "Scans the places where malware usually hides with Microsoft Defender Antivirus. Skipped when another antivirus protects your PC.",
                "Start-MpScan -ScanType QuickScan", RunAs.Administrator, IncludedByDefault: true, Traits: LongRunning),
            "Scanning",
            _ => PowerShell.Script("Defender", new Dictionary<string, string> { ["Action"] = "scan" }),
            ScanResult),

        // Troubleshooting
        new CommandTool(
            new("renew-ip", "Renew IP address", Troubleshooting, "\uE839",
                "Asks your router for a new network address. You're offline for a moment while it does.",
                "ipconfig /release; ipconfig /renew", RunAs.Administrator,
                UseWhen: "You're connected to Wi-Fi or Ethernet, but nothing loads."),
            [
                new(@"%SystemRoot%\System32\ipconfig.exe", "/release", "Releasing the address", Required: false),
                new(@"%SystemRoot%\System32\ipconfig.exe", "/renew", "Getting a new address"),
            ],
            "Your PC has a new network address."),
        new CommandTool(
            new("network-reset", "Network stack reset", Troubleshooting, "\uE701",
                "Resets Windows' network settings to their defaults. Custom settings such as a fixed IP address may need to be set again. Needs a restart.",
                "netsh winsock reset; netsh int ip reset", RunAs.Administrator, Traits: RestartRequired,
                UseWhen: "Nothing on the internet works, and renewing the IP address didn't help."),
            [
                new(@"%SystemRoot%\System32\netsh.exe", "winsock reset", "Resetting Winsock"),
                new(@"%SystemRoot%\System32\netsh.exe", "int ip reset", "Resetting TCP/IP", Required: false),
            ],
            "Network settings are reset. Restart your PC to finish."),
        new ScriptTool(
            new("reset-windows-update", "Reset Windows Update", Troubleshooting, "\uE72C",
                "Stops Windows Update, sets its cache aside and starts it with a new one. Your update history may look empty afterwards; installed updates aren't affected.",
                "Stops wuauserv, bits and cryptsvc; renames SoftwareDistribution and catroot2 to *.bak; starts the services again",
                RunAs.Administrator, UseWhen: "Windows Update keeps failing or is stuck on the same update."),
            "Resetting Windows Update",
            _ => PowerShell.Script("ResetWindowsUpdate"),
            _ => ToolResult.Succeeded("Windows Update is reset. It builds a new cache the next time it checks for updates.")),
        new CommandTool(
            new("store-cache", "Microsoft Store cache", Troubleshooting, "\uE719",
                "Clears the Microsoft Store cache. The Store opens when it's done.",
                "wsreset.exe", RunAs.User, UseWhen: "The Microsoft Store won't open, or its downloads are stuck."),
            [new(@"%SystemRoot%\System32\wsreset.exe", string.Empty, "Clearing the Store cache")],
            "The Microsoft Store cache is cleared."),
        new CleanupTool(
            new("font-cache", "Font cache", Troubleshooting, "\uE8D2",
                "Deletes the font cache, which Windows rebuilds when it restarts.",
                @"Stops FontCache; deletes %SystemRoot%\ServiceProfiles\LocalService\AppData\Local\FontCache\*FontCache* and %SystemRoot%\System32\FNTCACHE.DAT; starts FontCache again",
                RunAs.Administrator, Traits: RestartRequired, UseWhen: "Text shows up in the wrong font, or fonts look garbled or are missing."),
            () =>
            [
                new(Path.Combine(Windows, @"ServiceProfiles\LocalService\AppData\Local\FontCache"), "*FontCache*", Recursive: false),
                new(Path.Combine(Windows, @"System32\FNTCACHE.DAT")),
            ],
            stopServices: ["FontCache"],
            summary: "The font cache is cleared. Restart your PC to rebuild it."),
        new CleanupTool(
            new("print-queue", "Print queue", Troubleshooting, "\uE749",
                "Cancels every waiting print job and restarts the print service.",
                @"Stops Spooler, empties %SystemRoot%\System32\spool\PRINTERS, starts Spooler again", RunAs.Administrator,
                UseWhen: "A document is stuck in the print queue and nothing prints."),
            () => [new(Path.Combine(Windows, @"System32\spool\PRINTERS"))],
            stopServices: ["Spooler"],
            summary: "The print queue is empty."),
        new ScriptTool(
            new("search-index", "Search index", Troubleshooting, "\uE721",
                "Rebuilds the Windows Search index from scratch. Search results may be incomplete while it rebuilds.",
                @"Stops WSearch, sets HKLM\SOFTWARE\Microsoft\Windows Search\SetupCompletedSuccessfully to 0, starts WSearch again",
                RunAs.Administrator, UseWhen: "Windows search doesn't find files or apps that you know are there."),
            "Resetting the search index",
            _ => PowerShell.Script("SearchIndex"),
            _ => ToolResult.Succeeded("Windows Search is rebuilding its index in the background.")),
        new ServiceRestartTool(
            new("restart-audio", "Audio services", Troubleshooting, "\uE767",
                "Restarts the Windows audio services.",
                "Restarts AudioEndpointBuilder and Audiosrv", RunAs.Administrator, Traits: NotInterruptible,
                UseWhen: "There's no sound or it crackles, although your speakers and volume are fine."),
            ["AudioEndpointBuilder", "Audiosrv"],
            "The audio services are restarted."),
        new ShellRestartTool(
            new("restart-shell", "Start menu and taskbar", Troubleshooting, "\uE75B",
                "Restarts File Explorer, the Start menu and search. Open File Explorer windows close.",
                "Restart Manager restarts explorer.exe; ends StartMenuExperienceHost, ShellExperienceHost and SearchHost, which Windows starts again",
                RunAs.User, Traits: RestartsExplorer, UseWhen: "The Start menu, taskbar or search stops responding.")),
        new CleanupTool(
            new("shader-cache", "Graphics shader cache", Troubleshooting, "\uE7FC",
                "Deletes the shaders Windows and your graphics driver saved. Games rebuild them, so their first start may be slower.",
                @"%LOCALAPPDATA%\D3DSCache; NVIDIA DXCache and GLCache; AMD DxCache, DxcCache, GLCache and VkCache; %USERPROFILE%\AppData\LocalLow\Intel\ShaderCache",
                RunAs.User, UseWhen: "A game stutters, shows graphics glitches or crashes after a driver update."),
            () =>
            [
                new(Path.Combine(LocalAppData, "D3DSCache")),
                new(Path.Combine(LocalAppData, @"NVIDIA\DXCache")),
                new(Path.Combine(LocalAppData, @"NVIDIA\GLCache")),
                new(Path.Combine(LocalAppData, @"AMD\DxCache")),
                new(Path.Combine(LocalAppData, @"AMD\DxcCache")),
                new(Path.Combine(LocalAppData, @"AMD\GLCache")),
                new(Path.Combine(LocalAppData, @"AMD\VkCache")),
                new(Path.Combine(LocalLow, @"Intel\ShaderCache")),
            ]),

        // Fixes for known issues, not shown on their own
        new CommandTool(
            new("fix-enable-trustedinstaller", "Turn on the Windows Modules Installer service", Repair, string.Empty,
                "Sets the service System file check repairs with back to start when needed.",
                "sc config TrustedInstaller start= demand", RunAs.Administrator, Hidden: true),
            [new(@"%SystemRoot%\System32\sc.exe", "config TrustedInstaller start= demand", "Turning on the service")],
            "The Windows Modules Installer service is on."),
        new CommandTool(
            new("fix-enable-wuauserv", "Turn on the Windows Update service", UpdatesAndSecurity, string.Empty,
                "Sets the Windows Update service back to start when needed.",
                "sc config wuauserv start= demand", RunAs.Administrator, Hidden: true),
            [new(@"%SystemRoot%\System32\sc.exe", "config wuauserv start= demand", "Turning on the service")],
            "The Windows Update service is on."),
        new CommandTool(
            new("fix-winget-source-reset", "Reset winget's sources", UpdatesAndSecurity, string.Empty,
                "Resets winget's app sources to the defaults.",
                "winget source reset --force", RunAs.Administrator, Hidden: true),
            [new(@"%LOCALAPPDATA%\Microsoft\WindowsApps\winget.exe", "source reset --force", "Resetting the sources")],
            "winget's sources are reset."),
        new ScriptTool(
            new("fix-enable-system-protection", "Turn on System Protection", Safety, string.Empty,
                "Turns on System Protection for the system drive, so restore points can be created.",
                @"Enable-ComputerRestore -Drive C:\", RunAs.Administrator, Hidden: true),
            "Turning on System Protection",
            _ => PowerShell.Inline("Enable-ComputerRestore -Drive \"$env:SystemDrive\\\"", "powershell.exe Enable-ComputerRestore"),
            _ => ToolResult.Succeeded("System Protection is on for the system drive.")),
        new CommandTool(
            new("fix-schedule-disk-repair", "Schedule a disk repair", Repair, string.Empty,
                "Marks the system drive for a repair that runs the next time Windows starts.",
                "fsutil dirty set C:", RunAs.Administrator, Traits: RestartRequired, Hidden: true),
            [new(@"%SystemRoot%\System32\fsutil.exe", "dirty set %SystemDrive%", "Scheduling the repair")],
            "The repair runs the next time you restart your PC."),
        new ScriptTool(
            new("fix-register-app-installer", "Register App Installer", UpdatesAndSecurity, string.Empty,
                "Registers Microsoft's App Installer, which provides winget, for your account.",
                "Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe", RunAs.User, Hidden: true),
            "Registering App Installer",
            _ => PowerShell.Inline(
                "Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe",
                "powershell.exe Add-AppxPackage"),
            _ => ToolResult.Succeeded("App Installer is registered.")),
    ];

    private static string? State(IReadOnlyList<JsonElement> messages) =>
        PowerShell.Find(messages, "state")?.GetProperty("state").GetString();

    private static ToolResult ScanResult(IReadOnlyList<JsonElement> messages)
    {
        var scan = PowerShell.Find(messages, "threats");
        var threats = scan?.GetProperty("threats").GetInt32() ?? 0;
        if (threats == 0)
            return ToolResult.Succeeded("No threats were found.");

        var names = scan!.Value.GetProperty("names").EnumerateArray().Select(name => name.GetString() ?? string.Empty).ToList();
        return ToolResult.Warning($"Found {Format.Count(threats, "threat")}. Open Windows Security to review what Microsoft Defender did.") with
        {
            Details = names,
        };
    }
}
