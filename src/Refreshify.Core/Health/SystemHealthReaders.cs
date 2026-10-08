using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;
using Refreshify.Core.Platform;

namespace Refreshify.Core.Health;

/// <summary>
/// Reads each check from the running PC. WMI goes through the existing PowerShell helper (<c>Get-CimInstance</c>),
/// because the project doesn't reference System.Management; the rest uses the registry, powercfg and the Windows
/// Update Agent API on an STA thread. Nothing here needs elevation or changes anything.
/// </summary>
public sealed partial class SystemHealthReaders : IHealthReaders
{
    public static readonly SystemHealthReaders Default = new();

    /// <summary>The Windows application ID every edition shares in <c>SoftwareLicensingProduct</c>.</summary>
    private const string LicensingApplicationId = "55c92734-d682-4d71-983e-d6ec3f16059f";

    /// <summary>Windows Update's succeeded result code.</summary>
    private const int UpdateSucceeded = 2;

    public bool HasBattery() =>
        GetSystemPowerStatus(out var status) && status.BatteryFlag is not (128 or 255);

    public Task<IReadOnlyList<DriveSpaceReading>> ReadDrivesAsync(CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<DriveSpaceReading>>(() =>
            [.. DriveInfo.GetDrives()
                .Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
                .Select(ReadDrive)
                .OfType<DriveSpaceReading>()],
            cancellationToken);

    public async Task<IReadOnlyList<PhysicalDiskReading>> ReadPhysicalDisksAsync(CancellationToken cancellationToken)
    {
        var items = await CimAsync(
            "Get-CimInstance -ClassName MSFT_PhysicalDisk -Namespace 'root/Microsoft/Windows/Storage' | Select-Object FriendlyName, HealthStatus",
            cancellationToken);

        return [.. items.Select(item => new PhysicalDiskReading(Text(item, "FriendlyName") ?? "Disk", Number(item, "HealthStatus")))];
    }

    public async Task<BatteryReading?> ReadBatteryAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"refreshify-battery-{Guid.NewGuid():N}.xml");
        try
        {
            var spec = ProcessSpec.System32("powercfg.exe", $"/batteryreport /xml /output \"{path}\"");
            var result = await ProcessRunner.Default.RunAsync(spec, null, cancellationToken);
            return result.ExitCode == 0 && File.Exists(path)
                ? BatteryReport.Parse(await File.ReadAllTextAsync(path, cancellationToken))
                : null;
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A leftover file in the temp folder is harmless.
            }
        }
    }

    public Task<RestartReading> ReadRestartAsync(CancellationToken cancellationToken) => Task.Run(() => new RestartReading(
        KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"),
        KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"),
        HasPendingFileRename()), cancellationToken);

    public Task<UptimeReading> ReadUptimeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new UptimeReading(TimeSpan.FromMilliseconds(Environment.TickCount64)));

    public async Task<VirusProtectionReading> ReadVirusProtectionAsync(CancellationToken cancellationToken)
    {
        var products = await CimAsync(
            "Get-CimInstance -ClassName AntiVirusProduct -Namespace 'root/SecurityCenter2' | " +
            "Where-Object { $_.displayName -notlike '*Defender*' -and ($_.productState -band 0x1000) -ne 0 } | " +
            "Select-Object displayName",
            cancellationToken);
        if (products.Length > 0 && Text(products[0], "displayName") is { Length: > 0 } product)
            return new VirusProtectionReading(product, null, null);

        var defender = await CimAsync(
            "Get-CimInstance -ClassName MSFT_MpComputerStatus -Namespace 'root/Microsoft/Windows/Defender' | " +
            "Select-Object RealTimeProtectionEnabled, AntivirusSignatureAge",
            cancellationToken);
        return defender.Length == 0
            ? new VirusProtectionReading(null, null, null)
            : new VirusProtectionReading(null, Flag(defender[0], "RealTimeProtectionEnabled"), Number(defender[0], "AntivirusSignatureAge"));
    }

    public async Task<WindowsUpdateReading> ReadWindowsUpdateAsync(CancellationToken cancellationToken) =>
        new(await StaThread.Run(LastSuccessfulUpdate));

    public async Task<ActivationReading> ReadActivationAsync(CancellationToken cancellationToken)
    {
        var items = await CimAsync(
            // Filtered in WQL: enumerating every licensing product first takes over 30 seconds.
            "Get-CimInstance -ClassName SoftwareLicensingProduct " +
            $"-Filter \"ApplicationID='{LicensingApplicationId}' AND PartialProductKey IS NOT NULL\" | " +
            "Select-Object LicenseStatus",
            cancellationToken);
        return new ActivationReading(items.Length == 0 ? null : Number(items[0], "LicenseStatus"));
    }

    /// <summary>One fixed drive, or null when it turned out to be unreadable.</summary>
    private static DriveSpaceReading? ReadDrive(DriveInfo drive)
    {
        try
        {
            var label = drive.VolumeLabel;
            var name = string.IsNullOrWhiteSpace(label) ? "Local Disk" : label;
            return new DriveSpaceReading(name, drive.Name.TrimEnd('\\'), drive.AvailableFreeSpace, drive.TotalSize);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool KeyExists(string path)
    {
        using var key = Registry.LocalMachine.OpenSubKey(path);
        return key is not null;
    }

    private static bool HasPendingFileRename()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
        return key?.GetValue("PendingFileRenameOperations") is string[] { Length: > 0 } or string { Length: > 0 };
    }

    /// <summary>The date of the newest successfully installed update, or null when the API reports none.</summary>
    private static DateTimeOffset? LastSuccessfulUpdate()
    {
        var type = Type.GetTypeFromProgID("Microsoft.Update.Session");
        if (type is null || Activator.CreateInstance(type) is not { } session)
            return null;

        var searcher = Invoke(session, "CreateUpdateSearcher") ?? throw new InvalidOperationException("Windows Update search couldn't start.");
        var total = Convert.ToInt32(Invoke(searcher, "GetTotalHistoryCount"), CultureInfo.InvariantCulture);
        if (total <= 0)
            return null;

        var history = Invoke(searcher, "QueryHistory", 0, total) ?? throw new InvalidOperationException("Windows Update history couldn't be read.");
        var count = Convert.ToInt32(Get(history, "Count"), CultureInfo.InvariantCulture);
        DateTimeOffset? latest = null;
        for (var index = 0; index < count; index++)
        {
            if (Get(history, "Item", index) is not { } entry)
                continue;
            if (Convert.ToInt32(Get(entry, "ResultCode"), CultureInfo.InvariantCulture) != UpdateSucceeded)
                continue;
            if (Get(entry, "Date") is not DateTime installed)
                continue;

            var value = new DateTimeOffset(installed);
            if (latest is null || value > latest)
                latest = value;
        }

        return latest;
    }

    /// <summary>Runs a CIM query through PowerShell and returns its rows as JSON.</summary>
    private static async Task<JsonElement[]> CimAsync(string pipeline, CancellationToken cancellationToken)
    {
        var spec = PowerShell.Inline($"$items = @({pipeline})\nEmit @{{ items = $items }}", "powershell.exe Get-CimInstance");
        var result = await ProcessRunner.Default.RunAsync(spec, null, cancellationToken);
        var message = PowerShell.Find(PowerShell.Messages(result.Output), "items");
        return message is { } found && found.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
            ? [.. items.EnumerateArray()]
            : [];
    }

    private static string? Text(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? Number(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt32(),
            JsonValueKind.String when int.TryParse(value.GetString(), CultureInfo.InvariantCulture, out var number) => number,
            _ => null,
        };
    }

    private static bool? Flag(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    /// <summary>Calls a COM method by name, which is how the Windows Update Agent API is reached without a type library.</summary>
    private static object? Invoke(object target, string name, params object[] arguments) =>
        target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, arguments);

    private static object? Get(object target, string name, params object[] arguments) =>
        target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, arguments);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }
}
