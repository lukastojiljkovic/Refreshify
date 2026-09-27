using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Refreshify.Core.Catalog;
using Refreshify.Core.Engine;
using Refreshify.Models;

namespace Refreshify.Views;

public sealed partial class CategoryView : UserControl
{
    private readonly MainWindow _window;

    public CategoryView(MainWindow window, CategoryInfo category)
    {
        _window = window;
        Category = category;
        Tools = [.. ToolCatalog.All.Where(tool => tool.Info.Category == category.Category && !tool.Info.Hidden).Select(tool => new ToolItem(tool.Info))];
        InitializeComponent();
        UpdateRunSelected();
    }

    public CategoryInfo Category { get; }

    public IReadOnlyList<ToolItem> Tools { get; }

    private void UpdateRunSelected() => RunSelectedButton.IsEnabled = Tools.Any(tool => tool.IsInRunAll);

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
