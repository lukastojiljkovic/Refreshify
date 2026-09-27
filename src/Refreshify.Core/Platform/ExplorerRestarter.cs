using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Refreshify.Core.Platform;

/// <summary>
/// Closes File Explorer through Restart Manager, as installers do, so it can save its state and is restarted with it.
/// Must run unelevated: Explorer belongs to the signed-in user.
/// </summary>
public static partial class ExplorerRestarter
{
    private const int ForceShutdown = 0x1;
    private static readonly TimeSpan RestartTimeout = TimeSpan.FromSeconds(10);

    public static T Restart<T>(Func<T> whileClosed)
    {
        var processes = SessionExplorers();
        var session = StartSession();
        try
        {
            if (processes.Length > 0)
            {
                Check(RmRegisterResources(session, 0, 0, (uint)processes.Length, processes, 0, 0));

                // If Explorer doesn't close completely, the work still runs; files it keeps open are skipped.
                _ = RmShutdown(session, ForceShutdown, 0);
            }

            try
            {
                return whileClosed();
            }
            finally
            {
                if (processes.Length > 0)
                    _ = RmRestart(session, 0, 0);
                EnsureRunning();
            }
        }
        finally
        {
            _ = RmEndSession(session);
        }
    }

    /// <summary>Restart Manager restarts Explorer asynchronously; if it hasn't come back, start it as the shell.</summary>
    private static void EnsureRunning()
    {
        var deadline = DateTime.UtcNow + RestartTimeout;
        while (SessionExplorers().Length == 0)
        {
            if (DateTime.UtcNow > deadline)
            {
                Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"))?.Dispose();
                return;
            }

            Thread.Sleep(250);
        }
    }

    private static UniqueProcess[] SessionExplorers()
    {
        using var current = Process.GetCurrentProcess();
        var sessionId = current.SessionId;
        var result = new List<UniqueProcess>();
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            using (process)
            {
                try
                {
                    if (process.SessionId != sessionId)
                        continue;
                    var startTime = process.StartTime.ToFileTime();
                    result.Add(new UniqueProcess(process.Id, (uint)startTime, (uint)(startTime >> 32)));
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    // Exited or not ours.
                }
            }
        }

        return [.. result];
    }

    private static unsafe uint StartSession()
    {
        var key = stackalloc char[33];
        Check(RmStartSession(out var session, 0, key));
        return session;
    }

    private static void Check(int error)
    {
        if (error != 0)
            throw new Win32Exception(error);
    }

    /// <summary><c>RM_UNIQUE_PROCESS</c>: the start time tells a process apart from a later one that reuses its id.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct UniqueProcess(int ProcessId, uint StartTimeLow, uint StartTimeHigh);

    [LibraryImport("rstrtmgr.dll")]
    private static unsafe partial int RmStartSession(out uint session, int flags, char* sessionKey);

    [LibraryImport("rstrtmgr.dll")]
    private static partial int RmRegisterResources(
        uint session, uint fileCount, nint files, uint applicationCount, UniqueProcess[] applications, uint serviceCount, nint services);

    [LibraryImport("rstrtmgr.dll")]
    private static partial int RmShutdown(uint session, int flags, nint statusCallback);

    [LibraryImport("rstrtmgr.dll")]
    private static partial int RmRestart(uint session, int flags, nint statusCallback);

    [LibraryImport("rstrtmgr.dll")]
    private static partial int RmEndSession(uint session);
}
