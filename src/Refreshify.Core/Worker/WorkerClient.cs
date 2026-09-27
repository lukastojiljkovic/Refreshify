using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Engine;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Worker;

/// <summary>
/// Runs administrator tools in one elevated worker per session, started on the first request so there is a single UAC
/// prompt. The pipe admits only the current user and Administrators, and only the process that was launched.
/// </summary>
/// <param name="launch">Starts the worker for a pipe name and returns its process id.</param>
public sealed class WorkerClient(Func<string, int> launch) : IToolExecutor, IAsyncDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private NamedPipeServerStream? _pipe;
    private PipePeer? _peer;
    private int _lastRequestId;

    public WorkerClient()
        : this(LaunchElevated)
    {
    }

    public async Task<ToolResult> RunAsync(Tool tool, ToolOptions options, IProgress<ToolEvent> progress, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(CancellationToken.None);
        try
        {
            var peer = await ConnectAsync(cancellationToken);
            var id = ++_lastRequestId;
            peer.Send(new RunMessage(id, tool.Info.Id, options));
            await using var cancel = cancellationToken.Register(() => peer.TrySend(new CancelMessage(id)));

            while (await peer.ReceiveAsync(CancellationToken.None) is { } message)
            {
                if (message is EventMessage { } update && update.RequestId == id)
                    progress.Report(update.Event);
                else if (message is ResultMessage { } result && result.RequestId == id)
                    return result.Result;
            }

            throw new IOException("Refreshify's administrator helper stopped unexpectedly.");
        }
        catch (IOException)
        {
            Disconnect();
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        Disconnect();
        _lock.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Starts this executable elevated in worker mode. Declining the UAC prompt throws <c>ERROR_CANCELLED</c>.</summary>
    public static int LaunchElevated(string pipeName)
    {
        using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, $"--worker {pipeName} {Environment.ProcessId}")
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        }) ?? throw new InvalidOperationException("Refreshify's administrator helper didn't start.");
        return process.Id;
    }

    private async Task<PipePeer> ConnectAsync(CancellationToken cancellationToken)
    {
        if (_peer is not null && _pipe is { IsConnected: true })
            return _peer;

        Disconnect();
        var name = $"Refreshify-{Guid.NewGuid():N}";
        var pipe = NamedPipeServerStreamAcl.Create(
            name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, Security());
        try
        {
            int expected;
            try
            {
                expected = launch(name);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCodes.ElevationCancelled)
            {
                throw new ElevationDeclinedException();
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ConnectTimeout);
            try
            {
                await pipe.WaitForConnectionAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Refreshify's administrator helper didn't start in time.");
            }

            if (PipePeer.ClientProcessId(pipe) != expected)
                throw new UnauthorizedAccessException("An unexpected process connected to Refreshify's administrator helper.");

            _pipe = pipe;
            _peer = new PipePeer(pipe);
            return _peer;
        }
        catch
        {
            pipe.Dispose();
            throw;
        }
    }

    private void Disconnect()
    {
        _pipe?.Dispose();
        _pipe = null;
        _peer = null;
    }

    private static PipeSecurity Security()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        return security;
    }
}
