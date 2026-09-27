using Refreshify.Core.Diagnostics;

namespace Refreshify.Core.Tools;

public enum SfcVerdict
{
    Clean,
    Repaired,
    CannotRepair,
    ServiceUnavailable,
    RestartPending,
    CouldNotRun,
    Unknown,
}

/// <summary>
/// Judges an SFC run from its English console text, or, on other display languages, from the <c>[SR]</c> lines it wrote to
/// CBS.log, which is English on every locale.
/// </summary>
public static class SfcAnalyzer
{
    private const int MaxExcerptLines = 40;

    private static readonly (string Text, SfcVerdict Verdict)[] ConsoleVerdicts =
    [
        ("did not find any integrity violations", SfcVerdict.Clean),
        ("found corrupt files and successfully repaired them", SfcVerdict.Repaired),
        ("unable to fix some of them", SfcVerdict.CannotRepair),
        ("could not start the repair service", SfcVerdict.ServiceUnavailable),
        ("system repair pending", SfcVerdict.RestartPending),
        ("could not perform the requested operation", SfcVerdict.CouldNotRun),
    ];

    public static string LogPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Logs\CBS\CBS.log");

    public static SfcVerdict Analyze(IReadOnlyList<string> console, IReadOnlyList<string> srLines)
    {
        foreach (var (text, verdict) in ConsoleVerdicts)
        {
            if (console.Any(line => line.Contains(text, StringComparison.OrdinalIgnoreCase)))
                return verdict;
        }

        if (srLines.Any(IsUnrepairable))
            return SfcVerdict.CannotRepair;
        if (srLines.Any(IsRepair))
            return SfcVerdict.Repaired;
        return srLines.Any(line => line.Contains("[SR] Verify complete", StringComparison.Ordinal)) ? SfcVerdict.Clean : SfcVerdict.Unknown;
    }

    public static ToolResult ToResult(SfcVerdict verdict) => verdict switch
    {
        SfcVerdict.Clean => ToolResult.Succeeded("No damaged system files were found."),
        SfcVerdict.Repaired => ToolResult.Succeeded("Found damaged system files and repaired them."),
        SfcVerdict.CannotRepair => ToolResult.Failed("Found damaged system files it couldn't repair.", issueId: KnownIssues.SfcUnrepairable),
        SfcVerdict.ServiceUnavailable => ToolResult.Failed("Its repair service couldn't start.", issueId: KnownIssues.SfcServiceDisabled),
        SfcVerdict.RestartPending => ToolResult.Failed("An earlier repair is waiting for a restart.", issueId: KnownIssues.RestartPending),
        SfcVerdict.CouldNotRun => ToolResult.Failed("System file check couldn't run."),
        _ => ToolResult.Warning("The check finished, but its result couldn't be read. The technical details show its output."),
    };

    /// <summary>Recorded before SFC starts, so only the lines of this run are read afterwards.</summary>
    public static long LogLength(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

    /// <summary>
    /// The <c>[SR]</c> lines written after <paramref name="offset"/>. When Windows has rotated the log in the meantime, it
    /// is shorter than the offset and is read from the start.
    /// </summary>
    public static IReadOnlyList<string> ReadSrLines(string path, long offset)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Position = offset <= stream.Length ? offset : 0;
            using var reader = new StreamReader(stream);

            var lines = new List<string>();
            while (reader.ReadLine() is { } line)
            {
                if (line.Contains("[SR]", StringComparison.Ordinal))
                    lines.Add(line);
            }

            return lines;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>The lines about damaged files, for the technical details and the LLM report.</summary>
    public static string? Excerpt(IReadOnlyList<string> srLines)
    {
        var relevant = srLines.Where(line => IsUnrepairable(line) || IsRepair(line)).Take(MaxExcerptLines).ToList();
        return relevant.Count > 0 ? string.Join('\n', relevant) : null;
    }

    private static bool IsUnrepairable(string line) =>
        line.Contains("[SR] Cannot repair member file", StringComparison.Ordinal) ||
        line.Contains("[SR] Could not reproject corrupted file", StringComparison.Ordinal);

    private static bool IsRepair(string line) => line.Contains("[SR] Repairing corrupted file", StringComparison.Ordinal);
}
