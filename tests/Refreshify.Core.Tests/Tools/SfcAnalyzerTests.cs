using Refreshify.Core.Diagnostics;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Tests.Tools;

public sealed class SfcAnalyzerTests : IDisposable
{
    private const string Prefix = "2026-09-27 10:00:00, Info                  CSI    00000008 ";

    private readonly string _log = Path.GetTempFileName();

    public void Dispose() => File.Delete(_log);

    [Theory]
    [InlineData("Windows Resource Protection did not find any integrity violations.", SfcVerdict.Clean)]
    [InlineData("Windows Resource Protection found corrupt files and successfully repaired them.", SfcVerdict.Repaired)]
    [InlineData("Windows Resource Protection found corrupt files but was unable to fix some of them.", SfcVerdict.CannotRepair)]
    [InlineData("Windows Resource Protection could not start the repair service.", SfcVerdict.ServiceUnavailable)]
    [InlineData("There is a system repair pending which requires reboot to complete.  Restart Windows and run sfc again.", SfcVerdict.RestartPending)]
    [InlineData("Windows Resource Protection could not perform the requested operation.", SfcVerdict.CouldNotRun)]
    public void English_console_text_decides_first(string line, SfcVerdict verdict) =>
        Assert.Equal(verdict, SfcAnalyzer.Analyze(["Beginning system scan.  This process will take some time.", line], []));

    [Theory]
    [InlineData("Verification 42% complete.", 42.0)]
    [InlineData("Überprüfung zu 42 % abgeschlossen.", 42.0)]
    [InlineData("Beginning system scan.  This process will take some time.", null)]
    public void Progress_is_read_in_any_language(string line, double? percent) =>
        Assert.Equal(percent, SfcAnalyzer.ParseProgress(line));

    [Fact]
    public void On_other_languages_unrepairable_files_in_the_cbs_log_decide()
    {
        string[] console = ["Der Windows-Ressourcenschutz hat beschädigte Dateien gefunden."];
        string[] log = [$"{Prefix}[SR] Repairing corrupted file \\??\\C:\\Windows\\a.dll", $"{Prefix}[SR] Cannot repair member file [l:5]\"b.dll\""];

        Assert.Equal(SfcVerdict.CannotRepair, SfcAnalyzer.Analyze(console, log));
    }

    [Fact]
    public void Repaired_files_in_the_cbs_log_mean_repaired() =>
        Assert.Equal(SfcVerdict.Repaired, SfcAnalyzer.Analyze(["Überprüfung zu 100 % abgeschlossen."],
            [$"{Prefix}[SR] Verify complete", $"{Prefix}[SR] Repairing corrupted file \\??\\C:\\Windows\\a.dll", $"{Prefix}[SR] Repair complete"]));

    [Fact]
    public void A_completed_verification_without_repairs_means_clean() =>
        Assert.Equal(SfcVerdict.Clean, SfcAnalyzer.Analyze([], [$"{Prefix}[SR] Verifying 100 components", $"{Prefix}[SR] Verify complete"]));

    [Fact]
    public void Without_either_signal_the_result_is_unknown() =>
        Assert.Equal(SfcVerdict.Unknown, SfcAnalyzer.Analyze(["Проверка завершена."], []));

    [Theory]
    [InlineData(SfcVerdict.Clean, ToolOutcome.Succeeded, null)]
    [InlineData(SfcVerdict.Repaired, ToolOutcome.Succeeded, null)]
    [InlineData(SfcVerdict.CannotRepair, ToolOutcome.Failed, KnownIssues.SfcUnrepairable)]
    [InlineData(SfcVerdict.ServiceUnavailable, ToolOutcome.Failed, KnownIssues.SfcServiceDisabled)]
    [InlineData(SfcVerdict.RestartPending, ToolOutcome.Failed, KnownIssues.RestartPending)]
    [InlineData(SfcVerdict.CouldNotRun, ToolOutcome.Failed, null)]
    [InlineData(SfcVerdict.Unknown, ToolOutcome.Warning, null)]
    public void Verdicts_become_results(SfcVerdict verdict, ToolOutcome outcome, string? issue)
    {
        var result = SfcAnalyzer.ToResult(verdict);

        Assert.Equal((outcome, issue), (result.Outcome, result.IssueId));
    }

    [Fact]
    public void Only_sr_lines_written_after_the_offset_are_read()
    {
        File.WriteAllLines(_log, [$"{Prefix}[SR] Cannot repair member file old.dll", $"{Prefix}Unrelated"]);
        var offset = SfcAnalyzer.LogLength(_log);
        File.AppendAllLines(_log, [$"{Prefix}Loaded servicing stack", $"{Prefix}[SR] Verify complete"]);

        Assert.Equal([$"{Prefix}[SR] Verify complete"], SfcAnalyzer.ReadSrLines(_log, offset));
    }

    [Fact]
    public void A_log_that_was_rotated_is_read_from_the_start()
    {
        File.WriteAllLines(_log, [$"{Prefix}[SR] Verify complete"]);

        Assert.Single(SfcAnalyzer.ReadSrLines(_log, offset: 1_000_000));
    }

    [Fact]
    public void A_missing_log_has_no_lines() =>
        Assert.Empty(SfcAnalyzer.ReadSrLines(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), 0));

    [Fact]
    public void The_excerpt_keeps_the_lines_about_damaged_files()
    {
        string[] lines = [$"{Prefix}[SR] Verifying 100 components", $"{Prefix}[SR] Cannot repair member file b.dll", $"{Prefix}[SR] Verify complete"];

        Assert.Equal($"{Prefix}[SR] Cannot repair member file b.dll", SfcAnalyzer.Excerpt(lines));
        Assert.Null(SfcAnalyzer.Excerpt([$"{Prefix}[SR] Verify complete"]));
    }
}
