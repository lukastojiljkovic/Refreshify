namespace Refreshify.Core.Health;

/// <summary>A fixed drive's free space, as the Free space check reads it.</summary>
public sealed record DriveSpaceReading(string Name, string Letter, long FreeBytes, long TotalBytes);

/// <summary>A physical disk's <c>MSFT_PhysicalDisk.HealthStatus</c>, or null when it couldn't be read.</summary>
public sealed record PhysicalDiskReading(string Model, int? Health);

/// <summary>A battery's design and full-charge capacity, in milliwatt-hours.</summary>
public sealed record BatteryReading(long DesignCapacity, long FullChargeCapacity);

/// <summary>The three places Windows records that a restart is waiting.</summary>
public sealed record RestartReading(bool ComponentServicing, bool WindowsUpdate, bool PendingFileRename);

/// <summary>How long the PC has been running since its last restart.</summary>
public sealed record UptimeReading(TimeSpan Uptime);

/// <summary>
/// The active antivirus when it isn't Microsoft Defender, or else Defender's real-time state and how old its
/// definitions are. A null means the value couldn't be read.
/// </summary>
public sealed record VirusProtectionReading(string? ActiveProduct, bool? RealTimeProtectionOn, int? SignatureAgeDays);

/// <summary>When the last Windows update was installed, or null when there is no record of one.</summary>
public sealed record WindowsUpdateReading(DateTimeOffset? LastInstalled);

/// <summary>The Windows license's <c>SoftwareLicensingProduct.LicenseStatus</c>, or null when it couldn't be read.</summary>
public sealed record ActivationReading(int? LicenseStatus);
