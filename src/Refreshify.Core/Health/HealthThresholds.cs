namespace Refreshify.Core.Health;

/// <summary>Where each check changes its mind, kept together as named values.</summary>
public static class HealthThresholds
{
    public const double FreeSpaceProblemPercent = 5.0;

    public const long FreeSpaceProblemBytes = 5L * 1024 * 1024 * 1024;

    public const double FreeSpaceAttentionPercent = 15.0;

    public const int BatteryGoodPercent = 80;

    public const int BatteryAttentionPercent = 60;

    public const int UptimeAttentionDays = 14;

    public const int VirusDefinitionsAttentionDays = 3;

    public const int WindowsUpdateAttentionDays = 45;
}
