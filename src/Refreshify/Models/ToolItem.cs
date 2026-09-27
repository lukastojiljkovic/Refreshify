using Refreshify.Core.Tools;
using Refreshify.Services;

namespace Refreshify.Models;

/// <summary>A tool card on a category page.</summary>
public sealed class ToolItem(ToolInfo info)
{
    public ToolInfo Info { get; } = info;

    public string Name => Info.Name;

    public string Glyph => Info.Glyph;

    public string Description => Info.Description;

    public string UseWhen => Info.UseWhen ?? string.Empty;

    public bool HasUseWhen => Info.UseWhen is not null;

    public string Technical => Info.Technical;

    public string RunsAs => Info.RunAs == RunAs.Administrator
        ? "Runs as administrator. Windows asks for your approval once per session."
        : "Runs with your account.";

    public IReadOnlyList<string> Badges { get; } =
    [
        .. new (ToolTraits Trait, string Label)[]
        {
            (ToolTraits.LongRunning, "Takes a while"),
            (ToolTraits.RestartRequired, "Needs a restart"),
            (ToolTraits.RestartsExplorer, "Restarts File Explorer"),
            (ToolTraits.MayCloseApps, "May close apps"),
            (ToolTraits.NeedsInternet, "Needs internet"),
        }.Where(badge => info.Has(badge.Trait)).Select(badge => badge.Label),
    ];

    public bool HasBadges => Badges.Count > 0;

    public bool IsInRunAll
    {
        get => AppSettings.IsInRunAll(Info);
        set => AppSettings.SetInRunAll(Info, value);
    }

    public string IncludeLabel => $"Include {Name} in Run all";

    public string RunLabel => $"Run {Name}";

    public string DetailsLabel => $"Details about {Name}";

    public override string ToString() => Name;
}
