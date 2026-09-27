using System.Globalization;
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

    /// <param name="isLive">A run of this session, which can still be cancelled, fixed and retried.</param>
    public RunModel(RunRecord record, bool isLive)
    {
        _record = record;
        IsLive = isLive;
        ShowTechnicalDetails = AppSettings.ShowTechnicalDetails;
        Steps = [.. record.Steps.Select((step, index) => new StepItem(this, index, step))];
    }

    public string Title => TitleOf(_record);

    public string StartedText => $"Started {When(_record.Started)}";

    public bool IsLive { get; }

    public bool ShowTechnicalDetails { get; }

    public IReadOnlyList<StepItem> Steps { get; }

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

    public double Progress => 100.0 * Steps.Count(step => step.Record.Status == StepStatus.Done) / Math.Max(1, Steps.Count);

    public bool ShowSummary => !IsBusy && _record.Finished is not null;

    public InfoBarSeverity SummarySeverity =>
        _record.Cancelled ? InfoBarSeverity.Informational : NeedingAttention > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success;

    public string SummaryTitle =>
        _record.Cancelled ? "Stopped" : NeedingAttention > 0 ? "Finished, but some steps need your attention" : "All done";

    public string SummaryMessage
    {
        get
        {
            var record = Record;
            List<string> parts = [];
            if (record.BytesFreed > 0)
                parts.Add($"Freed {Format.Bytes(record.BytesFreed)}.");
            if (NeedingAttention is > 0 and var count)
                parts.Add($"{Format.Count(count, "step")} {(count == 1 ? "needs" : "need")} your attention.");
            if (record.RestartRequired)
                parts.Add("Restart your PC to finish.");
            return parts.Count > 0 ? string.Join(' ', parts) : "Every step finished.";
        }
    }

    public bool RestartRequired => IsLive && Record.RestartRequired;

    private int NeedingAttention => Steps.Count(step => step.Record.Result?.Outcome is ToolOutcome.Failed or ToolOutcome.Warning);

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
        for (var index = 0; index < Steps.Count; index++)
            Steps[index].Update(record.Steps[index]);
        RefreshAll();
    }

    public void Update(int index, StepRecord step)
    {
        Steps[index].Update(step);
        Changed(string.Empty);
    }

    public void Report(int index, ToolEvent toolEvent) => Steps[index].Report(toolEvent);

    private void RefreshAll()
    {
        Changed(string.Empty);
        foreach (var step in Steps)
            step.Refresh();
    }
}
