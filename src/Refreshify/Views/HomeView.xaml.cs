using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Refreshify.Core.Catalog;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Engine;
using Refreshify.Core.Platform;
using Refreshify.Models;
using Refreshify.Services;

namespace Refreshify.Views;

public sealed partial class HomeView : UserControl
{
    private readonly MainWindow _window;
    private readonly List<string> _runAll;

    public HomeView(MainWindow window)
    {
        _window = window;
        InitializeComponent();

        _runAll = [.. ToolCatalog.All.Where(tool => !tool.Info.Hidden && AppSettings.IsInRunAll(tool.Info)).Select(tool => tool.Info.Id)];
        RunAllButton.IsEnabled = _runAll.Count > 0;
        RunAllText.Text = _runAll.Count > 0
            ? $"Cleans up, repairs and updates your PC with the {Format.Count(_runAll.Count, "tool")} included in Run all. Choose them on each category page."
            : "No tools are included in Run all. Include some on the category pages.";

        LastRefreshCard.Description = AppSettings.LastFullRefresh is { } last ? RunModel.When(last) : "Never";

        var drive = new DriveInfo(SystemState.SystemDrive);
        SpaceCard.Header = $"Free space on {drive.Name.TrimEnd('\\')}";
        SpaceCard.Description = $"{Format.Bytes(drive.AvailableFreeSpace)} free of {Format.Bytes(drive.TotalSize)}";
        SpaceBar.Value = 100.0 * (drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize;

        var restartPending = SystemState.IsRestartPending;
        RestartCard.Description = restartPending
            ? "Windows is waiting for a restart to finish installing or repairing something."
            : "No restart is pending.";
        RestartButton.Visibility = restartPending ? Visibility.Visible : Visibility.Collapsed;

        var system = SystemInfo.Current(App.Version);
        WindowsCard.Description = $"{system.Edition}, version {system.Version}, build {system.Build}";
    }

    private async void OnRunAllClick(object sender, RoutedEventArgs e) => await _window.StartRunAsync(RunKind.All, _runAll);

    private async void OnRestartClick(object sender, RoutedEventArgs e) => await _window.RestartWindowsAsync();
}
