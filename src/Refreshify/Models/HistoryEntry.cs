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

    public string Duration => Record.Finished is { } finished ? Format.Duration(finished - Record.Started) : "—";

    public string Outcomes => Format.Outcomes(Record.Steps.Select(step => step.Result?.Outcome).OfType<ToolOutcome>());

    public string Freed => Record.BytesFreed > 0 ? Format.Bytes(Record.BytesFreed) : "—";

    public Style BadgeStyle => (Style)Application.Current.Resources[
        Record.Cancelled ? "InformationalIconInfoBadgeStyle"
        : Count(ToolOutcome.Failed) > 0 ? "CriticalIconInfoBadgeStyle"
        : Count(ToolOutcome.Warning) > 0 ? "CautionIconInfoBadgeStyle"
        : "SuccessIconInfoBadgeStyle"];

    public string Label => $"{Title}, {When}, {Outcomes}";

    public override string ToString() => Label;

    private int Count(ToolOutcome outcome) => Record.Steps.Count(step => step.Result?.Outcome == outcome);
}
