using System.Text;
using System.Text.RegularExpressions;

namespace Refreshify.Core.Updates;

/// <summary>One group of a release's notes, such as "New" or "Fixed".</summary>
public sealed record ReleaseNotesSection(string Title, IReadOnlyList<string> Items);

/// <summary>
/// The part of a release's notes an end user needs: the sections a GitHub
/// release body or the CHANGELOG describes, with everything technical removed.
/// Items keep their inline markdown; the renderer decides how to show it.
/// </summary>
public sealed record ReleaseNotes(IReadOnlyList<ReleaseNotesSection> Sections)
{
    private const string WhatsNewHeading = "## What's new";

    /// <summary>HTML comments carry notes for maintainers; they are never shown.</summary>
    private static readonly Regex Comment = new(@"<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>A Keep a Changelog link reference, which ends a version's section.</summary>
    private static readonly Regex LinkReference = new(@"^\[[^\]]+\]:\s*https?://", RegexOptions.Compiled);

    /// <summary>True when no section has an item.</summary>
    public bool IsEmpty => Sections.All(section => section.Items.Count == 0);

    /// <summary>
    /// Parses a GitHub release body: the lines under <c>## What's new</c>, or
    /// the whole body when it has no such heading. Unreadable input comes back
    /// as empty notes, never as an exception.
    /// </summary>
    public static ReleaseNotes FromReleaseBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return new ReleaseNotes([]);

        var lines = Lines(body);
        var start = -1;
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].Trim().Equals(WhatsNewHeading, StringComparison.OrdinalIgnoreCase))
            {
                start = index + 1;
                break;
            }
        }

        IEnumerable<string> block = start < 0 ? lines : Until(lines, start, IsReleaseHeading);
        return Parse(block);
    }

    /// <summary>
    /// Parses the section of a Keep a Changelog file for
    /// <paramref name="version"/>. <see langword="null"/> when the version has
    /// no section; <c>[Unreleased]</c> never matches.
    /// </summary>
    public static ReleaseNotes? FromChangelog(string changelog, Version version)
    {
        var heading = $"## [{version.ToString(3)}]";
        var lines = Lines(changelog);
        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].StartsWith(heading, StringComparison.Ordinal))
                continue;

            var rest = lines[index][heading.Length..];
            if (rest.Length > 0 && !rest.StartsWith(" - ", StringComparison.Ordinal) && !rest.StartsWith(" — ", StringComparison.Ordinal))
                continue;

            return Parse(Until(lines, index + 1, line => IsReleaseHeading(line) || LinkReference.IsMatch(line)));
        }

        return null;
    }

    /// <summary>The lines, with CRLF normalised and maintainer-only comments dropped.</summary>
    private static string[] Lines(string text) =>
        Comment.Replace(text.Replace("\r\n", "\n", StringComparison.Ordinal), string.Empty).Split('\n');

    private static IEnumerable<string> Until(string[] lines, int start, Func<string, bool> isEnd)
    {
        for (var index = start; index < lines.Length; index++)
        {
            if (isEnd(lines[index]))
                yield break;
            yield return lines[index];
        }
    }

    private static bool IsReleaseHeading(string line) => line.StartsWith("## ", StringComparison.Ordinal);

    /// <summary>
    /// The shared block parser: <c>### Title</c> starts a section, bullets and
    /// paragraphs become items, and indented lines continue the item above.
    /// Sections without items are dropped and their order is kept.
    /// </summary>
    private static ReleaseNotes Parse(IEnumerable<string> block)
    {
        var sections = new List<ReleaseNotesSection>();
        var items = new List<string>();
        var pending = new StringBuilder();
        var title = string.Empty;
        var kind = ItemKind.None;

        foreach (var raw in block)
        {
            var line = raw.TrimEnd();
            if (line.Trim().Length == 0)
            {
                FlushItem();
                continue;
            }

            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                FlushSection();
                title = line[4..].Trim();
                continue;
            }

            // Headings other than the section's own are structure, not content.
            if (line.StartsWith('#'))
            {
                FlushItem();
                continue;
            }

            if (kind != ItemKind.None && Indent(line) >= 2)
            {
                pending.Append(' ').Append(line.Trim());
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                FlushItem();
                kind = ItemKind.Bullet;
                pending.Append(line[2..].Trim());
                continue;
            }

            if (kind != ItemKind.Paragraph)
            {
                FlushItem();
                kind = ItemKind.Paragraph;
            }
            if (pending.Length > 0)
                pending.Append(' ');
            pending.Append(line.Trim());
        }

        FlushSection();
        return new ReleaseNotes(sections);

        void FlushItem()
        {
            var text = pending.ToString().Trim();
            if (text.Length > 0)
                items.Add(text);
            pending.Clear();
            kind = ItemKind.None;
        }

        void FlushSection()
        {
            FlushItem();
            if (items.Count > 0)
                sections.Add(new ReleaseNotesSection(TitleOf(title), [.. items]));
            items.Clear();
            title = string.Empty;
        }
    }

    private static int Indent(string line)
    {
        var indent = 0;
        while (indent < line.Length && char.IsWhiteSpace(line[indent]))
            indent++;
        return indent;
    }

    /// <summary>Keep a Changelog's words, in the words an end user knows.</summary>
    private static string TitleOf(string title) => title switch
    {
        "Added" => "New",
        "Changed" => "Improved",
        "Fixed" => "Fixed",
        "Removed" => "Removed",
        "Deprecated" => "Deprecated",
        "Security" => "Security",
        _ => title,
    };

    private enum ItemKind
    {
        None,
        Bullet,
        Paragraph,
    }
}
