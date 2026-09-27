using Microsoft.UI.Dispatching;
using Refreshify.Core.Engine;
using Refreshify.Core.Tools;
using Refreshify.Core.Worker;
using Refreshify.Models;

namespace Refreshify.Services;

/// <summary>
/// Runs tools for the window: user tools in this process, administrator tools in the elevated worker, which starts on
/// the first administrator step and closes with the window. Progress arrives on background threads and is moved to the
/// UI thread. Finished runs go to History, and their raw output to Logs.
/// </summary>
internal sealed class RunCoordinator : IRunObserver, IAsyncDisposable
{
    private const int KeptLogs = 50;

    private readonly WorkerClient _worker = new();
    private readonly RunEngine _engine;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly Func<StepRecord, bool, Task<RestorePointChoice>> _askRestorePoint;
    private readonly List<string> _log = [];
    private RunModel? _running;
    private int _loggedStep = -1;
    private CancellationTokenSource? _cancellation;

    /// <param name="askRestorePoint">Asks what to do when the restore point fails; called on the UI thread.</param>
    public RunCoordinator(Func<StepRecord, bool, Task<RestorePointChoice>> askRestorePoint)
    {
        _engine = new RunEngine(LocalToolExecutor.Instance, _worker);
        _askRestorePoint = askRestorePoint;
    }

    public event EventHandler? Finished;

    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Refreshify");

    public static string LogDirectory { get; } = Path.Combine(DataDirectory, "Logs");

    public HistoryStore History { get; } = new(HistoryStore.DefaultDirectory);

    /// <summary>The run of this session, from the moment it starts.</summary>
    public RunModel? Current { get; private set; }

    public bool IsBusy => _cancellation is not null;

    public Task RunAsync(RunRequest request, IReadOnlyList<Tool> plan)
    {
        var steps = plan.Select(tool => new StepRecord(tool.Info.Id, tool.Info.Name)).ToList();
        var run = Current = new RunModel(new RunRecord(string.Empty, request.Kind, DateTimeOffset.Now, steps), isLive: true);
        return ExecuteAsync(run, token => _engine.RunAsync(request, this, token));
    }

    /// <summary><b>Fix it</b> or <b>Try again</b> on a step of the current run.</summary>
    public Task FixAsync(int index, ToolOptions options)
    {
        var run = Current!;
        return ExecuteAsync(run, token => _engine.FixAsync(run.Record, index, options, this, token));
    }

    /// <summary>Stops after the current step; steps that must not be interrupted finish first.</summary>
    public void Cancel()
    {
        if (_cancellation is not { IsCancellationRequested: false } cancellation)
            return;
        cancellation.Cancel();
        _running?.Stopping();
    }

    public void ClearHistory()
    {
        History.Clear();
        if (Directory.Exists(LogDirectory))
        {
            foreach (var log in Directory.GetFiles(LogDirectory, "*.log"))
                File.Delete(log);
        }
    }

    public ValueTask DisposeAsync()
    {
        _cancellation?.Cancel();
        return _worker.DisposeAsync();
    }

    void IRunObserver.StepChanged(int index, StepRecord step) => _dispatcher.TryEnqueue(() => _running?.Update(index, step));

    void IRunObserver.ToolEvent(int index, ToolEvent toolEvent) => _dispatcher.TryEnqueue(() =>
    {
        _running?.Report(index, toolEvent);
        if (toolEvent.Kind == ToolEventKind.Output)
            Log(index, toolEvent.Text);
    });

    Task<RestorePointChoice> IRunObserver.RestorePointFailedAsync(StepRecord step, bool canFix)
    {
        var choice = new TaskCompletionSource<RestorePointChoice>();
        _dispatcher.TryEnqueue(async () =>
        {
            try
            {
                choice.SetResult(await _askRestorePoint(step, canFix));
            }
            catch (Exception ex)
            {
                choice.SetException(ex);
            }
        });
        return choice.Task;
    }

    private async Task ExecuteAsync(RunModel run, Func<CancellationToken, Task<RunRecord>> execute)
    {
        using var cancellation = _cancellation = new CancellationTokenSource();
        _running = run;
        run.Begin();
        try
        {
            var record = await execute(cancellation.Token);
            run.End(record);
            Save(record);
        }
        finally
        {
            _cancellation = null;
            if (run.IsBusy)
                run.End(run.Record);
            Finished?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Save(RunRecord record)
    {
        if (record.Kind == RunKind.All && !record.Cancelled)
            AppSettings.LastFullRefresh = record.Finished;

        History.Save(record);
        if (_log.Count == 0)
            return;

        Directory.CreateDirectory(LogDirectory);
        File.AppendAllLines(Path.Combine(LogDirectory, $"{record.Id}.log"), _log);
        _log.Clear();
        _loggedStep = -1;
        foreach (var old in Directory.GetFiles(LogDirectory, "*.log").OrderDescending(StringComparer.Ordinal).Skip(KeptLogs))
            File.Delete(old);
    }

    private void Log(int index, string line)
    {
        if (index != _loggedStep)
        {
            _loggedStep = index;
            _log.Add($"== {_running!.Steps[index].Name} ==");
        }

        if (OutputLines.IsProgressUpdate(_log[^1], line))
            _log[^1] = line;
        else
            _log.Add(line);
    }
}
