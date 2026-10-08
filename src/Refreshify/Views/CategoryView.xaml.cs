using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Refreshify.Core.Catalog;
using Refreshify.Core.Engine;
using Refreshify.Models;

namespace Refreshify.Views;

public sealed partial class CategoryView : UserControl
{
    private readonly MainWindow _window;

    /// <param name="focusToolId">A tool to bring into view, when the page is opened from somewhere that points at one.</param>
    public CategoryView(MainWindow window, CategoryInfo category, string? focusToolId = null)
    {
        _window = window;
        Category = category;
        Tools = [.. ToolCatalog.All.Where(tool => tool.Info.Category == category.Category && !tool.Info.Hidden).Select(tool => new ToolItem(tool.Info))];
        InitializeComponent();
        UpdateRunSelected();
        if (focusToolId is not null)
            FocusTool(focusToolId);
    }

    public CategoryInfo Category { get; }

    public IReadOnlyList<ToolItem> Tools { get; }

    private void UpdateRunSelected() => RunSelectedButton.IsEnabled = Tools.Any(tool => tool.IsInRunAll);

    /// <summary>Scrolls to the tool and brings it into view, once the list has laid out.</summary>
    private void FocusTool(string toolId)
    {
        var index = Tools.ToList().FindIndex(tool => tool.Info.Id == toolId);
        if (index < 0)
            return;

        ToolRepeater.Loaded += (_, _) => DispatcherQueue.TryEnqueue(() => ToolRepeater.GetOrCreateElement(index)?.StartBringIntoView());
    }

    private void OnIncludeClick(object sender, RoutedEventArgs e)
    {
        var box = (CheckBox)sender;
        ((ToolItem)box.DataContext).IsInRunAll = box.IsChecked == true;
        UpdateRunSelected();
    }

    private async void OnRunClick(object sender, RoutedEventArgs e) =>
        await _window.StartRunAsync(RunKind.Tool, [((ToolItem)((FrameworkElement)sender).DataContext).Info.Id]);

    private async void OnRunSelectedClick(object sender, RoutedEventArgs e) =>
        await _window.StartRunAsync(RunKind.Category, [.. Tools.Where(tool => tool.IsInRunAll).Select(tool => tool.Info.Id)]);
}
