using Refreshify.Core.Diagnostics;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Health;

/// <summary>
/// Turns a reading into a status, a sentence and, when Refreshify has a tool for it, where to open it. Kept pure, so
/// every threshold and sentence is tested without WMI, the registry or powercfg.
/// </summary>
public static class HealthEvaluation
{
    private static readonly HealthAction Cleanup = new(ToolCategory.Cleanup);

    private static readonly HealthAction Defender = new(ToolCategory.UpdatesAndSecurity, "defender-definitions");

    private static readonly HealthAction Updates = new(ToolCategory.UpdatesAndSecurity, "windows-update");

    public static HealthResult Evaluate(DriveSpaceReading drive)
    {
        var percent = drive.TotalBytes > 0 ? 100.0 * drive.FreeBytes / drive.TotalBytes : 0;
        var status = drive.FreeBytes < HealthThresholds.FreeSpaceProblemBytes || percent < HealthThresholds.FreeSpaceProblemPercent
            ? HealthStatus.Problem
            : percent < HealthThresholds.FreeSpaceAttentionPercent ? HealthStatus.Attention : HealthStatus.Good;
        var sentence = $"{drive.Name} ({drive.Letter}) has {Format.Bytes(drive.FreeBytes)} free of {Format.Bytes(drive.TotalBytes)}.";
        return new HealthResult(status, sentence, Cleanup);
    }

    /// <summary>One line per drive, on one card.</summary>
    public static HealthResult Evaluate(IReadOnlyList<DriveSpaceReading> drives) =>
        drives.Count == 0 ? HealthResult.Unknown : Combine([.. drives.Select(drive => Evaluate(drive))]);

    public static HealthResult Evaluate(PhysicalDiskReading disk) => disk.Health switch
    {
        0 => new HealthResult(HealthStatus.Good, $"{disk.Model} reports no problems."),
        1 => new HealthResult(HealthStatus.Attention, $"{disk.Model} reports a warning. Back up your files soon."),
        2 => new HealthResult(HealthStatus.Problem, $"{disk.Model} reports it is failing. Back up your files now."),
        _ => HealthResult.Unknown,
    };

    /// <summary>One line per disk, on one card.</summary>
    public static HealthResult Evaluate(IReadOnlyList<PhysicalDiskReading> disks) =>
        disks.Count == 0 ? HealthResult.Unknown : Combine([.. disks.Select(disk => Evaluate(disk))]);

    public static HealthResult Evaluate(BatteryReading battery)
    {
        if (battery.DesignCapacity <= 0 || battery.FullChargeCapacity <= 0)
            return HealthResult.Unknown;

        // Truncated, so 79% is never reported as 80% and then called good.
        var percent = (int)Math.Floor(100.0 * battery.FullChargeCapacity / battery.DesignCapacity);
        var status = percent >= HealthThresholds.BatteryGoodPercent ? HealthStatus.Good
            : percent >= HealthThresholds.BatteryAttentionPercent ? HealthStatus.Attention
            : HealthStatus.Problem;
        return new HealthResult(status, $"Your battery holds {percent}% of the charge it held when new.");
    }

    public static HealthResult Evaluate(RestartReading restart) =>
        restart.ComponentServicing || restart.WindowsUpdate || restart.PendingFileRename
            ? new HealthResult(HealthStatus.Attention, "Windows is waiting for a restart to finish installing updates.")
            : new HealthResult(HealthStatus.Good, "No restart is waiting.");

    public static HealthResult Evaluate(UptimeReading uptime)
    {
        var days = (int)uptime.Uptime.TotalDays;
        if (days > HealthThresholds.UptimeAttentionDays)
        {
            return new HealthResult(HealthStatus.Attention,
                $"Your PC has been running for {Format.Count(days, "day")} without a restart. Restarting now and then keeps Windows running smoothly.");
        }

        return new HealthResult(HealthStatus.Good, days < 1
            ? "Your PC has been running for less than a day."
            : $"Your PC has been running for {Format.Count(days, "day")}.");
    }

    public static HealthResult Evaluate(VirusProtectionReading virus)
    {
        if (virus.ActiveProduct is { Length: > 0 } product)
            return new HealthResult(HealthStatus.Good, $"{product} protects this PC.");
        if (virus.RealTimeProtectionOn is null && virus.SignatureAgeDays is null)
            return HealthResult.Unknown;
        if (virus.RealTimeProtectionOn == false)
            return new HealthResult(HealthStatus.Problem, "Real-time protection is off.", Defender);
        if (virus.SignatureAgeDays is { } age && age > HealthThresholds.VirusDefinitionsAttentionDays)
            return new HealthResult(HealthStatus.Attention, $"Virus definitions are {Format.Count(age, "day")} old.", Defender);
        return new HealthResult(HealthStatus.Good, "Microsoft Defender is on and up to date.", Defender);
    }

    public static HealthResult Evaluate(WindowsUpdateReading update, DateTimeOffset now)
    {
        if (update.LastInstalled is not { } last)
            return HealthResult.Unknown;

        var days = (int)Math.Max(0, (now - last).TotalDays);
        if (days > HealthThresholds.WindowsUpdateAttentionDays)
        {
            return new HealthResult(HealthStatus.Attention,
                $"The last Windows update was installed {Format.Count(days, "day")} ago.", Updates);
        }

        return new HealthResult(HealthStatus.Good, days < 1
            ? "Updates were installed today."
            : $"Updates were installed {Format.Count(days, "day")} ago.", Updates);
    }

    public static HealthResult Evaluate(ActivationReading activation) => activation.LicenseStatus switch
    {
        1 => new HealthResult(HealthStatus.Good, "Windows is activated."),
        null => HealthResult.Unknown,
        _ => new HealthResult(HealthStatus.Attention, "Windows is not activated."),
    };

    /// <summary>The card takes the worst status, every line, and the first action any line offers.</summary>
    private static HealthResult Combine(IReadOnlyList<HealthResult> results) => new(
        results.Max(result => result.Status),
        string.Join('\n', results.Select(result => result.Sentence)),
        results.Select(result => result.Action).FirstOrDefault(action => action is not null));
}
