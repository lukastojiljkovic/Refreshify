using Refreshify.Core.Worker;

namespace Refreshify;

public static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        switch (args)
        {
            case ["--worker", var pipeName, var id] when int.TryParse(id, out var uiProcessId):
                return RunWorker(pipeName, uiProcessId);
        }

        XamlGeneratedProgram.XamlGeneratedMain();
        return 0;
    }

    /// <summary>The elevated helper has no window: it serves the app window that started it, then exits.</summary>
    private static int RunWorker(string pipeName, int uiProcessId)
    {
        try
        {
            new WorkerHost().RunAsync(pipeName, uiProcessId, CancellationToken.None).GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception ex)
        {
            return ex.HResult;
        }
    }
}
