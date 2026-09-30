using Microsoft.UI.Xaml;
using Refreshify.Core.Catalog;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Engine;
using Refreshify.Core.Tools;

namespace Refreshify.Models;

/// <summary>A step of a run: its live status while it runs, then its result and what can be done about it.</summary>
public sealed class StepItem(RunModel run, int index, StepRecord record) : Observable
{
    private const int MaxOutputLines = 300;
    private const int ActivityLines = 6;

    private readonly List<string> _output = [];
    private string? _status;
    private double? _percent;

    public int Index { get; } = index;

    public StepRecord Record { get; private set; } = record;

    public string Name => Record.Name;

    public string Description => ToolCatalog.Find(Record.ToolId)?.Info.Description ?? string.Empty;

    public bool IsRunning => Record.Status == StepStatus.Running;

    public string Summary => Record.Status switch
    {
        StepStatus.Pending => "Waiting",
        StepStatus.Running => _status ?? "Starting",
        _ => Result?.Summary ?? string.Empty,
    };

    public double Percent => _percent ?? 0;

    public bool IsIndeterminate => _percent is null;

    public string PercentText => _percent is { } percent ? $"{percent:0}%" : string.Empty;

    /// <summary>Counts up while the step runs, then shows how long it took. Runs saved before 1.1 have no times.</summary>
    public string Time => Record switch
    {
        { Status: StepStatus.Running, Started: { } started } => Format.Duration(DateTimeOffset.Now - started),
        { Status: StepStatus.Done, Started: { } started, Finished: { } finished } => Format.Duration(finished - started),
        _ => string.Empty,
    };

    /// <summary>The last lines the step printed, while it runs.</summary>
    public string Activity => IsRunning ? string.Join('\n', OutputLines.Latest(_output, ActivityLines)) : string.Empty;

    public bool HasActivity => Activity.Length > 0;

    /// <summary>The Windows status icons for results that need a color; the others use <see cref="Glyph"/>.</summary>
    public Style? BadgeStyle => Record.Status != StepStatus.Done ? null : Result?.Outcome switch
    {
        ToolOutcome.Succeeded => Resource("SuccessIconInfoBadgeStyle"),
        ToolOutcome.Warning => Resource("CautionIconInfoBadgeStyle"),
        ToolOutcome.Failed => Resource("CriticalIconInfoBadgeStyle"),
        _ => null,
    };

    public bool HasBadge => BadgeStyle is not null;

    public string Glyph => Result?.Outcome switch
    {
        ToolOutcome.Skipped => Glyphs.Skipped,
        ToolOutcome.Cancelled => Glyphs.Cancelled,
        _ => Glyphs.Pending,
    };

    public bool HasGlyph => !IsRunning && !HasBadge;

    public bool HasIssue => Issue is not null;

    public string IssueTitle => Issue?.Title ?? string.Empty;

    public string IssueExplanation => Issue?.Explanation ?? string.Empty;

    public string Details => string.Join('\n', [.. Record.FixesTried.Select(fix => $"Tried {fix}"), .. Result?.Details ?? []]);

    public bool HasDetails => Record.Status == StepStatus.Done && Details.Length > 0;

    public bool ShowFix => run.IsLive && Record.CanFix;

    public string FixLabel => Record.Issue?.FixLabel ?? "Fix it";

    public bool ShowRestart => run.IsLive && Issue?.Remedy == RemedyKind.Restart;

    /// <summary>Failures without a fix to run, and steps skipped because the UAC prompt was declined, can simply run again.</summary>
    public bool ShowRetry => run.IsLive && !Record.CanFix && !ShowRestart &&
        (NeedsAttention || Result?.Summary == RunEngine.DeclinedSummary);

    /// <summary>Past runs can't be fixed anymore, so every problem in them offers help.</summary>
    public bool ShowHelp => NeedsAttention && !(run.IsLive && Record.CanFix);

    public bool HasActions => ShowFix || ShowRestart || ShowRetry || ShowHelp;

    public bool CanAct => !run.IsBusy;

    public bool ShowOutput => run.ShowTechnicalDetails && Output.Length > 0;

    /// <summary>The live output while the app runs the step; the saved trace for a past run.</summary>
    public string Output => _output.Count > 0
        ? string.Join('\n', _output)
        : string.Join('\n', [.. Result?.Trace?.Commands.Select(command => $"> {command}") ?? [], .. Result?.Trace?.Output ?? []]);

    private ToolResult? Result => Record.Result;

    private bool NeedsAttention => Record.Status == StepStatus.Done && Result?.Outcome is ToolOutcome.Failed or ToolOutcome.Warning;

    private KnownIssue? Issue => NeedsAttention ? Record.Issue : null;

    public void Update(StepRecord record)
    {
        Record = record;
        if (record.Status != StepStatus.Running)
            (_status, _percent) = (null, null);
        Changed(string.Empty);
    }

    public void Report(ToolEvent toolEvent)
    {
        if (toolEvent.Kind == ToolEventKind.Status)
        {
            (_status, _percent) = (toolEvent.Text, toolEvent.Percent);
        }
        else if (_output.Count > 0 && OutputLines.IsProgressUpdate(_output[^1], toolEvent.Text))
        {
            _output[^1] = toolEvent.Text;
        }
        else
        {
            _output.Add(toolEvent.Text);
            if (_output.Count > MaxOutputLines)
                _output.RemoveAt(0);
        }

        Changed(string.Empty);
    }

    public void Refresh() => Changed(string.Empty);

    public void Tick()
    {
        if (IsRunning)
            Changed(nameof(Time));
    }

    public override string ToString() => Name;

    private static Style Resource(string key) => (Style)Application.Current.Resources[key];
}
