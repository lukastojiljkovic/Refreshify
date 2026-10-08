using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using Refreshify.Core.Engine;
using Refreshify.Core.Tools;
using Refreshify.Core.Updates;
using Refreshify.Dialogs;
using Refreshify.Models;
using Refreshify.Services;
using Refreshify.Views;
using Windows.Graphics;

namespace Refreshify;

public sealed partial class MainWindow : Window
{
    private bool _closeAfterRun;
    private ReleaseInfo? _availableRelease;
    private CancellationTokenSource? _updateDownload;

    public MainWindow()
    {
        InitializeComponent();
        Dialogs = new DialogService(Root);
        Runs = new RunCoordinator(Dialogs.RestorePointFailedAsync);
        Runs.Finished += OnRunFinished;
        Updates = new UpdateCoordinator();
        Updates.Checked += OnUpdateChecked;
        ConfigureWindow();
        ApplyTheme(AppSettings.Theme);

        var index = NavView.MenuItems.IndexOf(CategoriesEnd);
        foreach (var category in CategoryInfo.All)
            NavView.MenuItems.Insert(index++, new NavigationViewItem { Content = category.Name, Tag = category.Category, Icon = new FontIcon { Glyph = category.Glyph } });
        NavView.SelectedItem = HomeItem;

        Root.Loaded += async (_, _) =>
        {
            // Read before the check starts: the check writes LastUpdateCheckUtc.
            var previous = AppSettings.LastRunVersion;
            var ranBefore = previous is not null || AppSettings.LastUpdateCheckUtc is not null;

            // The check runs in the background; the window is never delayed.
            _ = Updates.CheckOnStartupAsync();

            // Recorded before any dialog opens, so closing the window over one
            // cannot make the next launch report an update that did not happen.
            var current = AppVersion.Current;
            AppSettings.LastRunVersion = current.ToString(3);
            if (PostUpdateNotes(ranBefore, previous, current) is { } notes)
                await UpdatePrompts.ShowInstalledNotesAsync(Dialogs, current, notes);
            else if (AppSettings.ShowWelcome)
            {
                var welcome = new WelcomeDialog();
                await Dialogs.ShowAsync(welcome);
                if (welcome.DontShowAgain)
                    AppSettings.ShowWelcome = false;
            }
        };
    }

    internal DialogService Dialogs { get; }

    internal RunCoordinator Runs { get; }

    internal UpdateCoordinator Updates { get; }

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

    /// <summary>For a second start of Refreshify, such as a click on the reminder.</summary>
    internal void BringToFront()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            presenter.Restore();
        Activate();
    }

    internal void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.Content = null;
        StatusBar.IsOpen = true;
    }

    internal void ShowPage(UIElement page)
    {
        StatusBar.IsOpen = false;
        // As wide as the page's column: 1400 on Run and History, 1000 elsewhere, less the padding.
        StatusBar.MaxWidth = page is RunView or HistoryView ? 1336 : 936;
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
        RefreshUpdateActions();
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
            "Space" => new DiskSpaceView(this),
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
        RefreshUpdateActions();
        if (_closeAfterRun)
            Close();
    }

    /// <summary>The startup check's result. Only a newer version opens the bar.</summary>
    private void OnUpdateChecked(object? sender, UpdateCheckResult result)
    {
        if (result is { Status: UpdateCheckStatus.UpdateAvailable, Release: { } release })
            ShowUpdateAvailable(release);
    }

    internal void ShowUpdateAvailable(ReleaseInfo release)
    {
        _availableRelease = release;
        UpdateBar.Title = $"Refreshify {release.Version.ToString(3)} is available";
        UpdateBar.Message = $"You have Refreshify {Updates.CurrentVersion.ToString(3)}.";
        UpdateBar.IsOpen = true;
        RefreshUpdateActions();
    }

    /// <summary>An update cannot start while a run is in progress.</summary>
    private void RefreshUpdateActions() =>
        UpdateInstallButton.IsEnabled = !Runs.IsBusy && _updateDownload is null;

    private void OnUpdateBarClosed(InfoBar sender, object args) => _availableRelease = null;

    private async void OnUpdateNotesClick(object sender, RoutedEventArgs e)
    {
        if (_availableRelease is { } release)
        {
            // The same condition that enables the bar's Update button, so the dialog
            // can offer the update only when the button would.
            if (await UpdatePrompts.ShowReleaseNotesAsync(Dialogs, release, !Runs.IsBusy && _updateDownload is null))
                await DownloadUpdateAsync(release);
        }
    }

    /// <summary>
    /// The notes to show after an update: the section for
    /// <paramref name="current"/> in the embedded CHANGELOG, when this launch
    /// follows an update from another version. Null when nothing should be shown.
    /// </summary>
    private static ReleaseNotes? PostUpdateNotes(bool ranBefore, string? previous, Version current)
    {
        var notes = VersionNotes(current);
        if (notes is null)
            return null;
#if DEBUG
        // Forces the notes for the real version to be shown, since the test
        // version override changes only what the check compares against.
        if (Environment.GetEnvironmentVariable("REFRESHIFY_SHOW_UPDATED_NOTES") == "1")
            return notes;
#endif
        if (!ranBefore)
            return null;
        if (previous is null)
            return notes;
        return Version.TryParse(previous, out var parsed) && parsed >= current ? null : notes;
    }

    /// <summary>The current version's section of the embedded CHANGELOG, when it has items.</summary>
    private static ReleaseNotes? VersionNotes(Version version)
    {
        string changelog;
        using (var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("CHANGELOG.md"))
        {
            if (stream is null)
                return null;
            using var reader = new StreamReader(stream);
            changelog = reader.ReadToEnd();
        }

        var notes = ReleaseNotes.FromChangelog(changelog, version);
        return notes is null || notes.IsEmpty ? null : notes;
    }

    private async void OnUpdateInstallClick(object sender, RoutedEventArgs e)
    {
        if (_availableRelease is { } release)
            await DownloadUpdateAsync(release);
    }

    /// <summary>
    /// Downloads the release's installer, verifies it against its checksum,
    /// then starts it and closes the window. Progress and cancel use the status
    /// bar, so no modal has to be dismissed when the work finishes.
    /// </summary>
    private async Task DownloadUpdateAsync(ReleaseInfo release)
    {
        if (Runs.IsBusy)
        {
            ShowStatus(InfoBarSeverity.Warning, "Refreshify is busy", "Wait for the current run to finish before updating.");
            return;
        }
        if (_updateDownload is not null)
            return;

        using var cancellation = new CancellationTokenSource();
        _updateDownload = cancellation;
        RefreshUpdateActions();

        var progressBar = new ProgressBar { Width = 220, IsIndeterminate = true, VerticalAlignment = VerticalAlignment.Center };
        var percent = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Text = "0%" };
        var cancel = new Button { Content = "Cancel" };
        cancel.Click += (_, _) => cancellation.Cancel();
        StatusBar.Severity = InfoBarSeverity.Informational;
        StatusBar.Title = $"Downloading Refreshify {release.Version.ToString(3)}\u2026";
        StatusBar.Message = "Refreshify verifies the installer before it runs it.";
        StatusBar.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { progressBar, percent, cancel },
        };
        StatusBar.IsOpen = true;

        var progress = new Progress<UpdateProgress>(update =>
        {
            if (update.TotalBytes is not > 0)
                return;
            progressBar.IsIndeterminate = false;
            progressBar.Value = Math.Clamp(100.0 * update.BytesReceived / update.TotalBytes.Value, 0, 100);
            percent.Text = $"{progressBar.Value:0}%";
        });

        UpdateDownloadResult result;
        try
        {
            result = await Updates.Service.DownloadAsync(release, cancellation.Token, progress);
        }
        catch (OperationCanceledException)
        {
            ShowStatus(InfoBarSeverity.Informational, "Update cancelled", "Refreshify is unchanged.");
            return;
        }
        finally
        {
            _updateDownload = null;
            StatusBar.Content = null;
            RefreshUpdateActions();
        }

        StatusBar.IsOpen = false;
        if (!result.Success)
        {
            await UpdatePrompts.ShowUpdateFailureAsync(Dialogs, result.Error ?? "The installer could not be downloaded.", result.ReleasePageUrl);
            return;
        }

        switch (await Updates.Service.InstallAsync(result, CancellationToken.None))
        {
            case InstallOutcome.Started:
                Close();
                break;
            case InstallOutcome.Cancelled:
                ShowStatus(InfoBarSeverity.Informational, "Update cancelled", "Windows did not get permission to run the installer.");
                break;
            default:
                await UpdatePrompts.ShowUpdateFailureAsync(Dialogs, "The installer could not be started.", release.PageUrl);
                break;
        }
    }

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint hwnd);
}
