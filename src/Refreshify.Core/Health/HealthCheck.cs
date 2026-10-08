namespace Refreshify.Core.Health;

/// <summary>One thing the Health page checks: its name and how its result is read.</summary>
public sealed class HealthCheck(string id, string name, Func<CancellationToken, Task<HealthResult>> run)
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    public Task<HealthResult> RunAsync(CancellationToken cancellationToken) => run(cancellationToken);

    public override string ToString() => Name;
}
