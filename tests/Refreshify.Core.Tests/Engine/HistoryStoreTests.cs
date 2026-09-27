using Refreshify.Core.Engine;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Tests.Engine;

public sealed class HistoryStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"refreshify-history-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static RunRecord Run(int minute) => new(
        $"20260927-10{minute:00}00-000", RunKind.All, new DateTimeOffset(2026, 9, 27, 10, minute, 0, TimeSpan.Zero),
        [
            new StepRecord("sfc", "System file check")
            {
                Status = StepStatus.Done,
                Result = ToolResult.Failed("Found damaged system files it couldn't repair.", 5, "sfc-unrepairable") with
                {
                    Details = ["a.dll"],
                    Trace = new ToolTrace(["sfc.exe /scannow (exit code 0)"], ["Verification 100% complete."], "[SR] Cannot repair member file"),
                },
                FixesTried = ["System image repair: It stopped with error 0x800F081F."],
                RemediedIssues = ["sfc-unrepairable"],
            },
        ])
    {
        Finished = new DateTimeOffset(2026, 9, 27, 10, minute, 30, TimeSpan.Zero),
    };

    [Fact]
    public void A_saved_run_loads_back_unchanged()
    {
        var store = new HistoryStore(_directory);
        var run = Run(1);

        store.Save(run);
        var loaded = Assert.Single(store.Load());

        Assert.Equal((run.Id, run.Kind, run.Started, run.Finished), (loaded.Id, loaded.Kind, loaded.Started, loaded.Finished));
        var step = Assert.Single(loaded.Steps);
        Assert.Equal(run.Steps[0].Result!.Summary, step.Result!.Summary);
        Assert.Equal((5, "sfc-unrepairable"), (step.Result.ErrorCode, step.Result.IssueId));
        Assert.Equal(["a.dll"], step.Result.Details);
        Assert.Equal("[SR] Cannot repair member file", step.Result.Trace!.LogExcerpt);
        Assert.Equal(run.Steps[0].FixesTried, step.FixesTried);
        Assert.Equal(run.Steps[0].RemediedIssues, step.RemediedIssues);
    }

    [Fact]
    public void Runs_load_newest_first_and_only_the_most_recent_are_kept()
    {
        var store = new HistoryStore(_directory, capacity: 3);

        for (var minute = 1; minute <= 5; minute++)
            store.Save(Run(minute));

        Assert.Equal(["20260927-100500-000", "20260927-100400-000", "20260927-100300-000"], store.Load().Select(run => run.Id));
    }

    [Fact]
    public void Damaged_files_are_skipped()
    {
        var store = new HistoryStore(_directory);
        store.Save(Run(1));
        File.WriteAllText(Path.Combine(_directory, "20260927-110000-000.json"), "{ not json");

        Assert.Single(store.Load());
    }

    [Fact]
    public void Clear_removes_every_run()
    {
        var store = new HistoryStore(_directory);
        store.Save(Run(1));

        store.Clear();

        Assert.Empty(store.Load());
    }

    [Fact]
    public void A_missing_folder_means_no_history() => Assert.Empty(new HistoryStore(_directory).Load());
}
