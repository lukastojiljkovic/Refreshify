using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Platform;
using static Refreshify.Core.Diagnostics.ErrorCodes;

namespace Refreshify.Core.Tools;

/// <summary>winget results come from its documented <c>0x8A15xxxx</c> exit codes, not from its localized text.</summary>
public static partial class Winget
{
    /// <summary>The per-user App Installer alias; the elevated worker runs as the same user, so it resolves there too.</summary>
    public static string Executable => Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Microsoft\WindowsApps\winget.exe");

    public static ProcessSpec UpgradeAll() =>
        new(Executable, "upgrade --all --silent --accept-source-agreements --accept-package-agreements --disable-interactivity")
        {
            Encoding = new UTF8Encoding(false),
        };

    /// <summary>winget numbers the packages it upgrades as <c>(2/5)</c>; the prefix is the same in every language.</summary>
    public static (int Index, int Count)? ParseCounter(string line)
    {
        var match = PackageCounter().Match(line);
        return match.Success
            ? (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture))
            : null;
    }

    public static ToolResult Classify(int exitCode, IEnumerable<string> output)
    {
        var updates = output.Select(ParseCounter).Max(counter => counter?.Count) ?? 0;
        var updated = updates > 0 ? $"Updated {Diagnostics.Format.Count(updates, "app")}." : "Your apps are up to date.";

        return exitCode switch
        {
            0 or WingetNoApplicationsFound or WingetUpdateNotApplicable => ToolResult.Succeeded(updated),
            WingetRebootRequiredToFinish => ToolResult.Succeeded(updated) with { RestartRequired = true },
            WingetUpdateAllHasFailure or WingetPackageInUse or WingetFileInUse or WingetPackageInUseByApplication =>
                ToolResult.Warning("Some apps couldn't update, usually because they were open.") with
                {
                    IssueId = KnownIssues.AppsInUse,
                    ErrorCode = exitCode,
                },
            WingetSourcesInvalid or WingetSourceDataMissing or WingetSourceOpenFailed =>
                ToolResult.Failed("winget couldn't read its list of apps.", exitCode, KnownIssues.WingetSources),
            _ => ToolResult.Failed($"It stopped with error {ErrorCodes.Format(exitCode)}.", exitCode, KnownIssues.FromHResult(exitCode)),
        };
    }

    [GeneratedRegex(@"^\((\d+)/(\d+)\)")]
    private static partial Regex PackageCounter();
}
