using System.Globalization;

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
}
