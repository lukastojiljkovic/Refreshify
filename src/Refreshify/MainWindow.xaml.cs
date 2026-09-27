using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using Refreshify.Core.Engine;
using Refreshify.Core.Tools;
using Refreshify.Dialogs;
using Refreshify.Models;
using Refreshify.Services;
using Refreshify.Views;
using Windows.Graphics;

namespace Refreshify;

public sealed partial class MainWindow : Window
{
    private bool _closeAfterRun;

    public MainWindow()
    {
        InitializeComponent();
        Dialogs = new DialogService(Root);
        Runs = new RunCoordinator(Dialogs.RestorePointFailedAsync);
        Runs.Finished += OnRunFinished;
        ConfigureWindow();
        ApplyTheme(AppSettings.Theme);

        var index = NavView.MenuItems.IndexOf(CategoriesEnd);
        foreach (var category in CategoryInfo.All)
            NavView.MenuItems.Insert(index++, new NavigationViewItem { Content = category.Name, Tag = category.Category, Icon = new FontIcon { Glyph = category.Glyph } });
        NavView.SelectedItem = HomeItem;

        Root.Loaded += async (_, _) =>
        {
            if (!AppSettings.ShowWelcome)
                return;
            var welcome = new WelcomeDialog();
            await Dialogs.ShowAsync(welcome);
            if (welcome.DontShowAgain)
                AppSettings.ShowWelcome = false;
        };
    }

    internal DialogService Dialogs { get; }

    internal RunCoordinator Runs { get; }

    private nint WindowHandle => Win32Interop.GetWindowFromWindowId(AppWindow.Id);

    internal void ApplyTheme(ElementTheme theme)
    {
        Root.RequestedTheme = theme;
        AppWindow.TitleBar.PreferredTheme = theme switch
        {
            ElementTheme.Light => TitleBarTheme.Light,
            ElementTheme.Dark => TitleBarTheme.Dark,
            _ => TitleBarTheme.UseDefaultAppMode,
        };
    }

    internal void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }

    internal void ShowPage(UIElement page)
    {
        StatusBar.IsOpen = false;
        PageHost.Content = page;
    }

    /// <summary>Confirms what the run will do, then starts it and shows its progress.</summary>
    internal async Task StartRunAsync(RunKind kind, IReadOnlyList<string> toolIds)
    {
        if (Runs.IsBusy)
        {
            ShowRun();
            ShowStatus(InfoBarSeverity.Warning, "Refreshify is already running", "Wait for the current run to finish, or cancel it.");
            return;
        }

        var request = new RunRequest(kind, toolIds, AppSettings.ToolOptions, AppSettings.CreateRestorePoint, AppSettings.FixAutomatically);
        var plan = RunEngine.Plan(request);
        if (!await Dialogs.ConfirmRunAsync(request, plan))
            return;

        var run = Runs.RunAsync(request, plan);
        ShowRun();
        await run;
    }

    internal async Task RestartWindowsAsync()
    {
        if (await Dialogs.ConfirmAsync("Restart now?", "Save your work and close your apps first. Windows restarts right away.", "Restart now"))
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), "/r /t 0") { CreateNoWindow = true })?.Dispose();
    }

    /// <returns>The full path of the chosen Windows image (ISO), or <see langword="null"/>.</returns>
    internal async Task<string?> PickWindowsImageAsync()
    {
        var picker = new FileOpenPicker(AppWindow.Id)
        {
            CommitButtonText = "Repair from this image",
            SuggestedStartLocation = PickerLocationId.Downloads,
            FileTypeFilter = { ".iso" },
        };
        return (await picker.PickSingleFileAsync())?.Path;
    }

    private void ShowRun()
    {
        RunItem.Visibility = Visibility.Visible;
        if (ReferenceEquals(NavView.SelectedItem, RunItem))
            ShowPage(new RunView(this, Runs.Current!));
        else
            NavView.SelectedItem = RunItem;
    }

    private void ConfigureWindow()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Refreshify.ico"));
        AppWindow.Closing += OnWindowClosing;
        Closed += async (_, _) => await Runs.DisposeAsync();

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

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        UIElement? page = args.IsSettingsSelected ? new SettingsView(this) : args.SelectedItemContainer?.Tag switch
        {
            "Home" => new HomeView(this),
            "Run" => new RunView(this, Runs.Current!),
            "History" => new HistoryView(this),
            ToolCategory category => new CategoryView(this, CategoryInfo.Get(category)),
            _ => null,
        };
        if (page is not null)
            ShowPage(page);
    }

    private void OnPaneToggleRequested(TitleBar sender, object args) => NavView.IsPaneOpen = !NavView.IsPaneOpen;

    /// <summary>Closing during a run stops it after the current step, then closes.</summary>
    private async void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!Runs.IsBusy)
            return;

        args.Cancel = true;
        if (_closeAfterRun || !await Dialogs.ConfirmAsync(
            "Stop the run and close Refreshify?",
            "Refreshify stops after the current step. Steps that must not be interrupted, such as System file check, finish first.",
            "Stop and close"))
        {
            return;
        }

        _closeAfterRun = true;
        Runs.Cancel();
        ShowStatus(InfoBarSeverity.Informational, "Closing after the current step", "Refreshify closes when the current step finishes.");
    }

    private void OnRunFinished(object? sender, EventArgs e)
    {
        if (_closeAfterRun)
            Close();
    }

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint hwnd);
}
