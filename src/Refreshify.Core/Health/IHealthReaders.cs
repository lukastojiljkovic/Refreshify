namespace Refreshify.Core.Health;

/// <summary>
/// The system reads the checks need, behind an interface so the evaluation and the check composition are tested
/// without WMI, the registry or powercfg.
/// </summary>
public interface IHealthReaders
{
    /// <summary>Whether this PC has a battery, so the Battery card only appears where it applies.</summary>
    bool HasBattery();

    Task<IReadOnlyList<DriveSpaceReading>> ReadDrivesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PhysicalDiskReading>> ReadPhysicalDisksAsync(CancellationToken cancellationToken);

    Task<BatteryReading?> ReadBatteryAsync(CancellationToken cancellationToken);

    Task<RestartReading> ReadRestartAsync(CancellationToken cancellationToken);

    Task<UptimeReading> ReadUptimeAsync(CancellationToken cancellationToken);

    Task<VirusProtectionReading> ReadVirusProtectionAsync(CancellationToken cancellationToken);

    Task<WindowsUpdateReading> ReadWindowsUpdateAsync(CancellationToken cancellationToken);

    Task<ActivationReading> ReadActivationAsync(CancellationToken cancellationToken);
}
