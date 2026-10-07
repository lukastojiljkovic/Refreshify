---
name: Refreshify
description: "A native WinUI 3 interface built entirely from Fluent controls and theme resources: standard cards on a Mica backdrop, one accent colour the user picks in Windows, and a single type ramp, with a separate black-and-white brochure palette for the product site."

colors:
  accent: AccentTextFillColorPrimaryBrush
  card: CardBackgroundFillColorDefaultBrush
  cardStroke: CardStrokeColorDefaultBrush
  secondaryText: TextFillColorSecondaryBrush
  tertiaryText: TextFillColorTertiaryBrush
  siteAccent: "#1a5fbf"
  siteAccentDark: "#64a6f4"
  siteBand: "#000"
  siteOnBand: "#fff"
  siteOnBandMuted: "#c4c8c0"
  sitePaper: "#fff"
  siteInk: "#0d0e0c"
  siteInkMuted: "#4a4f47"
  siteHairline: "#d5d9d1"
  siteDarkPaper: "#141613"
  siteDarkInk: "#eef0ea"
  siteDarkInkMuted: "#a9aea4"
  siteDarkHairline: "#30352e"

typography:
  title: TitleTextBlockStyle
  bodyStrong: BodyStrongTextBlockStyle
  body: BodyTextBlockStyle
  caption: CaptionTextBlockStyle
  monoFamily: '"Cascadia Mono", Consolas'
  monoSize: 12px
  siteFamily: Archivo
  siteBodySize: 1.0625rem

rounded:
  control: ControlCornerRadius
  siteButton: 2px

spacing:
  cardPadding: 16,12
  cardColumnSpacing: 16
  settingsCardMinHeight: 68
  sectionHeaderMargin: 1,28,0,6
  pagePadding: 32,20,32,32
  pageSpacing: 4
  paneLength: 240
  siteGutter: 24px
  siteMargin: clamp(20px, 3.4vw, 48px)

components:
  settings-card:
    backgroundColor: "{colors.card}"
    rounded: "{rounded.control}"
    padding: "{spacing.cardPadding}"
    height: 68
  card:
    backgroundColor: "{colors.card}"
    rounded: "{rounded.control}"
    padding: "{spacing.cardPadding}"
  icon-button:
    width: 36
    height: 32
  page:
    padding: "{spacing.pagePadding}"
    width: 1000
  run-page:
    width: 1400
    padding: "32,20,32,24"
---

# Design System: Refreshify

## Overview

**Creative North Star: "One Fluent window, no terminal"**

Refreshify looks like a Windows settings page rather than a custom tool. It is stock WinUI 3: a `MicaBackdrop`, the
WinUI `TitleBar` and a `NavigationView`, with every surface drawn from Fluent theme resources instead of app colours.
The one chromatic decision is the accent colour, which comes from the user's Windows personalization. The app defines
only a handful of styles of its own: most in `src/Refreshify/App.xaml`, and a few beside the views and dialogs that use
them (`RunView.xaml`, `WelcomeDialog.xaml`).

The product site under `site/` is deliberately different: a black title band on white paper, a 12-column grid and one
colour per publication, set in Archivo. It shares the design vocabulary of the rest of the portfolio rather than the
app's Fluent look.

**Key Characteristics:**

- Fluent resources for every colour, radius and type style; light, dark and high-contrast follow Windows.
- One accent reference (`{colors.accent}`), and the primary buttons use Fluent's `AccentButtonStyle`; both follow the
  colour the user chose in Windows.
- Cards are one shape: `{rounded.control}` corners, a 1px `{colors.cardStroke}` border and `{spacing.cardPadding}`.
- The site is a black-band brochure in `{typography.siteFamily}` with `{colors.siteAccent}` as its publication colour.

## Colors

The app defines no palette of its own. Every brush is a WinUI theme resource referenced from `src/Refreshify/App.xaml`:

| Token | Fluent resource | Used for |
| --- | --- | --- |
| `{colors.card}` | `CardBackgroundFillColorDefaultBrush` | card, settings row and card-button background |
| `{colors.cardStroke}` | `CardStrokeColorDefaultBrush` | the 1px card border |
| `{colors.accent}` | `AccentTextFillColorPrimaryBrush` | the accent glyph in dialogs |
| `{colors.secondaryText}` | `TextFillColorSecondaryBrush` | card and row descriptions |
| `{colors.tertiaryText}` | `TextFillColorTertiaryBrush` | the empty-state glyph |

The accent itself is not a hex value in the app: `AccentButtonStyle` and the Fluent accent resources take the colour the
user chose in Windows. The product site sets its own publication colour in `site/index.html` — `--app: #1a5fbf` for
light and `--app-dark: #64a6f4` for dark — on top of the black-and-white palette in `site/site.css`
(`{colors.siteBand}`, `{colors.sitePaper}`, `{colors.siteInk}`, `{colors.siteHairline}`, and their dark-theme
counterparts `{colors.siteDarkPaper}`, `{colors.siteDarkInk}`, `{colors.siteDarkHairline}`).

**The Theme-Resource Rule.** Colour is named by its Fluent resource, never by a hex value in the app, so the light, dark
and high-contrast themes all work.

**The Single-Accent Rule.** The accent appears on the primary action, the dialog step glyph and the focused step; it is
not used for decoration.

## Typography

Type is the WinUI ramp, used through its named styles rather than custom sizes, from `src/Refreshify/App.xaml` and the
views:

| Token | WinUI style | Used for |
| --- | --- | --- |
| `{typography.title}` | `TitleTextBlockStyle` | the page title, such as **Settings** |
| `{typography.bodyStrong}` | `BodyStrongTextBlockStyle` | section headers and row headers |
| `{typography.body}` | `BodyTextBlockStyle` | body text |
| `{typography.caption}` | `CaptionTextBlockStyle` | descriptions and secondary lines |

`SecondaryCaptionStyle` and `SecondaryBodyStyle` keep their Fluent base style and only change the foreground to
`{colors.secondaryText}`, with `TextWrapping="NoWrap"` and `CharacterEllipsis`. The one family the app names itself is
a monospace stack, `{typography.monoFamily}`, at `{typography.monoSize}`, for the command lines and the raw output in
the run details and the tool flyouts; the "Use it when:" prefix is SemiBold. Screen captures are set in the site's
`{typography.siteFamily}` at `{typography.siteBodySize}`.

**The Ramp Rule.** Text uses a named Fluent style; the monospace command and output text is the one family the app
names itself.

## Layout

The window is a `NavigationView` with a 240px pane (`{spacing.paneLength}`). Pages are a single column with
`MaxWidth="1000"` and `{spacing.pagePadding}`, except Run and History, which go to `width: 1400` (`{components.run-page}`)
so their two panes fit. The window opens at 1180×800, has a minimum of 820×560, and both panes inside Run keep their
layout down to that minimum. Card grids use `{spacing.cardColumnSpacing}` and `{spacing.pageSpacing}` between rows.

**The One-Column Rule.** Pages are one centered column; only Run and History widen, and only to fit their two panes.

## Elevation & Depth

Depth comes from Windows, not from the app. The window sets `MicaBackdrop` as its `SystemBackdrop`, cards sit on it with
a 1px `{colors.cardStroke}` border and `{rounded.control}` corners, and nothing in the app defines a shadow. The
InfoBars that report status or an available update are the only elevated surface the app places itself; flyouts and
dialogs use Fluent's own elevation.

**The Mica Rule.** The Mica backdrop is the only background the app sets, and cards are separated by a border rather
than a shadow.

## Shapes

One corner radius runs through the app: cards, settings rows and card buttons are `{rounded.control}`
(`ControlCornerRadius`, from `src/Refreshify/App.xaml`). Icon buttons are `width: 36` by `height: 32`
(`{components.icon-button}`) with the same control radius. The only exception is the two panes on the Run page, whose
`PaneStyle` uses Fluent's own `OverlayCornerRadius`. The product site is the opposite: buttons are
`{rounded.siteButton}` and everything else is square.

**The Control-Radius Rule.** Card surfaces use `ControlCornerRadius`; the Run page's panes take Fluent's
`OverlayCornerRadius` and the product site uses its own 2px, so no surface invents a radius of its own.

## Components

- **Settings row** (`controls:SettingsCard`) — a glyph, a header and a description with the setting's control on the
  right. `{components.settings-card}`: `{colors.card}` background, `{colors.cardStroke}` border, `{rounded.control}`
  corners, `{spacing.cardPadding}` padding and a `{spacing.settingsCardMinHeight}` minimum height. The glyph is a
  `FontIcon` at 20px. Section headers use `{spacing.sectionHeaderMargin}`.
- **Card** (`CardStyle`, a `Grid`) — the same background, border, radius and padding, with
  `{spacing.cardColumnSpacing}` between columns. One card per tool on a category page; `CardButtonStyle` is the same
  shape as a stretched button.
- **Icon button** (`IconButtonStyle`) — `{components.icon-button}`, transparent background and border, used for the
  toolbar and list controls.
- **Page** — `MaxWidth="1000"` with `{spacing.pagePadding}` and `{spacing.pageSpacing}`, holding a `TitleTextBlockStyle`
  title, section headers and settings rows or cards.
- **Status surfaces** — `InfoBar` for the status line and for an available update, using Fluent severity colours; the
  update bar's **Update** button is `AccentButtonStyle`.

**The Card Rule.** A tool, a setting and a summary are all the same card shape; the app does not invent a second
container style.

## Do's and Don'ts

- Do reference Fluent theme resources (`{colors.card}`, `{colors.secondaryText}`, `{rounded.control}`) so light, dark
  and high-contrast follow Windows.
- Do use the WinUI type styles (`{typography.title}` … `{typography.caption}`) and the single card shape.
- Do keep the accent for the primary action and the step glyph.
- Do show only measured numbers, in plain language.
- Don't hard-code a colour, radius or font size in the app.
- Don't add a second container shape or a drop shadow; the border and Mica carry the depth.
- Don't mix the app's Fluent look with the site's black-band brochure look; they are separate surfaces.
