using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Refreshify.Models;

namespace Refreshify.Views;

public sealed partial class HistoryView : UserControl
{
    private readonly MainWindow _window;

    public HistoryView(MainWindow window)
    {
        _window = window;
        Entries = [.. window.Runs.History.Load().Select(record => new HistoryEntry(record))];
        InitializeComponent();
        EmptyText.Visibility = Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Header.Visibility = Entries.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    public IReadOnlyList<HistoryEntry> Entries { get; }

    private void OnEntryClick(object sender, RoutedEventArgs e)
    {
        var entry = (HistoryEntry)((FrameworkElement)sender).DataContext;
        _window.ShowPage(new RunView(_window, new RunModel(entry.Record, isLive: false), () => _window.ShowPage(new HistoryView(_window))));
    }
}
