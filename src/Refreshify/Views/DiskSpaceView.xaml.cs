using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Space;
using Refreshify.Models;

namespace Refreshify.Views;

/// <summary>Where the space under a folder went, as a tree the user can walk. Nothing is deleted from here.</summary>
public sealed partial class DiskSpaceView : UserControl
{
    private readonly MainWindow _window;
    private readonly SpaceLocation[] _locations = SpaceLocation.All();
    private readonly List<SpaceFolder> _path = [];

    private CancellationTokenSource? _scan;
    private SpaceScan? _result;
    private SpaceLocation? _location;
    private bool _largestFiles;

    public DiskSpaceView(MainWindow window)
    {
        _window = window;
        InitializeComponent();
        LocationBox.ItemsSource = _locations.Select(location => location.Title).ToArray();
        LocationBox.SelectedIndex = 0;
        // Leaving the page stops a scan that is still running.
        Unloaded += (_, _) => _scan?.Cancel();
    }

    private async void OnScanClick(object sender, RoutedEventArgs e)
    {
        var location = _locations[Math.Max(0, LocationBox.SelectedIndex)];
        var cancellation = _scan = new CancellationTokenSource();
        SetScanning(true);
        ProgressText.Text = Progress(0, 0);
        var progress = new Progress<SpaceProgress>(update => ProgressText.Text = Progress(update.Folders, update.Bytes));
        SpaceScan result;
        try
        {
            var walker = new WindowsFileSystemWalker();
            result = _result = await Task.Run(() => SpaceScanner.Scan(location.Path, walker, progress, cancellation.Token));
        }
        catch (Exception ex)
        {
            _window.ShowStatus(InfoBarSeverity.Error, "Couldn't scan that location", ex.Message);
            return;
        }
        finally
        {
            SetScanning(false);
            cancellation.Dispose();
            _scan = null;
        }

        _location = location;
        _path.Clear();
        _path.Add(result.Root);
        PartialText.Visibility = result.Partial ? Visibility.Visible : Visibility.Collapsed;
        Results.Visibility = Visibility.Visible;
        ShowCurrent();
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        StopButton.IsEnabled = false;
        _scan?.Cancel();
    }

    private void OnBreadcrumbClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Index >= _path.Count)
            return;
        _path.RemoveRange(args.Index + 1, _path.Count - args.Index - 1);
        ShowCurrent();
    }

    private void OnViewChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_result is null || sender.SelectedItem is null)
            return;
        _largestFiles = sender.Items.IndexOf(sender.SelectedItem) == 1;
        ShowRows();
    }

    private void OnRowTapped(object sender, TappedRoutedEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && Ancestor<Button>(source) is not null)
            return;
        var row = (SpaceRowItem)((FrameworkElement)sender).DataContext;
        if (row.IsFolder)
            Open(row);
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e) => Open((SpaceRowItem)((FrameworkElement)sender).DataContext);

    /// <summary>Opens a row in File Explorer: the folder itself, or the folder that holds a file, with it selected.</summary>
    private void OnOpenInExplorerClick(object sender, RoutedEventArgs e)
    {
        var row = (SpaceRowItem)((FrameworkElement)sender).DataContext;
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
        if (row.IsFolder)
        {
            start.ArgumentList.Add(row.Path);
        }
        else
        {
            start.ArgumentList.Add("/select,");
            start.ArgumentList.Add(row.Path);
        }

        Process.Start(start)?.Dispose();
    }

    private void Open(SpaceRowItem row)
    {
        if (_path[^1].Folders.FirstOrDefault(folder => folder.Path == row.Path) is not { } folder)
            return;
        _path.Add(folder);
        ShowCurrent();
    }

    private void SetScanning(bool scanning)
    {
        ProgressRow.Visibility = scanning ? Visibility.Visible : Visibility.Collapsed;
        ScanRing.IsActive = scanning;
        StopButton.IsEnabled = scanning;
        LocationBox.IsEnabled = !scanning;
        ScanButton.IsEnabled = !scanning;
        if (scanning)
            Results.Visibility = Visibility.Collapsed;
    }

    private static string Progress(long folders, long bytes) =>
        $"Scanning\u2026 {Format.Count(folders, "folder")}, {Format.Bytes(bytes)} so far";

    /// <summary>The breadcrumb, the rows and the notes, for the folder the user is in.</summary>
    private void ShowCurrent()
    {
        if (_result is not { } result || _location is not { } location || _path.Count == 0)
            return;

        List<string> crumbs = [location.Title, .. _path.Skip(1).Select(folder => folder.Name)];
        Breadcrumb.ItemsSource = crumbs;
        ShowRows();
        ShowNotes(result, location);
    }

    private void ShowRows()
    {
        var current = _path[^1];
        if (_largestFiles)
        {
            Rows.ItemsSource = current.LargestFiles
                .Select(file => new SpaceRowItem(Child(file), 0, Holder(file), showBar: false, showShare: false))
                .ToArray();
            RemainderText.Visibility = Visibility.Collapsed;
            return;
        }

        var page = current.Children();
        var largest = page.Rows.Count > 0 ? page.Rows[0].Size : 0;
        Rows.ItemsSource = page.Rows
            .Select(row => new SpaceRowItem(
                row, largest <= 0 ? 0 : 100.0 * row.Size / largest, Caption(row), showBar: true, showShare: true))
            .ToArray();
        RemainderText.Text = $"{Format.Count(page.RemainingCount, "smaller item")}, {Format.Bytes(page.RemainingSize)}";
        RemainderText.Visibility = page.RemainingCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowNotes(SpaceScan result, SpaceLocation location)
    {
        UnreadableExpander.Header = $"{Format.Count(result.UnreadableFolders, "folder")} couldn't be read.";
        UnreadableExpander.Visibility = result.UnreadableFolders > 0 ? Visibility.Visible : Visibility.Collapsed;
        UnreadableList.ItemsSource = result.UnreadablePaths;
        OnlineOnlyText.Visibility = result.OnlineOnlyFiles > 0 ? Visibility.Visible : Visibility.Collapsed;

        UnseenText.Visibility = Visibility.Collapsed;
        if (result.Partial || location.Drive is not { } drive)
            return;
        if (SpaceScanner.UnseenBytes(drive.TotalSize - drive.AvailableFreeSpace, result.Root.Size) is not { } unseen)
            return;
        UnseenText.Text = $"Files Refreshify could not see, such as other users' files and system files, take {Format.Bytes(unseen)}.";
        UnseenText.Visibility = Visibility.Visible;
    }

    private static SpaceChild Child(SpaceFile file) => new(file.Name, file.Path, file.Size, false, 0, 0);

    private static string Caption(SpaceChild row) => row.IsFolder ? Format.Count(row.FileCount, "file") : string.Empty;

    /// <summary>The folder a file in the largest files list is in, relative to the folder the user is in.</summary>
    private string Holder(SpaceFile file)
    {
        var current = _path[^1];
        if (Path.GetDirectoryName(file.Path) is not { } folder)
            return string.Empty;
        var relative = Path.GetRelativePath(current.Path, folder);
        return relative == "." ? current.Name : relative;
    }

    private static T? Ancestor<T>(DependencyObject source) where T : DependencyObject
    {
        for (DependencyObject? node = source; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is T match)
                return match;
        }

        return null;
    }
}
