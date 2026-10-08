using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Refreshify.Core.Updates;

namespace Refreshify.Dialogs;

/// <summary>The update dialogs the window shows: what's new, what was installed, and failures.</summary>
internal static class UpdatePrompts
{
    /// <summary>The release tag page, for a version the CHANGELOG describes but the app did not fetch.</summary>
    private const string ReleaseTagUrl = "https://github.com/lukastojiljkovic/Refreshify/releases/tag/v";

    /// <summary>
    /// Shows a release's notes before updating, with the release page as the
    /// fallback for the technical text. Returns <see langword="true"/> when the
    /// user chose to update now.
    /// </summary>
    public static async Task<bool> ShowReleaseNotesAsync(DialogService dialogs, ReleaseInfo release, bool canUpdate)
    {
        var dialog = new ContentDialog
        {
            Title = $"What's new in Refreshify {release.Version.ToString(3)}",
            Content = NotesContent(release.PublishedAt, ReleaseNotes.FromReleaseBody(release.Body), release.PageUrl),
            PrimaryButtonText = "Update now",
            IsPrimaryButtonEnabled = canUpdate,
            CloseButtonText = "Later",
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.Resources["ContentDialogMaxWidth"] = 640.0;
        return await dialogs.ShowAsync(dialog) == ContentDialogResult.Primary;
    }

    /// <summary>Shows the notes of the version the app just updated to.</summary>
    public static async Task ShowInstalledNotesAsync(DialogService dialogs, Version version, ReleaseNotes notes)
    {
        var dialog = new ContentDialog
        {
            Title = $"Refreshify was updated to {version.ToString(3)}",
            Content = NotesContent(null, notes, ReleaseTagUrl + version.ToString(3)),
            CloseButtonText = "Got it",
        };
        dialog.Resources["ContentDialogMaxWidth"] = 640.0;
        await dialogs.ShowAsync(dialog);
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

    /// <summary>The notes in a scroll viewer: the release date, the sections, and one link out.</summary>
    private static ScrollViewer NotesContent(DateTimeOffset? publishedAt, ReleaseNotes notes, string pageUrl)
    {
        var stack = new StackPanel();
        if (publishedAt is { } published)
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"Released on {published.LocalDateTime.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}",
                Style = (Style)Application.Current.Resources["ReleaseNotesDateStyle"],
            });
        }
        stack.Children.Add(ReleaseNotesView.Build(notes));
        stack.Children.Add(new HyperlinkButton
        {
            Content = "See the full release notes on GitHub",
            NavigateUri = Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri) ? uri : null,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 20, 0, 0),
        });
        return new ScrollViewer
        {
            Content = stack,
            MaxHeight = 420,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }
}
