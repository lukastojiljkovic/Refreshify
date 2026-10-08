using Refreshify.Core.Tools;

namespace Refreshify.Core.Health;

/// <summary>What one check found: its status, one plain sentence, and where the user can go next.</summary>
public sealed record HealthResult(HealthStatus Status, string Sentence, HealthAction? Action = null)
{
    /// <summary>Shown when a check can't read what it needs, including after its time limit.</summary>
    public static HealthResult Unknown { get; } = new(HealthStatus.Unknown, "Couldn't check this right now.");
}

/// <summary>The page a result points at, and the tool to bring into view when there is one.</summary>
public sealed record HealthAction(ToolCategory Category, string? ToolId = null);
