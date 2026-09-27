using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Engine;
using Refreshify.Core.Tools;
using Refreshify.Models;
using Refreshify.Services;
using Windows.ApplicationModel.DataTransfer;

namespace Refreshify.Dialogs;

/// <summary>The app's dialogs, shown one at a time over the window's content.</summary>
internal sealed class DialogService(FrameworkElement root)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Waits for any open dialog first, since only one ContentDialog can be open at a time.</summary>
    public async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        await _gate.WaitAsync();
        try
        {
            dialog.XamlRoot = root.XamlRoot;
            dialog.RequestedTheme = root.ActualTheme;
            dialog.Style ??= (Style)Application.Current.Resources["DefaultContentDialogStyle"];
            return await dialog.ShowAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> ConfirmAsync(string title, string message, string action) =>
        await ShowAsync(new ContentDialog
        {
            Title = title,
            Content = Paragraph(message),
            PrimaryButtonText = action,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        }) == ContentDialogResult.Primary;

    /// <summary>Lists what will happen before a run starts.</summary>
    public async Task<bool> ConfirmRunAsync(RunRequest request, IReadOnlyList<Tool> plan)
    {
        var tools = plan.Where(tool => !tool.Info.Hidden).ToList();
        bool Any(ToolTraits trait) => tools.Any(tool => tool.Info.Has(trait));

        var points = new StackPanel { Spacing = 14 };
        if (tools is [var only])
            Add(only.Info.Glyph, only.Info.Description);
        else
            Add(Glyphs.Run, $"{tools.Count} tools run, one after another.");
        if (tools.Count < plan.Count)
            Add(Glyphs.RestorePoint, "A restore point is created first, so you can undo the changes with System Restore.");
        if (tools.Any(tool => tool.Info.RunAs == RunAs.Administrator))
            Add(Glyphs.Administrator, "Windows asks you to let Refreshify make changes, once per session.");
        if (Any(ToolTraits.RestartsExplorer))
            Add(Glyphs.Explorer, "File Explorer restarts, so its windows close for a moment.");
        if (Any(ToolTraits.MayCloseApps))
            Add(Glyphs.Apps, "Apps that are open may close while they update. Save your work first.");
        if (Any(ToolTraits.LongRunning))
            Add(Glyphs.Time, "Some tools take a while. You can keep using your PC in the meantime.");
        if (Any(ToolTraits.RestartRequired))
            Add(Glyphs.Restart, "Some changes finish after a restart. Refreshify never restarts your PC without asking.");

        var name = RunModel.TitleOf(new RunRecord(string.Empty, request.Kind, DateTimeOffset.Now,
            [.. plan.Select(tool => new StepRecord(tool.Info.Id, tool.Info.Name))]));
        return await ShowAsync(new ContentDialog
        {
            Title = request.Kind == RunKind.All ? "Run all?" : $"Run {name}?",
            Content = points,
            PrimaryButtonText = "Run",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        }) == ContentDialogResult.Primary;

        void Add(string glyph, string text)
        {
            var row = new Grid { ColumnSpacing = 14, ColumnDefinitions = { new() { Width = GridLength.Auto }, new() } };
            var icon = new FontIcon { Glyph = glyph, Style = (Style)Application.Current.Resources["AccentGlyphStyle"] };
            var label = Paragraph(text);
            Grid.SetColumn(label, 1);
            row.Children.Add(icon);
            row.Children.Add(label);
            points.Children.Add(row);
        }
    }

    /// <summary>The restore point is the safety net for everything after it, so its failure is decided right away.</summary>
    public async Task<RestorePointChoice> RestorePointFailedAsync(StepRecord step, bool canFix)
    {
        var explanation = step.Issue?.Explanation ?? $"{step.Result?.Summary} Without a restore point, System Restore can't undo this run's changes.";
        return await ShowAsync(new ContentDialog
        {
            Title = "Couldn't create a restore point",
            Content = Paragraph(explanation),
            PrimaryButtonText = canFix ? step.Issue?.FixLabel ?? "Fix it" : string.Empty,
            SecondaryButtonText = "Continue without it",
            CloseButtonText = "Stop the run",
            DefaultButton = canFix ? ContentDialogButton.Primary : ContentDialogButton.Close,
        }) switch
        {
            ContentDialogResult.Primary => RestorePointChoice.Fix,
            ContentDialogResult.Secondary => RestorePointChoice.Continue,
            _ => RestorePointChoice.Cancel,
        };
    }

    /// <summary><b>Get help</b>: a report to paste into an AI assistant. Nothing is sent anywhere.</summary>
    public async Task ShowReportAsync(RunRecord run, int index)
    {
        var system = SystemInfo.Current(App.Version);
        var report = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 320,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 12,
        };
        ScrollViewer.SetVerticalScrollBarVisibility(report, ScrollBarVisibility.Auto);
        // Copied from here rather than from the TextBox, which turns every line break into a bare CR.
        var text = string.Empty;
        var hide = new CheckBox { Content = "Hide personal details", IsChecked = AppSettings.HidePersonalDetails };
        hide.Click += (_, _) =>
        {
            AppSettings.HidePersonalDetails = hide.IsChecked == true;
            Build();
        };
        Build();

        var dialog = new ContentDialog
        {
            Title = "Get help with this step",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    Paragraph("Refreshify can't fix this on its own. Copy this report and paste it into an AI assistant, such as Copilot, ChatGPT or Claude. It describes your PC and what went wrong, so the assistant can help you step by step. Refreshify doesn't send it anywhere."),
                    report,
                    hide,
                },
            },
            PrimaryButtonText = "Copy report",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.Resources["ContentDialogMaxWidth"] = 760.0;
        dialog.PrimaryButtonClick += (sender, args) =>
        {
            args.Cancel = true;
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            sender.PrimaryButtonText = "Copied";
        };
        await ShowAsync(dialog);

        void Build() => report.Text = text = ReportBuilder.Build(run, index, system, hide.IsChecked == true ? Redactor.Current : null);
    }

    private static TextBlock Paragraph(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
}
