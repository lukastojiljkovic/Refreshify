using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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

    public static string Version { get; } = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
