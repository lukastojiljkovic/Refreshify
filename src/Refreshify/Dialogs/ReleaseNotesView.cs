using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Refreshify.Core.Updates;

namespace Refreshify.Dialogs;

/// <summary>
/// Renders parsed release notes: one section per group, one bulleted row per
/// item. Item markdown stays a small subset (bold, inline code, https links),
/// so no markdown package is involved.
/// </summary>
internal static class ReleaseNotesView
{
    private static readonly Regex Inline = new(
        @"`(?<code>[^`]+)`|\[(?<label>[^\]]+)\]\((?<url>https://[^)\s]+)\)|(?<bare>https://\S+)|(?<bold>\*\*(?<boldText>[^*]+)\*\*)",
        RegexOptions.Compiled);

    /// <summary>The notes as a spaced stack, or a single line when there are none.</summary>
    public static StackPanel Build(ReleaseNotes notes)
    {
        var panel = new StackPanel { Spacing = 20 };
        if (notes.IsEmpty)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "This release has no notes.",
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            });
            return panel;
        }

        foreach (var section in notes.Sections)
        {
            if (section.Items.Count > 0)
                panel.Children.Add(BuildSection(section));
        }
        return panel;
    }

    private static StackPanel BuildSection(ReleaseNotesSection section)
    {
        var group = new StackPanel { Spacing = 8 };
        if (section.Title.Length > 0)
        {
            group.Children.Add(new TextBlock
            {
                Text = section.Title,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            });
        }

        foreach (var item in section.Items)
        {
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition(),
                },
            };
            var text = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
            AddInlines(text.Inlines, item);
            Grid.SetColumn(text, 1);
            row.Children.Add(new TextBlock
            {
                Text = "\u2022",
                Style = (Style)Application.Current.Resources["ReleaseNotesBulletStyle"],
            });
            row.Children.Add(text);
            group.Children.Add(row);
        }
        return group;
    }

    /// <summary>
    /// Bold, inline code and https links become runs; everything else, including
    /// a label whose URL is not https, stays text.
    /// </summary>
    private static void AddInlines(InlineCollection inlines, string markdown)
    {
        var index = 0;
        foreach (Match match in Inline.Matches(markdown))
        {
            if (match.Index > index)
                inlines.Add(new Run { Text = markdown[index..match.Index] });

            if (match.Groups["code"].Success)
                inlines.Add(new Run { Text = match.Groups["code"].Value });
            else if (match.Groups["bold"].Success)
                inlines.Add(new Bold { Inlines = { new Run { Text = match.Groups["boldText"].Value } } });
            else
            {
                var url = match.Groups["url"].Success ? match.Groups["url"].Value : match.Groups["bare"].Value;
                var label = match.Groups["label"].Success ? match.Groups["label"].Value : url;
                var link = new Hyperlink();
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    link.NavigateUri = uri;
                link.Inlines.Add(new Run { Text = label });
                inlines.Add(link);
            }
            index = match.Index + match.Length;
        }
        if (index < markdown.Length)
            inlines.Add(new Run { Text = markdown[index..] });
    }
}
