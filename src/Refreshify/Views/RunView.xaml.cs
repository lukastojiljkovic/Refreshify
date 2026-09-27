using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Refreshify.Core.Diagnostics;
using Refreshify.Models;
using Refreshify.Services;

namespace Refreshify.Views;

/// <summary>The steps of a run with their live progress and results, and what can be done about failures.</summary>
public sealed partial class RunView : UserControl
{
    private readonly MainWindow _window;
    private readonly Action? _back;

    /// <param name="back">For a past run: goes back to the History list.</param>
    public RunView(MainWindow window, RunModel model, Action? back = null)
    {
        _window = window;
        _back = back;
        Model = model;
        InitializeComponent();

        if (back is not null)
        {
            Breadcrumb.ItemsSource = new[] { "History", model.Title };
            Breadcrumb.Visibility = Visibility.Visible;
        }
    }

    public RunModel Model { get; }

    private static StepItem Step(object sender) => (StepItem)((FrameworkElement)sender).DataContext;

    private void OnCancelClick(object sender, RoutedEventArgs e) => _window.Runs.Cancel();

    private async void OnFixClick(object sender, RoutedEventArgs e)
    {
        var step = Step(sender);
        var options = AppSettings.ToolOptions;
        if (step.Record.Issue?.Remedy == RemedyKind.WindowsImage)
        {
            if (await _window.PickWindowsImageAsync() is not { } image)
                return;
            options = options with { WindowsImagePath = image };
        }

        await _window.Runs.FixAsync(step.Index, options);
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e) => await _window.Runs.FixAsync(Step(sender).Index, AppSettings.ToolOptions);

    private async void OnRestartClick(object sender, RoutedEventArgs e) => await _window.RestartWindowsAsync();

    private async void OnHelpClick(object sender, RoutedEventArgs e) => await _window.Dialogs.ShowReportAsync(Model.Record, Step(sender).Index);

    private void OnBreadcrumbClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Index == 0)
            _back?.Invoke();
    }
}
