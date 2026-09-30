using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Refreshify.Core.Catalog;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Engine;
using Refreshify.Core.Tools;
using Refreshify.Services;

namespace Refreshify.Models;

/// <summary>A run on the Run page: the one in progress, or a past one opened from History.</summary>
public sealed class RunModel : Observable
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    private RunRecord _record;
    private DispatcherQueueTimer? _clock;
    private DateTimeOffset _began;
    private StepItem? _selected;
    private bool _pinned;

    /// <param name="isLive">A run of this session, which can still be cancelled, fixed and retried.</param>
    public RunModel(RunRecord record, bool isLive)
    {
        _record = record;
        IsLive = isLive;
        ShowTechnicalDetails = AppSettings.ShowTechnicalDetails;
        Steps = [.. record.Steps.Select((step, index) => new StepItem(this, index, step))];
        _selected = FirstNeedingAttention ?? Steps.FirstOrDefault();
    }

    public string Title => TitleOf(_record);

    public string StartedText => $"Started {When(_record.Started)}";

    public bool IsLive { get; }

    public bool ShowTechnicalDetails { get; }

    public IReadOnlyList<StepItem> Steps { get; }

    /// <summary>The step whose details are shown: the one picked in the list, or else the running one, and after the run the
    /// first one that needs attention.</summary>
    public StepItem? Selected => _selected;

    /// <summary>The run with every step as it is now.</summary>
    public RunRecord Record => _record with { Steps = [.. Steps.Select(step => step.Record)] };

    public bool IsBusy { get; private set; }

    public bool IsStopping { get; private set; }

    public bool CanCancel => IsBusy && !IsStopping;

    public string Status => IsStopping
        ? "Stopping after the current step"
        : Steps.FirstOrDefault(step => step.IsRunning) is { } running
            ? $"Step {running.Index + 1} of {Steps.Count}: {running.Name}"
            : "Starting";

    public string ElapsedLabel => IsBusy ? "Elapsed" : "Duration";

    /// <summary>For a <b>Fix it</b>, the time since it started; the run's own duration stays in History.</summary>
    public string Elapsed => IsBusy
        ? Format.Duration(DateTimeOffset.Now - _began)
        : _record.Finished is { } finished ? Format.Duration(finished - _record.Started) : string.Empty;

    /// <summary>Steps that ran to the end, so a stopped run doesn't count the ones it never started.</summary>
    public string StepsFinished =>
        $"{Steps.Count(step => step.Record.Status == StepStatus.Done && step.Record.Result?.Outcome != ToolOutcome.Cancelled)} of {Steps.Count}";

    public string Freed => Format.Bytes(Record.BytesFreed);

    public string Attention => NeedingAttention > 0 ? NeedingAttention.ToString(English) : "None";

    public double Progress => 100.0 * Steps.Count(step => step.Record.Status == StepStatus.Done) / Math.Max(1, Steps.Count);

    public bool ShowSummary => !IsBusy && _record.Finished is not null;

    public InfoBarSeverity SummarySeverity =>
        _record.Cancelled ? InfoBarSeverity.Informational : NeedingAttention > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success;

    public string SummaryTitle =>
        _record.Cancelled ? "Stopped" : NeedingAttention > 0 ? "Finished, but some steps need your attention" : "All done";

    /// <summary>The space freed and the steps that need attention are in the numbers above, so this doesn't repeat them.</summary>
    public string SummaryMessage
    {
        get
        {
            var message = _record.Cancelled ? "The remaining steps didn't run."
                : NeedingAttention > 0 ? "Select a marked step to see what happened and what you can do."
                : "Every step finished.";
            return Record.RestartRequired ? $"{message} Restart your PC to finish." : message;
        }
    }

    public bool RestartRequired => IsLive && Record.RestartRequired;

    private int NeedingAttention => Steps.Count(NeedsAttention);

    private StepItem? FirstNeedingAttention => Steps.FirstOrDefault(NeedsAttention);

    public static string TitleOf(RunRecord record) => record.Kind switch
    {
        RunKind.All => "Run all",
        _ when record.Steps.LastOrDefault() is { } last && ToolCatalog.Find(last.ToolId) is { } tool =>
            record.Kind == RunKind.Category ? CategoryInfo.Get(tool.Info.Category).Name : tool.Info.Name,
        _ => "Run",
    };

    public static string When(DateTimeOffset time) => time.ToLocalTime().ToString("MMMM d, yyyy 'at' h:mm tt", English);

    public void Begin()
    {
        IsBusy = true;
        IsStopping = false;
        _began = DateTimeOffset.Now;
        if (_clock is null)
        {
            _clock = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _clock.Interval = TimeSpan.FromSeconds(1);
            _clock.Tick += (_, _) => Tick();
        }

        _clock.Start();
        RefreshAll();
    }

    public void Stopping()
    {
        IsStopping = true;
        Changed(string.Empty);
    }

    public void End(RunRecord record)
    {
        _record = record;
        IsBusy = false;
        IsStopping = false;
        _clock?.Stop();
        for (var index = 0; index < Steps.Count; index++)
            Steps[index].Update(record.Steps[index]);
        if (!_pinned)
            _selected = FirstNeedingAttention ?? _selected;
        RefreshAll();
    }

    public void Update(int index, StepRecord step)
    {
        Steps[index].Update(step);
        if (!_pinned && step.Status == StepStatus.Running)
            _selected = Steps[index];
        Changed(string.Empty);
    }

    /// <summary>A step picked in the list stays shown; picking the running step follows the run again.</summary>
    public void Pick(StepItem step)
    {
        if (ReferenceEquals(step, _selected))
            return;
        _selected = step;
        _pinned = !step.IsRunning;
        Changed(nameof(Selected));
    }

    public void Report(int index, ToolEvent toolEvent) => Steps[index].Report(toolEvent);

    private static bool NeedsAttention(StepItem step) => step.Record.Result?.Outcome is ToolOutcome.Failed or ToolOutcome.Warning;

    private void Tick()
    {
        Changed(nameof(Elapsed));
        foreach (var step in Steps)
            step.Tick();
    }

    private void RefreshAll()
    {
        Changed(string.Empty);
        foreach (var step in Steps)
            step.Refresh();
    }
}
