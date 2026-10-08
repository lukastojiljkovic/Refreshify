using System.Globalization;
using Refreshify.Core.Updates;

namespace Refreshify.Core.Tests.Updates;

public sealed class ReleaseNotesTests
{
    /// <summary>The release body users downloaded by hand, copied verbatim.</summary>
    private const string ReleaseBody = """
Refreshify cleans up, repairs and updates your PC with the tools Windows already has, in one native Windows 11 app.

## Download

**Refreshify-1.2.1-Setup.exe** for Windows 11, or Windows 10 version 1809 or later, x64.

SHA-256: `1DD9B9BDDF2B91BD470F4A8D4097F173A42552759EF2B01E634818AD55EEBB29`

- **SmartScreen.** The installer isn't code-signed yet, so Windows may warn you. Check the hash with `Get-FileHash .\Refreshify-1.2.1-Setup.exe`, then select **More info** > **Run anyway**.
- **Administrator approval.** Setup installs to Program Files. Refreshify starts its own exe as administrator for the tools that need it, so it must live where only administrators can replace it.
- **Provenance.** GitHub attests that this installer was built by this repository's release workflow: `gh attestation verify Refreshify-1.2.1-Setup.exe --repo lukastojiljkovic/Refreshify`.

## What's new

### Changed

- Nothing in the app. 1.2.1 is the first release that 1.2.0's update check can find, so updating to it shows that the updater works end to end.

## Verification

The [release build](https://github.com/lukastojiljkovic/Refreshify/actions/runs/37702770136) passed all 254 unit tests before it built this installer. The README describes [how Refreshify is verified](https://github.com/lukastojiljkovic/Refreshify#verification), including how to check the administrator tools.

## Limitations

See [Limitations](https://github.com/lukastojiljkovic/Refreshify#limitations) in the README.

[Terms of Use](https://github.com/lukastojiljkovic/Refreshify/blob/v1.2.1/TERMS.md) · [Privacy Statement](https://github.com/lukastojiljkovic/Refreshify/blob/v1.2.1/PRIVACY.md) · [Third-Party Notices](https://github.com/lukastojiljkovic/Refreshify/blob/v1.2.1/THIRD-PARTY-NOTICES.md)
""";

    /// <summary>A fixture copy of this repository's CHANGELOG.md.</summary>
    private const string Changelog = """
# Changelog

All notable changes to Refreshify are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/),
and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.2.1] - 2026-10-07

### Changed

- Nothing in the app. 1.2.1 is the first release that 1.2.0's update check can find, so updating to it shows that
  the updater works end to end.

## [1.2.0] - 2026-10-05

### Added

- Refreshify checks GitHub for a newer release when it starts.

## [1.1.0] - 2026-09-30

### Fixed

- Pages sat off-center in wide windows.

[Unreleased]: https://github.com/lukastojiljkovic/Refreshify/compare/v1.2.1...HEAD
[1.2.1]: https://github.com/lukastojiljkovic/Refreshify/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/lukastojiljkovic/Refreshify/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/lukastojiljkovic/Refreshify/compare/v1.0.0...v1.1.0
""";

    [Fact]
    public void A_release_body_shows_only_its_Whats_new_section()
    {
        var notes = ReleaseNotes.FromReleaseBody(ReleaseBody);

        var section = Assert.Single(notes.Sections);
        Assert.Equal("Improved", section.Title);
        var item = Assert.Single(section.Items);
        Assert.Equal(
            "Nothing in the app. 1.2.1 is the first release that 1.2.0's update check can find, so updating to it shows that the updater works end to end.",
            item);

        var items = notes.Sections.SelectMany(section => section.Items).ToList();
        foreach (var technical in new[] { "SHA-256", "SmartScreen", "attestation", "unit tests", "Terms of Use" })
            Assert.DoesNotContain(items, text => text.Contains(technical, StringComparison.Ordinal));
    }

    [Fact]
    public void A_body_without_the_heading_is_parsed_whole()
    {
        var notes = ReleaseNotes.FromReleaseBody(ReleaseBody.Replace("## What's new", "## Changes", StringComparison.Ordinal));

        Assert.Contains(notes.Sections, section => section.Items.Any(item => item.Contains("SHA-256", StringComparison.Ordinal)));
        Assert.Contains(notes.Sections, section => section.Title == "Improved");
    }

    [Fact]
    public void An_indented_line_continues_the_bullet_above_it()
    {
        var notes = ReleaseNotes.FromReleaseBody("""
### Added

- A bullet that
  wraps onto the next line.
- A second bullet.
""");

        var section = Assert.Single(notes.Sections);
        Assert.Equal(new[] { "A bullet that wraps onto the next line.", "A second bullet." }, section.Items);
    }

    [Fact]
    public void Keep_a_Changelog_titles_become_user_words()
    {
        var notes = ReleaseNotes.FromReleaseBody("""
### Added

- a

### Changed

- b

### Fixed

- c

### Something else

- d
""");

        Assert.Equal(new[] { "New", "Improved", "Fixed", "Something else" }, notes.Sections.Select(section => section.Title));
    }

    [Fact]
    public void An_unknown_heading_is_kept_and_a_paragraph_becomes_an_item()
    {
        var notes = ReleaseNotes.FromReleaseBody("""
The first release.

### Notes

A paragraph that
continues here.
""");

        Assert.Equal(new[] { "", "Notes" }, notes.Sections.Select(section => section.Title));
        Assert.Equal(new[] { "The first release." }, notes.Sections[0].Items);
        Assert.Equal(new[] { "A paragraph that continues here." }, notes.Sections[1].Items);
    }

    [Fact]
    public void The_changelog_gives_the_versions_items_and_stops_before_the_next_one()
    {
        var notes = ReleaseNotes.FromChangelog(Changelog, new Version(1, 2, 1));

        Assert.NotNull(notes);
        var section = Assert.Single(notes.Sections);
        Assert.Equal("Improved", section.Title);
        var item = Assert.Single(section.Items);
        Assert.Equal(
            "Nothing in the app. 1.2.1 is the first release that 1.2.0's update check can find, so updating to it shows that the updater works end to end.",
            item);

        var items = notes.Sections.SelectMany(section => section.Items).ToList();
        Assert.DoesNotContain(items, text => text.Contains("checks GitHub for a newer release", StringComparison.Ordinal));
        Assert.DoesNotContain(items, text => text.Contains("compare/v1.2.0", StringComparison.Ordinal));
    }

    [Fact]
    public void A_version_missing_from_the_changelog_is_null()
    {
        Assert.Null(ReleaseNotes.FromChangelog(Changelog, new Version(9, 9, 9)));
    }

    [Fact]
    public void Unreleased_is_never_returned()
    {
        const string changelog = """
## [Unreleased]

### Added

- Not released yet.

## [1.2.1] - 2026-10-07

### Fixed

- A real fix.
""";

        var notes = ReleaseNotes.FromChangelog(changelog, new Version(1, 2, 1));

        Assert.NotNull(notes);
        Assert.Equal(new[] { "Fixed" }, notes.Sections.Select(section => section.Title));
        Assert.DoesNotContain(
            notes.Sections.SelectMany(section => section.Items),
            item => item.Contains("Not released yet", StringComparison.Ordinal));
    }

    [Fact]
    public void A_version_heading_with_a_suffix_other_than_a_date_is_not_matched()
    {
        var notes = ReleaseNotes.FromChangelog("## [1.2.1] notes\n\n### Fixed\n\n- a\n", new Version(1, 2, 1));

        Assert.Null(notes);
    }

    [Fact]
    public void CRLF_gives_the_same_notes_as_LF()
    {
        var lf = ReleaseNotes.FromReleaseBody(ReleaseBody);
        var crlf = ReleaseNotes.FromReleaseBody(ReleaseBody.Replace("\n", "\r\n", StringComparison.Ordinal));

        Assert.Equal(lf.Sections.Select(section => section.Title), crlf.Sections.Select(section => section.Title));
        Assert.Equal(
            lf.Sections.SelectMany(section => section.Items),
            crlf.Sections.SelectMany(section => section.Items));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_input_gives_empty_notes(string? body)
    {
        var notes = ReleaseNotes.FromReleaseBody(body);

        Assert.True(notes.IsEmpty);
        Assert.Empty(notes.Sections);
    }

    [Fact]
    public void Published_at_is_parsed_and_a_missing_or_invalid_one_is_null()
    {
        Assert.True(ReleaseReader.TryParse(Releases.Json("v1.2.0", publishedAt: "2026-10-07T12:00:00Z"), out var dated, out _));
        Assert.Equal(DateTimeOffset.Parse("2026-10-07T12:00:00Z", CultureInfo.InvariantCulture), dated!.PublishedAt);

        Assert.True(ReleaseReader.TryParse(Releases.Json("v1.2.0"), out var undated, out _));
        Assert.Null(undated!.PublishedAt);

        Assert.True(ReleaseReader.TryParse(Releases.Json("v1.2.0", publishedAt: "not a date"), out var invalid, out _));
        Assert.Null(invalid!.PublishedAt);
    }
}
