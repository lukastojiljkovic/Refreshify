namespace Refreshify.Core.Tools;

public enum ToolCategory
{
    Safety,
    Cleanup,
    Repair,
    Network,
    UpdatesAndSecurity,
    Troubleshooting,
}

/// <summary>Where a tool runs: in the unelevated app, or in the elevated worker.</summary>
public enum RunAs
{
    User,
    Administrator,
}

[Flags]
public enum ToolTraits
{
    None = 0,
    LongRunning = 1,
    RestartRequired = 2,
    RestartsExplorer = 4,
    MayCloseApps = 8,

    /// <summary>Killing the tool midway could leave Windows in a worse state, so a run only stops after it finishes.</summary>
    NotInterruptible = 16,
    NeedsInternet = 32,
}

/// <summary>Everything the UI shows about a tool. <see cref="Description"/> and <see cref="UseWhen"/> are plain language.</summary>
public sealed record ToolInfo(
    string Id,
    string Name,
    ToolCategory Category,
    string Glyph,
    string Description,
    string Technical,
    RunAs RunAs,
    bool IncludedByDefault = false,
    ToolTraits Traits = ToolTraits.None,
    string? UseWhen = null,
    bool Hidden = false)
{
    public bool Has(ToolTraits trait) => (Traits & trait) == trait;

    public override string ToString() => Name;
}

public sealed record ToolOptions(int TempFileAgeHours = 24, string? WindowsImagePath = null)
{
    public const int MaxTempFileAgeHours = 720;

    /// <summary>Options cross the elevation boundary, so the worker rejects anything out of range.</summary>
    public bool IsValid =>
        TempFileAgeHours is >= 0 and <= MaxTempFileAgeHours &&
        (WindowsImagePath is null ||
         (Path.IsPathFullyQualified(WindowsImagePath) &&
          WindowsImagePath.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) &&
          File.Exists(WindowsImagePath)));
}

public enum ToolEventKind
{
    Status,
    Output,
}

/// <summary>A status line with an optional percentage, or a raw output line for the technical details view.</summary>
public sealed record ToolEvent(ToolEventKind Kind, string Text, double? Percent = null);

public enum ToolOutcome
{
    Succeeded,
    Warning,
    Failed,
    Skipped,
    Cancelled,
}

/// <summary>What ran and what it printed, kept for the history and the LLM report.</summary>
public sealed record ToolTrace(IReadOnlyList<string> Commands, IReadOnlyList<string> Output, string? LogExcerpt);

public sealed record ToolResult(ToolOutcome Outcome, string Summary)
{
    /// <summary>Measured, never estimated.</summary>
    public long BytesFreed { get; init; }

    public bool RestartRequired { get; init; }

    /// <summary>A <see cref="Diagnostics.KnownIssues"/> id when the tool recognized the failure.</summary>
    public string? IssueId { get; init; }

    public int? ErrorCode { get; init; }

    /// <summary>Per-item results, such as the titles of installed updates.</summary>
    public IReadOnlyList<string> Details { get; init; } = [];

    public ToolTrace? Trace { get; init; }

    public static ToolResult Succeeded(string summary) => new(ToolOutcome.Succeeded, summary);

    public static ToolResult Warning(string summary) => new(ToolOutcome.Warning, summary);

    public static ToolResult Failed(string summary, int? errorCode = null, string? issueId = null) =>
        new(ToolOutcome.Failed, summary) { ErrorCode = errorCode, IssueId = issueId };

    public static ToolResult Skipped(string summary) => new(ToolOutcome.Skipped, summary);
}
