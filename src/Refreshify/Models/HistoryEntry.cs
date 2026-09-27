using Microsoft.UI.Xaml;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Engine;
using Refreshify.Core.Tools;

namespace Refreshify.Models;

/// <summary>A past run in the History list.</summary>
public sealed class HistoryEntry(RunRecord record)
{
    public RunRecord Record { get; } = record;

    public string Title => RunModel.TitleOf(Record);

    public string When => RunModel.When(Record.Started);

    public string Summary
    {
        get
        {
            List<string> parts = [Format.Count(Record.Steps.Count, "step")];
            if (Record.BytesFreed > 0)
                parts.Add($"freed {Format.Bytes(Record.BytesFreed)}");
            if (Count(ToolOutcome.Failed) is > 0 and var failed)
                parts.Add($"{failed} failed");
            if (Count(ToolOutcome.Warning) is > 0 and var warnings)
                parts.Add($"{Format.Count(warnings, "warning")}");
            if (Record.Cancelled)
                parts.Add("stopped");
            return string.Join(" · ", parts);
        }
    }

    public Style BadgeStyle => (Style)Application.Current.Resources[
        Record.Cancelled ? "InformationalIconInfoBadgeStyle"
        : Count(ToolOutcome.Failed) > 0 ? "CriticalIconInfoBadgeStyle"
        : Count(ToolOutcome.Warning) > 0 ? "CautionIconInfoBadgeStyle"
        : "SuccessIconInfoBadgeStyle"];

    public string Label => $"{Title}, {When}";

    public override string ToString() => Label;

    private int Count(ToolOutcome outcome) => Record.Steps.Count(step => step.Result?.Outcome == outcome);
}
