using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Refreshify.Core.Health;
using Refreshify.Models;

namespace Refreshify.Views;

/// <summary>What the PC looks like right now. Every check runs on its own and nothing is changed.</summary>
public sealed partial class HealthView : UserControl
{
    private readonly MainWindow _window;

    public HealthView(MainWindow window)
    {
        _window = window;
        Items = [.. window.Health.Checks.Select(check => new HealthItem(check))];
        InitializeComponent();
        _ = RunAsync();
    }

    public IReadOnlyList<HealthItem> Items { get; }

    private async void OnCheckAgainClick(object sender, RoutedEventArgs e) => await RunAsync();

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is HealthItem { Action: { } action })
            _window.ShowCategory(action.Category, action.ToolId);
    }

    private async Task RunAsync()
    {
        CheckAgainButton.IsEnabled = false;
        foreach (var item in Items)
            item.Restart();
        var runs = _window.Health.RunAll();
        await Task.WhenAll(Items.Select((item, index) => FinishAsync(item, runs[index])));
        CheckAgainButton.IsEnabled = true;
    }

    private static async Task FinishAsync(HealthItem item, Task<HealthResult> run) => item.Finish(await run);
}
