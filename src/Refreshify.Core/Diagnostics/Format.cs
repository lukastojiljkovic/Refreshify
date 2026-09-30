using System.Globalization;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Diagnostics;

/// <summary>English formatting for summaries, matching the English UI regardless of the regional format.</summary>
public static class Format
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private static readonly string[] Units = ["KB", "MB", "GB", "TB"];

    public static string Bytes(long bytes)
    {
        if (bytes < 1024)
            return Count(bytes, "byte");

        double value = bytes;
        var unit = -1;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Format(English, "{0:0.0} {1}", value, Units[unit]);
    }

    public static string Count(long count, string noun) =>
        string.Format(English, "{0:N0} {1}{2}", count, noun, count == 1 ? string.Empty : "s");

    /// <summary>Like a clock: <c>6:10</c>, or <c>1:02:03</c> from an hour on. Negative, when the clock was set back, is zero.</summary>
    public static string Duration(TimeSpan duration)
    {
        var time = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        return time.TotalHours >= 1
            ? string.Format(English, "{0}:{1:mm\\:ss}", (int)time.TotalHours, time)
            : string.Format(English, "{0}:{1:ss}", time.Minutes, time);
    }

    /// <summary>How the steps of a run ended, such as <c>14 succeeded · 1 warning · 1 failed</c>.</summary>
    public static string Outcomes(IEnumerable<ToolOutcome> outcomes) => string.Join(" · ", outcomes
        .CountBy(outcome => outcome)
        .OrderBy(group => group.Key)
        .Select(group => group.Key switch
        {
            ToolOutcome.Succeeded => $"{group.Value:N0} succeeded",
            ToolOutcome.Warning => Count(group.Value, "warning"),
            ToolOutcome.Failed => $"{group.Value:N0} failed",
            ToolOutcome.Skipped => $"{group.Value:N0} skipped",
            _ => $"{group.Value:N0} stopped",
        }));
}
