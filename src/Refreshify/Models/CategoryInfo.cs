using Refreshify.Core.Tools;

namespace Refreshify.Models;

/// <summary>A tool category as the navigation and its page show it.</summary>
public sealed record CategoryInfo(ToolCategory Category, string Name, string Glyph, string Description)
{
    public static IReadOnlyList<CategoryInfo> All { get; } =
    [
        new(ToolCategory.Cleanup, "Cleanup", Glyphs.Cleanup,
            "Frees up space by removing files that Windows and your apps don't need anymore."),
        new(ToolCategory.Repair, "Repair", Glyphs.Repair,
            "Checks Windows' system files and your disk, and repairs what's damaged."),
        new(ToolCategory.Network, "Network", Glyphs.Network,
            "Clears network caches and syncs your clock."),
        new(ToolCategory.UpdatesAndSecurity, "Updates & security", Glyphs.UpdatesAndSecurity,
            "Updates Windows, your apps and Microsoft Defender, and checks for threats."),
        new(ToolCategory.Troubleshooting, "Troubleshooting", Glyphs.Troubleshooting,
            "Fixes for specific problems. Run one when you notice the problem it describes. They aren't part of Run all unless you include them."),
    ];

    public static CategoryInfo Get(ToolCategory category) => All.First(info => info.Category == category);

    public override string ToString() => Name;
}
