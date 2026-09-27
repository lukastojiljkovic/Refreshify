using Refreshify.Core.Diagnostics;
using Refreshify.Core.Engine;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Tests.Diagnostics;

public class ReportBuilderTests
{
    private static readonly SystemInfo System = new("Windows 11 Pro", "25H2", "26200.6899", "X64", "de-DE", "1.0.0");

    private static readonly Redactor Redactor = new(@"C:\Users\Luka", "Luka", "LUKA-PC");

    private static RunRecord Run() => new("20260927-101500-000", RunKind.All, new DateTimeOffset(2026, 9, 27, 10, 15, 0, TimeSpan.Zero),
    [
        new StepRecord("temp-files", "Your temporary files")
        {
            Status = StepStatus.Done,
            Result = ToolResult.Succeeded("Freed 1.0 GB (20 files)."),
        },
        new StepRecord("sfc", "System file check")
        {
            Status = StepStatus.Done,
            Result = ToolResult.Failed("Found damaged system files it couldn't repair.", unchecked((int)0x800F081F), KnownIssues.SfcUnrepairable) with
            {
                Trace = new ToolTrace(
                    ["sfc.exe /scannow (exit code 0)"],
                    [@"Details are in C:\Users\Luka\AppData\Local\Temp\x.log on LUKA-PC"],
                    "[SR] Cannot repair member file a.dll"),
            },
            FixesTried = ["System image repair: It stopped with error 0x800F081F."],
        },
    ]);

    [Fact]
    public void The_report_has_the_system_the_run_and_the_failed_step()
    {
        var report = ReportBuilder.Build(Run(), 1, System, redactor: null);

        Assert.Contains("Windows 11 Pro, version 25H2 (build 26200.6899), X64", report);
        Assert.Contains("Display language: de-DE", report);
        Assert.Contains("Refreshify: 1.0.0", report);
        Assert.Contains("- Your temporary files: Succeeded. Freed 1.0 GB (20 files).", report);
        Assert.Contains("## Failed step: System file check", report);
        Assert.Contains("Error code: 0x800F081F", report);
        Assert.Contains("Detected problem: Some system files couldn't be repaired", report);
        Assert.Contains("- System image repair: It stopped with error 0x800F081F.", report);
        Assert.Contains("sfc.exe /scannow (exit code 0)", report);
        Assert.Contains("[SR] Cannot repair member file a.dll", report);
        Assert.StartsWith("# ", report);
    }

    [Fact]
    public void Personal_details_are_replaced_when_hidden()
    {
        var report = ReportBuilder.Build(Run(), 1, System, Redactor);

        Assert.DoesNotContain("Luka", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LUKA-PC", report);
        Assert.Contains(@"%USERPROFILE%\AppData\Local\Temp\x.log on <computer>", report);
    }

    [Fact]
    public void Personal_details_stay_when_not_hidden() =>
        Assert.Contains(@"C:\Users\Luka\AppData", ReportBuilder.Build(Run(), 1, System, redactor: null));

    [Fact]
    public void Short_user_names_are_not_replaced_inside_other_words()
    {
        var redactor = new Redactor(@"C:\Users\al", "al", "PC1");

        Assert.Equal("<user> ran a total of 3 tools on <computer>.", redactor.Apply("al ran a total of 3 tools on PC1."));
    }

    [Theory]
    [InlineData("Windows 10 Pro", 26200, "Windows 11 Pro")]
    [InlineData("Windows 10 Pro", 19045, "Windows 10 Pro")]
    [InlineData("Windows 11 Home", 26200, "Windows 11 Home")]
    public void Windows_11_is_named_by_its_build(string productName, int build, string expected) =>
        Assert.Equal(expected, SystemInfo.ProductName(productName, build));
}
