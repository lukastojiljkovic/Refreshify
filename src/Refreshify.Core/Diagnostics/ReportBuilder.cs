using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Refreshify.Core.Engine;

namespace Refreshify.Core.Diagnostics;

/// <summary>A Markdown report about a failed step, for the user to paste into an LLM. Nothing is sent anywhere.</summary>
public static class ReportBuilder
{
    private const string Request =
        "I ran Refreshify, a Windows maintenance app that runs the built-in Windows tools, and one step failed. " +
        "Please help me fix it step by step: tell me which commands to run, whether they need a terminal opened as " +
        "administrator, and what to look for in their output.";

    public static string Build(RunRecord run, int failedStep, SystemInfo system, Redactor? redactor)
    {
        var step = run.Steps[failedStep];
        var result = step.Result;
        var text = new StringBuilder();

        text.AppendLine("# Refreshify troubleshooting report").AppendLine().AppendLine(Request).AppendLine();

        text.AppendLine("## System").AppendLine();
        text.AppendLine($"- Windows: {system.Edition}, version {system.Version} (build {system.Build}), {system.Architecture}");
        text.AppendLine($"- Display language: {system.DisplayLanguage}");
        text.AppendLine($"- Refreshify: {system.AppVersion}").AppendLine();

        text.AppendLine("## Run").AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"Started {run.Started.ToLocalTime():yyyy-MM-dd HH:mm} ({Describe(run.Kind)}).").AppendLine();
        foreach (var other in run.Steps)
            text.AppendLine($"- {other.Name}: {other.Result?.Outcome.ToString() ?? "Not run"}. {other.Result?.Summary}".TrimEnd());
        text.AppendLine();

        text.AppendLine($"## Failed step: {step.Name}").AppendLine();
        text.AppendLine($"- Result: {result?.Outcome.ToString() ?? "Not run"}. {result?.Summary}".TrimEnd());
        if (result?.ErrorCode is { } code)
            text.AppendLine($"- Error code: {ErrorCodes.Format(code)}");
        if (step.Issue is { } issue)
            text.AppendLine($"- Detected problem: {issue.Title}");

        List("Fixes already tried", step.FixesTried);
        List("Details", result?.Details ?? []);
        Block("Commands", result?.Trace?.Commands);
        Block("Last lines of output", result?.Trace?.Output);
        Block("Log excerpt", result?.Trace?.LogExcerpt is { } excerpt ? excerpt.Split('\n') : null);

        var report = text.ToString();
        return redactor?.Apply(report) ?? report;

        void List(string title, IReadOnlyList<string> items)
        {
            if (items.Count == 0)
                return;
            text.AppendLine().AppendLine($"{title}:").AppendLine();
            foreach (var item in items)
                text.AppendLine($"- {item}");
        }

        void Block(string title, IReadOnlyList<string>? lines)
        {
            if (lines is not { Count: > 0 })
                return;
            text.AppendLine().AppendLine($"### {title}").AppendLine().AppendLine("```text");
            foreach (var line in lines)
                text.AppendLine(line);
            text.AppendLine("```");
        }
    }

    private static string Describe(RunKind kind) => kind switch
    {
        RunKind.All => "Run all",
        RunKind.Category => "one category",
        _ => "a single tool",
    };
}

/// <summary>Replaces the profile path, computer name and user name with placeholders.</summary>
public sealed class Redactor(string profilePath, string userName, string computerName)
{
    public static Redactor Current { get; } = new(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.UserName, Environment.MachineName);

    public string Apply(string text)
    {
        if (profilePath.Length > 0)
            text = text.Replace(profilePath, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);

        // The computer name often contains the user name, so it goes first.
        return ReplaceWord(ReplaceWord(text, computerName, "<computer>"), userName, "<user>");
    }

    /// <summary>Whole words only, so a short name isn't replaced inside other words.</summary>
    private static string ReplaceWord(string text, string word, string placeholder) =>
        word.Length < 2
            ? text
            : Regex.Replace(text, $@"(?<!\w){Regex.Escape(word)}(?!\w)", placeholder, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
