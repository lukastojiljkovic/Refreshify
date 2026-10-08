using Refreshify.Core.Health;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Tests.Health;

public class HealthEvaluationTests
{
    private const long Gb = 1024L * 1024 * 1024;

    [Theory]
    [InlineData(4, 100, HealthStatus.Problem)]
    [InlineData(5, 100, HealthStatus.Attention)]
    [InlineData(14, 100, HealthStatus.Attention)]
    [InlineData(15, 100, HealthStatus.Good)]
    [InlineData(80, 100, HealthStatus.Good)]
    public void Free_space_follows_the_percentage_lines(int freeGb, int totalGb, HealthStatus expected)
    {
        var drive = new DriveSpaceReading("Local Disk", "C:", freeGb * Gb, totalGb * Gb);

        Assert.Equal(expected, HealthEvaluation.Evaluate(drive).Status);
    }

    [Fact]
    public void Free_space_under_five_gigabytes_is_a_problem_even_when_the_percentage_looks_fine()
    {
        // 8% free, but only 4 GB, so the absolute floor decides.
        var drive = new DriveSpaceReading("Local Disk", "C:", 4 * Gb, 50 * Gb);

        Assert.Equal(HealthStatus.Problem, HealthEvaluation.Evaluate(drive).Status);
    }

    [Fact]
    public void Free_space_sentence_names_the_drive_and_both_sizes_and_points_at_cleanup()
    {
        var drive = new DriveSpaceReading("Local Disk", "C:", 20 * Gb, 100 * Gb);

        var result = HealthEvaluation.Evaluate(drive);

        Assert.Equal("Local Disk (C:) has 20.0 GB free of 100.0 GB.", result.Sentence);
        Assert.Equal(new HealthAction(ToolCategory.Cleanup), result.Action);
    }

    [Fact]
    public void The_free_space_card_lists_every_drive_and_takes_the_worst_status()
    {
        var result = HealthEvaluation.Evaluate(
        [
            new DriveSpaceReading("Local Disk", "C:", 20 * Gb, 100 * Gb),
            new DriveSpaceReading("Data", "D:", 4 * Gb, 100 * Gb),
        ]);

        Assert.Equal(HealthStatus.Problem, result.Status);
        Assert.Equal(
            "Local Disk (C:) has 20.0 GB free of 100.0 GB.\nData (D:) has 4.0 GB free of 100.0 GB.",
            result.Sentence);
    }

    [Fact]
    public void Free_space_without_any_drive_is_unknown()
    {
        var result = HealthEvaluation.Evaluate(Array.Empty<DriveSpaceReading>());

        Assert.Equal(HealthStatus.Unknown, result.Status);
        Assert.Null(result.Action);
    }

    [Theory]
    [InlineData(0, HealthStatus.Good, "Test Disk reports no problems.")]
    [InlineData(1, HealthStatus.Attention, "Test Disk reports a warning. Back up your files soon.")]
    [InlineData(2, HealthStatus.Problem, "Test Disk reports it is failing. Back up your files now.")]
    [InlineData(3, HealthStatus.Unknown, "Couldn't check this right now.")]
    [InlineData(null, HealthStatus.Unknown, "Couldn't check this right now.")]
    public void Physical_disk_health_maps_to_a_status_and_a_sentence(int? health, HealthStatus expected, string sentence)
    {
        var result = HealthEvaluation.Evaluate(new PhysicalDiskReading("Test Disk", health));

        Assert.Equal(expected, result.Status);
        Assert.Equal(sentence, result.Sentence);
        Assert.Null(result.Action);
    }

    [Theory]
    [InlineData(100000, 80000, HealthStatus.Good, "Your battery holds 80% of the charge it held when new.")]
    [InlineData(100000, 79999, HealthStatus.Attention, "Your battery holds 79% of the charge it held when new.")]
    [InlineData(100000, 60000, HealthStatus.Attention, "Your battery holds 60% of the charge it held when new.")]
    [InlineData(100000, 59999, HealthStatus.Problem, "Your battery holds 59% of the charge it held when new.")]
    public void Battery_capacity_follows_the_percentage_lines(long design, long full, HealthStatus expected, string sentence)
    {
        var result = HealthEvaluation.Evaluate(new BatteryReading(design, full));

        Assert.Equal(expected, result.Status);
        Assert.Equal(sentence, result.Sentence);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(57000, 0)]
    public void A_battery_report_without_capacities_is_unknown(long design, long full)
    {
        Assert.Equal(HealthStatus.Unknown, HealthEvaluation.Evaluate(new BatteryReading(design, full)).Status);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Any_source_waiting_for_a_restart_is_attention(bool componentServicing, bool windowsUpdate, bool fileRename)
    {
        var result = HealthEvaluation.Evaluate(new RestartReading(componentServicing, windowsUpdate, fileRename));

        Assert.Equal(HealthStatus.Attention, result.Status);
        Assert.Equal("Windows is waiting for a restart to finish installing updates.", result.Sentence);
    }

    [Fact]
    public void No_source_waiting_for_a_restart_is_good()
    {
        var result = HealthEvaluation.Evaluate(new RestartReading(false, false, false));

        Assert.Equal(HealthStatus.Good, result.Status);
        Assert.Equal("No restart is waiting.", result.Sentence);
    }

    [Theory]
    [InlineData(0.5, HealthStatus.Good, "Your PC has been running for less than a day.")]
    [InlineData(1, HealthStatus.Good, "Your PC has been running for 1 day.")]
    [InlineData(14, HealthStatus.Good, "Your PC has been running for 14 days.")]
    [InlineData(15, HealthStatus.Attention,
        "Your PC has been running for 15 days without a restart. Restarting now and then keeps Windows running smoothly.")]
    public void Uptime_counts_whole_days(double days, HealthStatus expected, string sentence)
    {
        var result = HealthEvaluation.Evaluate(new UptimeReading(TimeSpan.FromDays(days)));

        Assert.Equal(expected, result.Status);
        Assert.Equal(sentence, result.Sentence);
    }

    [Fact]
    public void Another_antivirus_product_protecting_the_pc_is_good_and_offers_no_tool()
    {
        var result = HealthEvaluation.Evaluate(new VirusProtectionReading("Acme Antivirus", null, null));

        Assert.Equal(HealthStatus.Good, result.Status);
        Assert.Equal("Acme Antivirus protects this PC.", result.Sentence);
        Assert.Null(result.Action);
    }

    [Fact]
    public void Defender_real_time_protection_being_off_is_a_problem()
    {
        var result = HealthEvaluation.Evaluate(new VirusProtectionReading(null, false, 90));

        Assert.Equal(HealthStatus.Problem, result.Status);
        Assert.Equal("Real-time protection is off.", result.Sentence);
        Assert.Equal(new HealthAction(ToolCategory.UpdatesAndSecurity, "defender-definitions"), result.Action);
    }

    [Theory]
    [InlineData(4, "Virus definitions are 4 days old.")]
    [InlineData(9, "Virus definitions are 9 days old.")]
    public void Old_defender_definitions_are_attention(int age, string sentence)
    {
        var result = HealthEvaluation.Evaluate(new VirusProtectionReading(null, true, age));

        Assert.Equal(HealthStatus.Attention, result.Status);
        Assert.Equal(sentence, result.Sentence);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Fresh_defender_definitions_are_good(int age)
    {
        var result = HealthEvaluation.Evaluate(new VirusProtectionReading(null, true, age));

        Assert.Equal(HealthStatus.Good, result.Status);
        Assert.Equal("Microsoft Defender is on and up to date.", result.Sentence);
    }

    [Fact]
    public void Virus_protection_that_could_not_be_read_is_unknown()
    {
        Assert.Equal(HealthStatus.Unknown, HealthEvaluation.Evaluate(new VirusProtectionReading(null, null, null)).Status);
    }

    [Theory]
    [InlineData(0, HealthStatus.Good, "Updates were installed today.")]
    [InlineData(1, HealthStatus.Good, "Updates were installed 1 day ago.")]
    [InlineData(45, HealthStatus.Good, "Updates were installed 45 days ago.")]
    [InlineData(46, HealthStatus.Attention, "The last Windows update was installed 46 days ago.")]
    public void Windows_update_recency_follows_the_day_line(int daysAgo, HealthStatus expected, string sentence)
    {
        var now = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var result = HealthEvaluation.Evaluate(new WindowsUpdateReading(now - TimeSpan.FromDays(daysAgo)), now);

        Assert.Equal(expected, result.Status);
        Assert.Equal(sentence, result.Sentence);
        Assert.Equal(new HealthAction(ToolCategory.UpdatesAndSecurity, "windows-update"), result.Action);
    }

    [Fact]
    public void Windows_update_without_a_record_is_unknown()
    {
        Assert.Equal(HealthStatus.Unknown, HealthEvaluation.Evaluate(new WindowsUpdateReading(null), DateTimeOffset.UtcNow).Status);
    }

    [Theory]
    [InlineData(1, HealthStatus.Good, "Windows is activated.")]
    [InlineData(0, HealthStatus.Attention, "Windows is not activated.")]
    [InlineData(5, HealthStatus.Attention, "Windows is not activated.")]
    [InlineData(null, HealthStatus.Unknown, "Couldn't check this right now.")]
    public void Activation_reports_the_license_status(int? status, HealthStatus expected, string sentence)
    {
        var result = HealthEvaluation.Evaluate(new ActivationReading(status));

        Assert.Equal(expected, result.Status);
        Assert.Equal(sentence, result.Sentence);
        Assert.Null(result.Action);
    }
}
