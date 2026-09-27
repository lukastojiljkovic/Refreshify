using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Refreshify.Core.Worker;

/// <summary>One end of the worker pipe: JSON lines in, JSON lines out.</summary>
internal sealed partial class PipePeer(PipeStream pipe)
{
    private static readonly UTF8Encoding Utf8 = new(false);

    private readonly StreamReader _reader = new(pipe, Utf8, false, 4096, leaveOpen: true);
    private readonly Lock _writeLock = new();

    /// <summary>Synchronous and locked, so messages from several threads keep their order.</summary>
    public void Send(WorkerMessage message)
    {
        var bytes = Utf8.GetBytes(JsonSerializer.Serialize(message, WorkerJson.Default.WorkerMessage) + "\n");
        lock (_writeLock)
        {
            pipe.Write(bytes);
            pipe.Flush();
        }
    }

    /// <summary>Sends unless the other end is gone, for messages that don't matter once it is.</summary>
    public void TrySend(WorkerMessage message)
    {
        try
        {
            Send(message);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // Disconnected.
        }
    }

    /// <returns>The next message, or <see langword="null"/> when the other end has closed the pipe.</returns>
    public async Task<WorkerMessage?> ReceiveAsync(CancellationToken cancellationToken)
    {
        var line = await _reader.ReadLineAsync(cancellationToken);
        return line is null ? null : JsonSerializer.Deserialize(line, WorkerJson.Default.WorkerMessage);
    }

    public static int? ClientProcessId(PipeStream pipe) =>
        GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var id) ? (int)id : null;

    public static int? ServerProcessId(PipeStream pipe) =>
        GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var id) ? (int)id : null;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);
}
