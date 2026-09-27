using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Refreshify.Services;
using Windows.Graphics;

namespace Refreshify;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindow();
        ApplyTheme(AppSettings.Theme);
        NavView.SelectedItem = HomeItem;
    }

    private nint WindowHandle => Win32Interop.GetWindowFromWindowId(AppWindow.Id);

    private void ConfigureWindow()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Refreshify.ico"));

        var scale = GetDpiForWindow(WindowHandle) / 96.0;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(820 * scale);
            presenter.PreferredMinimumHeight = (int)(560 * scale);
        }

        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min((int)(1180 * scale), workArea.Width);
        var height = Math.Min((int)(800 * scale), workArea.Height);
        AppWindow.MoveAndResize(new RectInt32(workArea.X + (workArea.Width - width) / 2, workArea.Y + (workArea.Height - height) / 2, width, height));
    }

    private void ApplyTheme(ElementTheme theme)
    {
        Root.RequestedTheme = theme;
        AppWindow.TitleBar.PreferredTheme = theme switch
        {
            ElementTheme.Light => TitleBarTheme.Light,
            ElementTheme.Dark => TitleBarTheme.Dark,
            _ => TitleBarTheme.UseDefaultAppMode,
        };
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
    }

    private void OnPaneToggleRequested(TitleBar sender, object args) => NavView.IsPaneOpen = !NavView.IsPaneOpen;

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint hwnd);
}
