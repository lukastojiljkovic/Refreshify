namespace Refreshify.Core.Space;

/// <summary>How far a scan has got: folders read and bytes counted, for the progress line.</summary>
public sealed record SpaceProgress(long Folders, long Bytes);

/// <summary>What a scan found, and what it had to leave out.</summary>
/// <param name="Root">The tree below the scanned path.</param>
/// <param name="UnreadableFolders">How many folders couldn't be read.</param>
/// <param name="UnreadablePaths">The first of them, up to <see cref="SpaceScanner.ReportedUnreadablePaths"/>.</param>
/// <param name="OnlineOnlyFiles">Cloud files that take no space on this PC, which weren't counted.</param>
/// <param name="Partial">The scan was stopped early, so the tree is only what it counted so far.</param>
public sealed record SpaceScan(
    SpaceFolder Root,
    long UnreadableFolders,
    IReadOnlyList<string> UnreadablePaths,
    long OnlineOnlyFiles,
    bool Partial);
