using Refreshify.Core.Health;

namespace Refreshify.Core.Tests.Health;

public class HealthServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Every_check_is_listed_in_order_and_battery_only_when_there_is_one()
    {
        var withBattery = new HealthService(new FakeReaders(), TimeProvider.System);
        var withoutBattery = new HealthService(new FakeReaders { BatteryPresent = false }, TimeProvider.System);

        string[] all =
        [
            "Free space", "Drives", "Battery", "Restart", "Time since restart", "Virus protection", "Windows Update", "Activation",
        ];
        string[] without = [.. all.Where(name => name != "Battery")];

        Assert.Equal(
            all,
            withBattery.Checks.Select(check => check.Name));
        Assert.Equal(
            without,
            withoutBattery.Checks.Select(check => check.Name));
    }

    [Fact]
    public void Attention_counts_only_attention_and_problem()
    {
        Assert.Equal(2, HealthService.AttentionCount(
        [
            new HealthResult(HealthStatus.Good, "good"),
            new HealthResult(HealthStatus.Unknown, "unknown"),
            new HealthResult(HealthStatus.Attention, "attention"),
            new HealthResult(HealthStatus.Problem, "problem"),
        ]));
    }

    [Fact]
    public async Task The_fake_pc_reports_the_expected_statuses()
    {
        var readers = new FakeReaders
        {
            Drives = [new DriveSpaceReading("Local Disk", "C:", 4L * 1024 * 1024 * 1024, 100L * 1024 * 1024 * 1024)],
            Disks = [new PhysicalDiskReading("Test Disk", 1)],
            Battery = new BatteryReading(100000, 50000),
            Restart = new RestartReading(false, true, false),
            Uptime = new UptimeReading(TimeSpan.FromDays(20)),
            Virus = new VirusProtectionReading(null, false, 0),
            Update = new WindowsUpdateReading(DateTimeOffset.Now - TimeSpan.FromDays(60)),
            Activation = new ActivationReading(0),
        };
        var service = new HealthService(readers, TimeProvider.System);

        var results = new List<HealthResult>();
        foreach (var check in service.Checks)
            results.Add(await service.RunAsync(check, HealthService.CheckTimeout, Ct));

        HealthStatus[] expected =
        [
            HealthStatus.Problem, HealthStatus.Attention, HealthStatus.Problem, HealthStatus.Attention,
            HealthStatus.Attention, HealthStatus.Problem, HealthStatus.Attention, HealthStatus.Attention,
        ];

        Assert.Equal(
            expected,
            results.Select(result => result.Status));
        Assert.Equal(8, HealthService.AttentionCount(results));
    }

    [Fact]
    public async Task A_slow_check_times_out_as_could_not_check()
    {
        var readers = new FakeReaders { Stall = TimeSpan.FromSeconds(30) };
        var service = new HealthService(readers, TimeProvider.System);
        var activation = service.Checks.Single(check => check.Id == "activation");

        var result = await service.RunAsync(activation, TimeSpan.FromMilliseconds(50), Ct);

        Assert.Equal(HealthStatus.Unknown, result.Status);
        Assert.Equal("Couldn't check this right now.", result.Sentence);
    }

    [Fact]
    public async Task A_check_that_throws_is_unknown()
    {
        var service = new HealthService(new ThrowingReaders(), TimeProvider.System);
        var check = service.Checks.Single(entry => entry.Id == "activation");

        Assert.Equal(HealthStatus.Unknown, (await service.RunAsync(check, HealthService.CheckTimeout, Ct)).Status);
    }

    [Fact]
    public async Task Latest_results_are_reused_until_the_checks_run_again()
    {
        var readers = new FakeReaders();
        var service = new HealthService(readers, TimeProvider.System);

        await service.LatestAsync();
        await service.LatestAsync();
        Assert.Equal(1, readers.ActivationReads);

        await Task.WhenAll(service.RunAll());
        Assert.Equal(2, readers.ActivationReads);
        Assert.Equal(service.Checks.Count, (await service.LatestAsync()).Length);
        Assert.Equal(2, readers.ActivationReads);
    }

    private sealed class FakeReaders : IHealthReaders
    {
        private const long Gb = 1024L * 1024 * 1024;

        public int ActivationReads { get; private set; }

        public bool BatteryPresent { get; init; } = true;

        public TimeSpan Stall { get; init; }

        public IReadOnlyList<DriveSpaceReading> Drives { get; init; } = [new("Local Disk", "C:", 50 * Gb, 100 * Gb)];

        public IReadOnlyList<PhysicalDiskReading> Disks { get; init; } = [new("Test Disk", 0)];

        public BatteryReading? Battery { get; init; } = new(100000, 90000);

        public RestartReading Restart { get; init; } = new(false, false, false);

        public UptimeReading Uptime { get; init; } = new(TimeSpan.FromDays(1));

        public VirusProtectionReading Virus { get; init; } = new(null, true, 0);

        public WindowsUpdateReading Update { get; init; } = new(DateTimeOffset.Now - TimeSpan.FromDays(5));

        public ActivationReading Activation { get; init; } = new(1);

        public bool HasBattery() => BatteryPresent;

        public Task<IReadOnlyList<DriveSpaceReading>> ReadDrivesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Drives);

        public Task<IReadOnlyList<PhysicalDiskReading>> ReadPhysicalDisksAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Disks);

        public Task<BatteryReading?> ReadBatteryAsync(CancellationToken cancellationToken) => Task.FromResult(Battery);

        public Task<RestartReading> ReadRestartAsync(CancellationToken cancellationToken) => Task.FromResult(Restart);

        public Task<UptimeReading> ReadUptimeAsync(CancellationToken cancellationToken) => Task.FromResult(Uptime);

        public Task<VirusProtectionReading> ReadVirusProtectionAsync(CancellationToken cancellationToken) => Task.FromResult(Virus);

        public Task<WindowsUpdateReading> ReadWindowsUpdateAsync(CancellationToken cancellationToken) => Task.FromResult(Update);

        public async Task<ActivationReading> ReadActivationAsync(CancellationToken cancellationToken)
        {
            ActivationReads++;
            if (Stall > TimeSpan.Zero)
                await Task.Delay(Stall, cancellationToken);
            return Activation;
        }
    }

    private sealed class ThrowingReaders : IHealthReaders
    {
        public bool HasBattery() => true;

        public Task<IReadOnlyList<DriveSpaceReading>> ReadDrivesAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no drives");

        public Task<IReadOnlyList<PhysicalDiskReading>> ReadPhysicalDisksAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no disks");

        public Task<BatteryReading?> ReadBatteryAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no battery");

        public Task<RestartReading> ReadRestartAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no restart");

        public Task<UptimeReading> ReadUptimeAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no uptime");

        public Task<VirusProtectionReading> ReadVirusProtectionAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no virus");

        public Task<WindowsUpdateReading> ReadWindowsUpdateAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no update");

        public Task<ActivationReading> ReadActivationAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no activation");
    }
}
