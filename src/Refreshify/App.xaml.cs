using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;

namespace Refreshify;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();

        // An error in an event handler shouldn't take the window, or a run in progress, down with it.
        UnhandledException += (_, e) =>
        {
            if (_window is null)
                return;
            e.Handled = true;
            _window.ShowStatus(InfoBarSeverity.Error, "Something went wrong", e.Message);
        };
    }

    public static string Version { get; } = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.2.0";

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = _window = new MainWindow();
        AppInstance.GetCurrent().Activated += (_, _) => window.DispatcherQueue.TryEnqueue(window.BringToFront);
        window.Activate();
    }
}
