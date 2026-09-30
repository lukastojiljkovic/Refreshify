using System.Globalization;
using Refreshify.Core.Catalog;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Engine;

/// <summary>
/// Runs tools in catalog order, applies automatic remedies inline (each at most once per step), collects the issues that
/// need the user's consent, and stops between steps when cancelled.
/// </summary>
public sealed class RunEngine(IToolExecutor user, IToolExecutor admin)
{
    public const string DeclinedSummary = "Needs administrator approval, which was declined.";
    public const string NotRunSummary = "Not run, because the run was stopped.";

    private const string RestorePointId = "restore-point";

    public async Task<RunRecord> RunAsync(RunRequest request, IRunObserver observer, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.Now;
        var tools = Plan(request);
        var session = new Session(user, admin, [.. tools.Select(tool => new StepRecord(tool.Info.Id, tool.Info.Name))], request.Options, observer);

        var stopped = false;
        for (var index = 0; index < tools.Count; index++)
        {
            if (stopped || cancellationToken.IsCancellationRequested)
            {
                stopped = true;
                session.Update(index, step => step with { Status = StepStatus.Done, Result = new ToolResult(ToolOutcome.Cancelled, NotRunSummary) });
                continue;
            }

            stopped = !await session.RunStepAsync(index, tools[index], request.FixAutomatically, cancellationToken);
        }

        var id = started.UtcDateTime.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        return new RunRecord(id, request.Kind, started, session.Steps)
        {
            Finished = DateTimeOffset.Now,
            Cancelled = stopped || cancellationToken.IsCancellationRequested,
        };
    }

    /// <summary><b>Fix it</b> on a finished step: runs its remedy, or retries it when there is none to run.</summary>
    public async Task<RunRecord> FixAsync(RunRecord run, int index, ToolOptions options, IRunObserver observer, CancellationToken cancellationToken)
    {
        var step = run.Steps[index];
        var tool = ToolCatalog.Find(step.ToolId) ?? throw new ArgumentException($"There's no tool named {step.ToolId}.", nameof(run));
        var session = new Session(user, admin, [.. run.Steps], options, observer);

        session.Start(index);
        var result = step.CanFix
            ? await session.RemedyAsync(index, tool, step.Result!, step.Issue!, cancellationToken)
            : await session.ExecuteAsync(index, tool, cancellationToken);
        session.Finish(index, result);
        return run with { Steps = session.Steps };
    }

    /// <summary>The tools a run takes, in order, so the app can confirm them and list them before they start.</summary>
    public static List<Tool> Plan(RunRequest request)
    {
        var ids = request.ToolIds.ToHashSet(StringComparer.Ordinal);
        var tools = ToolCatalog.All.Where(tool => ids.Contains(tool.Info.Id) && tool.Info.Id != RestorePointId).ToList();

        // A restore point can only undo system changes, which only administrator tools make.
        if (request.CreateRestorePoint && tools.Any(tool => tool.Info.RunAs == RunAs.Administrator))
            tools.Insert(0, ToolCatalog.Find(RestorePointId)!);
        return tools;
    }

    private sealed class Session(IToolExecutor user, IToolExecutor admin, StepRecord[] steps, ToolOptions options, IRunObserver observer)
    {
        private bool _elevationDeclined;

        public IReadOnlyList<StepRecord> Steps => steps;

        public void Update(int index, Func<StepRecord, StepRecord> change)
        {
            steps[index] = change(steps[index]);
            observer.StepChanged(index, steps[index]);
        }

        public void Start(int index) =>
            Update(index, step => step with { Status = StepStatus.Running, Started = DateTimeOffset.Now, Finished = null });

        public void Finish(int index, ToolResult result) =>
            Update(index, step => step with { Status = StepStatus.Done, Result = result, Finished = DateTimeOffset.Now });

        /// <returns>Whether the run goes on.</returns>
        public async Task<bool> RunStepAsync(int index, Tool tool, bool fixAutomatically, CancellationToken cancellationToken)
        {
            Start(index);
            var result = await ExecuteAsync(index, tool, cancellationToken);
            if (fixAutomatically && !cancellationToken.IsCancellationRequested &&
                KnownIssues.Find(result.IssueId) is { Remedy: RemedyKind.Automatic } issue && !issue.FixToolIds.Contains(tool.Info.Id))
            {
                result = await RemedyAsync(index, tool, result, issue, cancellationToken);
            }

            Finish(index, result);
            return tool.Info.Id != RestorePointId || result.Outcome != ToolOutcome.Failed || await DecideRestorePointAsync(index, tool, cancellationToken);
        }

        public async Task<ToolResult> RemedyAsync(int index, Tool tool, ToolResult failed, KnownIssue issue, CancellationToken cancellationToken)
        {
            Update(index, step => step with { RemediedIssues = [.. step.RemediedIssues, issue.Id] });

            ToolResult? lastFix = null;
            foreach (var fix in issue.FixToolIds.Select(id => ToolCatalog.Find(id)!))
            {
                observer.ToolEvent(index, new ToolEvent(ToolEventKind.Status, $"Fixing: {fix.Info.Name}"));
                var fixResult = await ExecuteAsync(index, fix, cancellationToken);
                lastFix = fixResult;
                Update(index, step => step with { FixesTried = [.. step.FixesTried, $"{fix.Info.Name}: {fixResult.Summary}"] });

                // A tool that is its own fix, such as DISM repairing from a Windows image, was just retried.
                if (fix == tool)
                    return fixResult;

                // A fix that fails for a known reason hands that reason on, so the step offers the next remedy.
                if (fixResult.Outcome is not (ToolOutcome.Succeeded or ToolOutcome.Warning))
                    return KnownIssues.Find(fixResult.IssueId) is { } next && next.Id != issue.Id ? failed with { IssueId = next.Id } : failed;
            }

            if (cancellationToken.IsCancellationRequested)
                return failed;
            if (issue.RetryAfterFix || lastFix is null)
                return await ExecuteAsync(index, tool, cancellationToken);

            return failed with
            {
                Outcome = ToolOutcome.Warning,
                Summary = $"{failed.Summary} {lastFix.Summary}",
                RestartRequired = failed.RestartRequired || lastFix.RestartRequired,
            };
        }

        public async Task<ToolResult> ExecuteAsync(int index, Tool tool, CancellationToken cancellationToken)
        {
            var elevated = tool.Info.RunAs == RunAs.Administrator;
            if (elevated && _elevationDeclined)
                return ToolResult.Skipped(DeclinedSummary);

            var progress = new SynchronousProgress<ToolEvent>(toolEvent => observer.ToolEvent(index, toolEvent));
            var token = tool.Info.Has(ToolTraits.NotInterruptible) ? CancellationToken.None : cancellationToken;
            try
            {
                return await (elevated ? admin : user).RunAsync(tool, options, progress, token);
            }
            catch (ElevationDeclinedException)
            {
                _elevationDeclined = true;
                return ToolResult.Skipped(DeclinedSummary);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new ToolResult(ToolOutcome.Cancelled, "Stopped before it finished.");
            }
            catch (Exception ex)
            {
                return ToolResult.Failed(ex.Message, ex.HResult);
            }
        }

        private async Task<bool> DecideRestorePointAsync(int index, Tool tool, CancellationToken cancellationToken)
        {
            while (true)
            {
                var step = steps[index];
                var choice = await observer.RestorePointFailedAsync(step, step.CanFix);
                if (choice == RestorePointChoice.Cancel)
                    return false;
                if (choice == RestorePointChoice.Continue || !step.CanFix)
                    return true;

                Start(index);
                var result = await RemedyAsync(index, tool, step.Result!, step.Issue!, cancellationToken);
                Finish(index, result);
                if (result.Outcome != ToolOutcome.Failed)
                    return true;
            }
        }
    }
}
