using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Refreshify.Core.Updates;
using Refreshify.Services;

namespace Refreshify.Views;

public sealed partial class SettingsView : UserControl
{
    /// <summary>The ages the temporary files box offers, in hours and in its order. Zero deletes them all.</summary>
    private static readonly int[] TempFileAges = [24, 168, 720, 0];

    private readonly MainWindow _window;

    public SettingsView(MainWindow window)
    {
        _window = window;
        InitializeComponent();

        RestorePointToggle.IsOn = AppSettings.CreateRestorePoint;
        FixToggle.IsOn = AppSettings.FixAutomatically;
        TempAgeBox.SelectedIndex = Math.Max(0, Array.IndexOf(TempFileAges, AppSettings.TempFileAgeHours));
        TechnicalToggle.IsOn = AppSettings.ShowTechnicalDetails;
        PrivacyToggle.IsOn = AppSettings.HidePersonalDetails;
        ReminderBox.SelectedIndex = Array.IndexOf(AppSettings.ReminderIntervals, AppSettings.ReminderMonths);
        ThemeBox.SelectedIndex = (int)AppSettings.Theme;
        WelcomeToggle.IsOn = AppSettings.ShowWelcome;
        AutoUpdateToggle.IsOn = AppSettings.CheckForUpdatesAutomatically;
        UpdateStatusText.Text = $"Version {App.Version}";
        AboutCard.Description = $"Version {App.Version} · MIT License · © 2026 Luka Stojiljkovic";
    }

    private void OnRestorePointToggled(object sender, RoutedEventArgs e) => AppSettings.CreateRestorePoint = RestorePointToggle.IsOn;

    private void OnFixToggled(object sender, RoutedEventArgs e) => AppSettings.FixAutomatically = FixToggle.IsOn;

    private void OnTempAgeChanged(object sender, SelectionChangedEventArgs e) => AppSettings.TempFileAgeHours = TempFileAges[TempAgeBox.SelectedIndex];

    private void OnTechnicalToggled(object sender, RoutedEventArgs e) => AppSettings.ShowTechnicalDetails = TechnicalToggle.IsOn;

    private void OnPrivacyToggled(object sender, RoutedEventArgs e) => AppSettings.HidePersonalDetails = PrivacyToggle.IsOn;

    private async void OnReminderChanged(object sender, SelectionChangedEventArgs e)
    {
        var months = AppSettings.ReminderIntervals[ReminderBox.SelectedIndex];
        if (months == AppSettings.ReminderMonths)
            return;

        ReminderBox.IsEnabled = false;
        var scheduled = await Reminder.ScheduleAsync(months);
        ReminderBox.IsEnabled = true;
        if (scheduled)
        {
            AppSettings.ReminderMonths = months;
            return;
        }

        ReminderBox.SelectedIndex = Array.IndexOf(AppSettings.ReminderIntervals, AppSettings.ReminderMonths);
        _window.ShowStatus(InfoBarSeverity.Error, "Couldn't turn on the reminder", "Windows Task Scheduler didn't accept the daily check. Try again later.");
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        AppSettings.Theme = (ElementTheme)ThemeBox.SelectedIndex;
        _window.ApplyTheme(AppSettings.Theme);
    }

    private void OnWelcomeToggled(object sender, RoutedEventArgs e) => AppSettings.ShowWelcome = WelcomeToggle.IsOn;

    private void OnAutoUpdateToggled(object sender, RoutedEventArgs e) =>
        AppSettings.CheckForUpdatesAutomatically = AutoUpdateToggle.IsOn;

    private async void OnCheckNowClick(object sender, RoutedEventArgs e)
    {
        CheckNowButton.IsEnabled = false;
        UpdateStatusText.Text = "Checking…";
        try
        {
            var result = await _window.Updates.Service.CheckAsync(manual: true);
            UpdateStatusText.Text = result.Status switch
            {
                UpdateCheckStatus.UpToDate => "Refreshify is up to date.",
                UpdateCheckStatus.UpdateAvailable => $"Refreshify {result.Release!.Version.ToString(3)} is available.",
                UpdateCheckStatus.NoReleases => "No releases have been published yet.",
                UpdateCheckStatus.RateLimited => "GitHub's rate limit was reached. Try again later.",
                _ => $"Could not check for updates: {result.Detail}",
            };
            if (result is { Status: UpdateCheckStatus.UpdateAvailable, Release: { } release })
                _window.ShowUpdateAvailable(release);
        }
        finally
        {
            CheckNowButton.IsEnabled = true;
        }
    }

    private void OnOpenDataClick(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(RunCoordinator.DataDirectory);
        Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"))
        {
            ArgumentList = { RunCoordinator.DataDirectory },
        })?.Dispose();
    }

    private async void OnClearHistoryClick(object sender, RoutedEventArgs e)
    {
        if (!await _window.Dialogs.ConfirmAsync(
            "Clear history and logs?", "The results and output of past runs will be deleted. Your settings stay as they are.", "Clear"))
        {
            return;
        }

        _window.Runs.ClearHistory();
        _window.ShowStatus(InfoBarSeverity.Success, "History cleared", "The results and output of past runs are deleted.");
    }
}
