using System.IO.Pipes;
using Refreshify.Core.Catalog;
using Refreshify.Core.Engine;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Worker;

/// <summary>
/// The elevated side: serves the app window that started it until that window disconnects. It runs only administrator
/// tools from its own catalog, with validated options.
/// </summary>
public sealed class WorkerHost(Func<string, Tool?> findTool, IToolExecutor executor)
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    public WorkerHost()
        : this(ToolCatalog.Find, LocalToolExecutor.Instance)
    {
    }

    public async Task RunAsync(string pipeName, int uiProcessId, CancellationToken cancellationToken)
    {
        await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(ConnectTimeout);
            await pipe.ConnectAsync(timeout.Token);
        }

        if (PipePeer.ServerProcessId(pipe) != uiProcessId)
            throw new UnauthorizedAccessException("The pipe doesn't belong to the Refreshify window that started this helper.");

        var peer = new PipePeer(pipe);
        var running = Task.CompletedTask;
        CancellationTokenSource? current = null;
        try
        {
            while (await peer.ReceiveAsync(cancellationToken) is { } message)
            {
                switch (message)
                {
                    case RunMessage run when running.IsCompleted:
                        current?.Dispose();
                        current = new CancellationTokenSource();
                        running = ServeAsync(peer, run, current.Token);
                        break;
                    case RunMessage run:
                        peer.TrySend(new ResultMessage(run.RequestId, ToolResult.Failed("Another tool is still running.")));
                        break;
                    case CancelMessage:
                        current?.Cancel();
                        break;
                }
            }
        }
        finally
        {
            // The window closed: stop the current tool, unless stopping it midway could harm Windows.
            current?.Cancel();
            await running;
            current?.Dispose();
        }
    }

    private async Task ServeAsync(PipePeer peer, RunMessage run, CancellationToken cancellationToken)
    {
        ToolResult result;
        if (findTool(run.ToolId) is not { Info.RunAs: RunAs.Administrator } tool)
        {
            result = ToolResult.Failed("This tool doesn't run as administrator.");
        }
        else if (!run.Options.IsValid)
        {
            result = ToolResult.Failed("The tool's options aren't valid.");
        }
        else
        {
            var progress = new SynchronousProgress<ToolEvent>(toolEvent => peer.TrySend(new EventMessage(run.RequestId, toolEvent)));
            var token = tool.Info.Has(ToolTraits.NotInterruptible) ? CancellationToken.None : cancellationToken;
            try
            {
                result = await executor.RunAsync(tool, run.Options, progress, token);
            }
            catch (Exception ex)
            {
                result = ToolResult.Failed(ex.Message, ex.HResult);
            }
        }

        peer.TrySend(new ResultMessage(run.RequestId, result));
    }
}
