using Refreshify.Core.Diagnostics;
using Refreshify.Core.Engine;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Tests.Engine;

public class RunEngineTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Returns queued results per tool id (success when none are left) and records every call.</summary>
    private sealed class FakeExecutor : IToolExecutor
    {
        private readonly Dictionary<string, Queue<ToolResult>> _results = [];

        public List<string> Calls { get; } = [];

        public List<(string Id, bool CanBeCanceled)> Tokens { get; } = [];

        public bool DeclineElevation { get; init; }

        public Action<string>? OnRun { get; init; }

        public FakeExecutor Returns(string id, params ToolResult[] results)
        {
            _results[id] = new Queue<ToolResult>(results);
            return this;
        }

        public Task<ToolResult> RunAsync(Tool tool, ToolOptions options, IProgress<ToolEvent> progress, CancellationToken cancellationToken)
        {
            if (DeclineElevation)
                throw new ElevationDeclinedException();

            Calls.Add(tool.Info.Id);
            Tokens.Add((tool.Info.Id, cancellationToken.CanBeCanceled));
            OnRun?.Invoke(tool.Info.Id);
            return Task.FromResult(_results.TryGetValue(tool.Info.Id, out var queue) && queue.Count > 0
                ? queue.Dequeue()
                : ToolResult.Succeeded($"{tool.Info.Id} done"));
        }
    }

    private sealed class Observer(params RestorePointChoice[] choices) : IRunObserver
    {
        private readonly Queue<RestorePointChoice> _choices = new(choices);

        public List<bool> RestorePointPrompts { get; } = [];

        public void StepChanged(int index, StepRecord step)
        {
        }

        public void ToolEvent(int index, ToolEvent toolEvent)
        {
        }

        public Task<RestorePointChoice> RestorePointFailedAsync(StepRecord step, bool canFix)
        {
            RestorePointPrompts.Add(canFix);
            return Task.FromResult(_choices.Dequeue());
        }
    }

    private static RunRequest Request(params string[] ids) => new(RunKind.All, ids, new ToolOptions(), CreateRestorePoint: false);

    private static ToolResult Failure(string issue) => ToolResult.Failed("It failed.", 1, issue);

    private static Task<RunRecord> RunAsync(FakeExecutor user, FakeExecutor admin, RunRequest request, IRunObserver? observer = null) =>
        new RunEngine(user, admin).RunAsync(request, observer ?? new Observer(), Ct);

    [Fact]
    public async Task Tools_run_in_catalog_order_with_the_restore_point_first()
    {
        FakeExecutor user = new(), admin = new();

        var run = await RunAsync(user, admin, Request("sfc", "temp-files", "dism-restorehealth") with { CreateRestorePoint = true });

        Assert.Equal(["restore-point", "temp-files", "dism-restorehealth", "sfc"], run.Steps.Select(step => step.ToolId));
        Assert.Equal(["restore-point", "dism-restorehealth", "sfc"], admin.Calls);
        Assert.Equal(["temp-files"], user.Calls);
        Assert.All(run.Steps, step => Assert.Equal((StepStatus.Done, ToolOutcome.Succeeded), (step.Status, step.Result!.Outcome)));
    }

    [Fact]
    public async Task A_run_of_user_tools_needs_no_restore_point()
    {
        var run = await RunAsync(new FakeExecutor(), new FakeExecutor(), Request("temp-files") with { CreateRestorePoint = true });

        Assert.Equal(["temp-files"], run.Steps.Select(step => step.ToolId));
    }

    [Fact]
    public async Task Declining_elevation_skips_every_admin_step_but_user_steps_still_run()
    {
        FakeExecutor user = new(), admin = new() { DeclineElevation = true };

        var run = await RunAsync(user, admin, Request("temp-files", "windows-temp", "sfc", "shader-cache"));

        Assert.Equal(["temp-files", "shader-cache"], user.Calls);
        Assert.Equal(
            [ToolOutcome.Succeeded, ToolOutcome.Skipped, ToolOutcome.Skipped, ToolOutcome.Succeeded],
            run.Steps.Select(step => step.Result!.Outcome));
        Assert.Equal(RunEngine.DeclinedSummary, run.Steps[1].Result!.Summary);
    }

    [Fact]
    public async Task An_automatic_remedy_runs_its_fix_and_retries_the_tool_once()
    {
        var admin = new FakeExecutor().Returns("windows-update", Failure(KnownIssues.WuServiceDisabled), ToolResult.Succeeded("Installed 2 updates."));

        var run = await RunAsync(new FakeExecutor(), admin, Request("windows-update"));

        Assert.Equal(["windows-update", "fix-enable-wuauserv", "windows-update"], admin.Calls);
        var step = Assert.Single(run.Steps);
        Assert.Equal("Installed 2 updates.", step.Result!.Summary);
        Assert.Equal(["Turn on the Windows Update service: fix-enable-wuauserv done"], step.FixesTried);
        Assert.False(step.CanFix);
    }

    [Fact]
    public async Task A_remedy_that_does_not_help_is_not_repeated()
    {
        var admin = new FakeExecutor().Returns("windows-update", Failure(KnownIssues.WuServiceDisabled), Failure(KnownIssues.WuServiceDisabled));

        var run = await RunAsync(new FakeExecutor(), admin, Request("windows-update"));

        Assert.Equal(["windows-update", "fix-enable-wuauserv", "windows-update"], admin.Calls);
        Assert.Equal(ToolOutcome.Failed, run.Steps[0].Result!.Outcome);
        Assert.False(run.Steps[0].CanFix);
    }

    [Fact]
    public async Task With_automatic_fixes_off_the_step_offers_the_fix_instead()
    {
        var admin = new FakeExecutor().Returns("windows-update", Failure(KnownIssues.WuServiceDisabled));

        var run = await RunAsync(new FakeExecutor(), admin, Request("windows-update") with { FixAutomatically = false });

        Assert.Equal(["windows-update"], admin.Calls);
        Assert.True(run.Steps[0].CanFix);
    }

    [Fact]
    public async Task Issues_that_need_consent_are_collected_and_the_run_continues()
    {
        var admin = new FakeExecutor().Returns("disk-check", Failure(KnownIssues.DiskErrors));

        var run = await RunAsync(new FakeExecutor(), admin, Request("disk-check", "network-caches"));

        Assert.Equal(["disk-check", "network-caches"], admin.Calls);
        Assert.True(run.Steps[0].CanFix);
    }

    [Fact]
    public async Task Fix_it_runs_the_fix_and_retries_the_step()
    {
        var admin = new FakeExecutor().Returns("app-updates", Failure(KnownIssues.WingetSources), ToolResult.Succeeded("Updated 3 apps."));
        var engine = new RunEngine(new FakeExecutor(), admin);
        var run = await engine.RunAsync(Request("app-updates") with { FixAutomatically = false }, new Observer(), Ct);

        run = await engine.FixAsync(run, 0, new ToolOptions(), new Observer(), Ct);

        Assert.Equal(["app-updates", "fix-winget-source-reset", "app-updates"], admin.Calls);
        Assert.Equal(("Updated 3 apps.", false), (run.Steps[0].Result!.Summary, run.Steps[0].CanFix));
    }

    [Fact]
    public async Task A_fix_without_a_retry_reports_what_the_fix_did()
    {
        var admin = new FakeExecutor()
            .Returns("disk-check", Failure(KnownIssues.DiskErrors))
            .Returns("fix-schedule-disk-repair", ToolResult.Succeeded("The repair runs the next time you restart your PC.") with { RestartRequired = true });
        var engine = new RunEngine(new FakeExecutor(), admin);
        var run = await engine.RunAsync(Request("disk-check"), new Observer(), Ct);

        run = await engine.FixAsync(run, 0, new ToolOptions(), new Observer(), Ct);

        Assert.Equal(["disk-check", "fix-schedule-disk-repair"], admin.Calls);
        var result = run.Steps[0].Result!;
        Assert.Equal((ToolOutcome.Warning, true), (result.Outcome, result.RestartRequired));
        Assert.EndsWith("The repair runs the next time you restart your PC.", result.Summary);
    }

    [Fact]
    public async Task When_the_tool_is_its_own_fix_the_fix_run_is_the_retry()
    {
        var admin = new FakeExecutor().Returns("dism-restorehealth", Failure(KnownIssues.DismSourceUnavailable), ToolResult.Succeeded("Repaired."));
        var engine = new RunEngine(new FakeExecutor(), admin);
        var run = await engine.RunAsync(Request("dism-restorehealth"), new Observer(), Ct);

        run = await engine.FixAsync(run, 0, new ToolOptions(WindowsImagePath: @"D:\win.iso"), new Observer(), Ct);

        Assert.Equal(["dism-restorehealth", "dism-restorehealth"], admin.Calls);
        Assert.Equal("Repaired.", run.Steps[0].Result!.Summary);
    }

    [Fact]
    public async Task A_failing_fix_hands_its_own_known_issue_to_the_step()
    {
        var admin = new FakeExecutor()
            .Returns("sfc", Failure(KnownIssues.SfcUnrepairable))
            .Returns("dism-restorehealth", Failure(KnownIssues.DismSourceUnavailable));

        var run = await RunAsync(new FakeExecutor(), admin, Request("sfc"));

        Assert.Equal(["sfc", "dism-restorehealth"], admin.Calls);
        Assert.Equal((KnownIssues.DismSourceUnavailable, true), (run.Steps[0].Result!.IssueId, run.Steps[0].CanFix));
    }

    [Fact]
    public async Task Cancelling_stops_before_the_next_step()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var admin = new FakeExecutor { OnRun = _ => cancellation.Cancel() };

        var run = await new RunEngine(new FakeExecutor(), admin).RunAsync(Request("windows-temp", "sfc"), new Observer(), cancellation.Token);

        Assert.Equal(["windows-temp"], admin.Calls);
        Assert.True(run.Cancelled);
        Assert.Equal((ToolOutcome.Cancelled, RunEngine.NotRunSummary), (run.Steps[1].Result!.Outcome, run.Steps[1].Result!.Summary));
    }

    [Fact]
    public async Task Tools_that_must_not_be_interrupted_get_no_cancellation_token()
    {
        var admin = new FakeExecutor();

        await RunAsync(new FakeExecutor(), admin, Request("windows-temp", "sfc"));

        Assert.Equal([("windows-temp", true), ("sfc", false)], admin.Tokens);
    }

    [Fact]
    public async Task A_failed_restore_point_asks_and_cancel_stops_the_run()
    {
        var admin = new FakeExecutor().Returns("restore-point", ToolResult.Failed("Windows didn't create a restore point."));
        var observer = new Observer(RestorePointChoice.Cancel);

        var run = await RunAsync(new FakeExecutor(), admin, Request("sfc") with { CreateRestorePoint = true }, observer);

        Assert.Equal([false], observer.RestorePointPrompts);
        Assert.Equal(["restore-point"], admin.Calls);
        Assert.True(run.Cancelled);
    }

    [Fact]
    public async Task A_failed_restore_point_can_be_fixed_and_retried()
    {
        var admin = new FakeExecutor().Returns("restore-point", Failure(KnownIssues.SystemProtectionOff), ToolResult.Succeeded("Created a restore point."));
        var observer = new Observer(RestorePointChoice.Fix);

        var run = await RunAsync(new FakeExecutor(), admin, Request("sfc") with { CreateRestorePoint = true }, observer);

        Assert.Equal([true], observer.RestorePointPrompts);
        Assert.Equal(["restore-point", "fix-enable-system-protection", "restore-point", "sfc"], admin.Calls);
        Assert.Equal("Created a restore point.", run.Steps[0].Result!.Summary);
    }

    [Fact]
    public async Task Continuing_without_a_restore_point_runs_the_rest()
    {
        var admin = new FakeExecutor().Returns("restore-point", ToolResult.Failed("Windows didn't create a restore point."));

        var run = await RunAsync(new FakeExecutor(), admin, Request("sfc") with { CreateRestorePoint = true }, new Observer(RestorePointChoice.Continue));

        Assert.Equal(["restore-point", "sfc"], admin.Calls);
        Assert.False(run.Cancelled);
    }

    [Fact]
    public async Task Unknown_ids_are_ignored() =>
        Assert.Equal(["sfc"], (await RunAsync(new FakeExecutor(), new FakeExecutor(), Request("format-c", "sfc"))).Steps.Select(step => step.ToolId));
}
