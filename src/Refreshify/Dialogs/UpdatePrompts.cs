using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Refreshify.Core.Updates;

namespace Refreshify.Dialogs;

/// <summary>The update dialogs the window shows: what's new, and the release page fallback.</summary>
internal static class UpdatePrompts
{
    /// <summary>Renders a release's notes with a link to its page.</summary>
    public static async Task ShowReleaseNotesAsync(DialogService dialogs, ReleaseInfo release)
    {
        var scroll = new ScrollViewer
        {
            Content = MarkdownText.Build(release.Body),
            MaxHeight = 360,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var dialog = new ContentDialog
        {
            Title = $"What's new in Refreshify {release.Version.ToString(3)}",
            Content = scroll,
            PrimaryButtonText = "Close",
            SecondaryButtonText = "Open the release page",
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.Resources["ContentDialogMaxWidth"] = 760.0;
        if (await dialogs.ShowAsync(dialog) == ContentDialogResult.Secondary)
            await OpenAsync(release.PageUrl);
    }

    /// <summary>Tells the user why the update stopped and offers the release page.</summary>
    public static async Task ShowUpdateFailureAsync(DialogService dialogs, string message, string? releasePageUrl)
    {
        var dialog = new ContentDialog
        {
            Title = "The update could not be installed",
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "Close",
            SecondaryButtonText = releasePageUrl is null ? string.Empty : "Open the release page",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialogs.ShowAsync(dialog) == ContentDialogResult.Secondary && releasePageUrl is not null)
            await OpenAsync(releasePageUrl);
    }

    public static async Task OpenAsync(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            await Windows.System.Launcher.LaunchUriAsync(uri);
    }
}
