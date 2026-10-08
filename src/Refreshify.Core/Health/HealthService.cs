namespace Refreshify.Core.Health;

/// <summary>
/// The checks Home and the Health page run. Each reads and evaluates on its own, so a slow one never holds up the
/// rest, and each has its own time limit.
/// </summary>
public sealed class HealthService(IHealthReaders readers, TimeProvider clock)
{
    /// <summary>After this a check shows <see cref="HealthResult.Unknown"/> instead of waiting any longer.</summary>
    public static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(20);

    public HealthService(IHealthReaders readers)
        : this(readers, TimeProvider.System)
    {
    }

    private IReadOnlyList<Task<HealthResult>>? _latest;

    public IReadOnlyList<HealthCheck> Checks { get; } = Create(readers, clock);

    /// <summary>Starts every check, one task per entry of <see cref="Checks"/>, and keeps them as the latest results.</summary>
    public IReadOnlyList<Task<HealthResult>> RunAll() =>
        _latest = [.. Checks.Select(check => RunAsync(check, CheckTimeout, CancellationToken.None))];

    /// <summary>The latest results; the checks only run when they have not run yet, so Home never repeats them.</summary>
    public Task<HealthResult[]> LatestAsync() => Task.WhenAll(_latest ?? RunAll());

    /// <summary>The checks Home counts: only the ones that need the user's attention.</summary>
    public static int AttentionCount(IEnumerable<HealthResult> results) =>
        results.Count(result => result.Status is HealthStatus.Attention or HealthStatus.Problem);

    /// <summary>Runs one check, turning its failure or its time limit into an unknown result.</summary>
    public async Task<HealthResult> RunAsync(HealthCheck check, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            return await check.RunAsync(limit.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthResult.Unknown;
        }
        catch (Exception)
        {
            return HealthResult.Unknown;
        }
    }

    private static IReadOnlyList<HealthCheck> Create(IHealthReaders readers, TimeProvider clock)
    {
        var checks = new List<HealthCheck>
        {
            new("free-space", "Free space",
                async token => HealthEvaluation.Evaluate(await readers.ReadDrivesAsync(token))),
            new("drives", "Drives",
                async token => HealthEvaluation.Evaluate(await readers.ReadPhysicalDisksAsync(token))),
        };
        if (readers.HasBattery())
        {
            checks.Add(new HealthCheck("battery", "Battery", async token =>
                await readers.ReadBatteryAsync(token) is { } battery ? HealthEvaluation.Evaluate(battery) : HealthResult.Unknown));
        }

        checks.AddRange(
        [
            new HealthCheck("restart", "Restart",
                async token => HealthEvaluation.Evaluate(await readers.ReadRestartAsync(token))),
            new HealthCheck("uptime", "Time since restart",
                async token => HealthEvaluation.Evaluate(await readers.ReadUptimeAsync(token))),
            new HealthCheck("virus-protection", "Virus protection",
                async token => HealthEvaluation.Evaluate(await readers.ReadVirusProtectionAsync(token))),
            new HealthCheck("windows-update", "Windows Update",
                async token => HealthEvaluation.Evaluate(await readers.ReadWindowsUpdateAsync(token), clock.GetLocalNow())),
            new HealthCheck("activation", "Activation",
                async token => HealthEvaluation.Evaluate(await readers.ReadActivationAsync(token))),
        ]);
        return checks;
    }
}
