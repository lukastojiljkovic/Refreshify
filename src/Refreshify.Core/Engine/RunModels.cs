using System.Text.Json.Serialization;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Engine;

public enum RunKind
{
    /// <summary>Every tool selected for Run all; counts as a full refresh.</summary>
    All,
    Category,
    Tool,
}

/// <param name="ToolIds">Run in catalog order, whatever order they're given in; unknown ids are ignored.</param>
/// <param name="CreateRestorePoint">Adds the restore point before a run that includes administrator tools.</param>
/// <param name="FixAutomatically">Applies <see cref="RemedyKind.Automatic"/> remedies without asking.</param>
public sealed record RunRequest(
    RunKind Kind, IReadOnlyList<string> ToolIds, ToolOptions Options, bool CreateRestorePoint = true, bool FixAutomatically = true);

public enum StepStatus
{
    Pending,
    Running,
    Done,
}

public sealed record StepRecord(string ToolId, string Name)
{
    public StepStatus Status { get; init; }

    public ToolResult? Result { get; init; }

    /// <summary>What each fix did, for the step view and the LLM report.</summary>
    public IReadOnlyList<string> FixesTried { get; init; } = [];

    /// <summary>Issues whose remedy already ran for this step. A remedy runs at most once per step, so fixes can't loop.</summary>
    public IReadOnlyList<string> RemediedIssues { get; init; } = [];

    [JsonIgnore]
    public KnownIssue? Issue => KnownIssues.Find(Result?.IssueId);

    /// <summary>Whether the step offers <b>Fix it</b>: a recognized problem with a remedy that hasn't been tried yet.</summary>
    [JsonIgnore]
    public bool CanFix =>
        Status == StepStatus.Done &&
        Result?.Outcome is ToolOutcome.Failed or ToolOutcome.Warning &&
        Issue is { FixToolIds.Count: > 0 } issue &&
        !RemediedIssues.Contains(issue.Id);
}

/// <param name="Id">Sortable: runs saved by id are in chronological order.</param>
public sealed record RunRecord(string Id, RunKind Kind, DateTimeOffset Started, IReadOnlyList<StepRecord> Steps)
{
    public DateTimeOffset? Finished { get; init; }

    public bool Cancelled { get; init; }

    [JsonIgnore]
    public long BytesFreed => Steps.Sum(step => step.Result?.BytesFreed ?? 0);

    [JsonIgnore]
    public bool RestartRequired => Steps.Any(step => step.Result?.RestartRequired == true);
}

public enum RestorePointChoice
{
    Fix,
    Continue,
    Cancel,
}

/// <summary>Receives a run's progress. Calls arrive on background threads.</summary>
public interface IRunObserver
{
    void StepChanged(int index, StepRecord step);

    void ToolEvent(int index, ToolEvent toolEvent);

    /// <summary>The restore point is the safety net for everything after it, so its failure is decided right away.</summary>
    Task<RestorePointChoice> RestorePointFailedAsync(StepRecord step, bool canFix);
}
