using System.Globalization;
using System.Text.RegularExpressions;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Platform;

namespace Refreshify.Core.Tools;

/// <summary>DISM runs with <c>/English</c> and reports its result as an HRESULT exit code.</summary>
public static partial class Dism
{
    public static ProcessSpec CleanupImage(string arguments) =>
        ProcessSpec.System32("dism.exe", $"/Online /Cleanup-Image {arguments} /English");

    /// <summary>The percentage in DISM's <c>[====  42.0%  ]</c> progress bar.</summary>
    public static double? ParseProgress(string line)
    {
        var match = ProgressPattern().Match(line);
        return match.Success ? double.Parse(match.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture) : null;
    }

    public static ToolResult Classify(int exitCode, string summary) => exitCode switch
    {
        0 => ToolResult.Succeeded(summary),
        ErrorCodes.SuccessRebootRequired => ToolResult.Succeeded(summary) with { RestartRequired = true },
        _ => ToolResult.Failed($"It stopped with error {ErrorCodes.Format(exitCode)}.", exitCode, KnownIssues.FromHResult(exitCode)),
    };

    [GeneratedRegex(@"^\[[= ]*(\d{1,3}[.,]\d)%[= ]*\]")]
    private static partial Regex ProgressPattern();
}
