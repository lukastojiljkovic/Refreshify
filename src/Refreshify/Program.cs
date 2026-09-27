using System.Runtime.InteropServices;
using Microsoft.Windows.AppLifecycle;
using Refreshify.Core.Worker;
using Refreshify.Services;

namespace Refreshify;

public static partial class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        switch (args)
        {
            // The elevated helper has no window: it serves the app window that started it, then exits.
            case ["--worker", var pipeName, var id] when int.TryParse(id, out var uiProcessId):
                return Run(() => new WorkerHost().RunAsync(pipeName, uiProcessId, CancellationToken.None));
            case ["--reminder"]:
                return Run(() =>
                {
                    Reminder.ShowIfDue();
                    return Task.CompletedTask;
                });
            case ["--uninstall"]:
                return Run(UninstallAsync);
        }

        // One window: a second start hands its activation to the first, which comes to the front.
        var instance = AppInstance.FindOrRegisterForKey("Refreshify");
        if (!instance.IsCurrent)
        {
            AllowSetForegroundWindow(instance.ProcessId);
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            Task.Run(() => instance.RedirectActivationToAsync(activation).AsTask()).Wait();
            return 0;
        }

        XamlGeneratedProgram.XamlGeneratedMain();
        return 0;
    }

    /// <summary>Run by the uninstaller: removes everything Refreshify created for the current user.</summary>
    private static async Task UninstallAsync()
    {
        await Reminder.RemoveAsync();
        AppSettings.Clear();
        if (Directory.Exists(RunCoordinator.DataDirectory))
            Directory.Delete(RunCoordinator.DataDirectory, recursive: true);
    }

    /// <summary>Windowless modes report failure through the exit code.</summary>
    private static int Run(Func<Task> mode)
    {
        try
        {
            mode().GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception ex)
        {
            return ex.HResult;
        }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(uint processId);
}
