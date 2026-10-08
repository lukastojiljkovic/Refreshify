using Refreshify.Core.Diagnostics;
using Refreshify.Core.Space;

namespace Refreshify.Models;

/// <summary>A row of the Disk space list: a folder or file, with its size and how much of its folder it takes.</summary>
/// <param name="Bar">The row's bar, as a percentage of the largest row in the list.</param>
/// <param name="Caption">The small line under the name: a folder's file count, or the folder a file is in.</param>
public sealed class SpaceRowItem(SpaceChild child, double bar, string caption, bool showBar, bool showShare)
{
    public string Name => child.Name;

    public string Path => child.Path;

    public bool IsFolder => child.IsFolder;

    public bool IsFile => !child.IsFolder;

    public string Glyph => IsFolder ? Glyphs.Folder : Glyphs.File;

    public string Size => Format.Bytes(child.Size);

    public double Bar => bar;

    public bool ShowBar => showBar;

    public string Share => $"{child.Share}%";

    public bool ShowShare => showShare;

    public string Caption => caption;

    public bool ShowCaption => Caption.Length > 0;

    public string OpenLabel => $"Open {Name} in File Explorer";

    public override string ToString() => Name;
}
