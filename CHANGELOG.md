# Changelog

All notable changes to Refreshify are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/),
and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [2.0.0] - 2026-10-08

Refreshify now tells you how your PC is doing and where its disk space went, and its update notes are written to be
read.

### Added

- **Health**: a page that checks how your PC is doing and says it in plain words. It looks at the free space on each
  drive, the health your drives report, how much charge your battery holds, whether a restart is waiting, how long
  since the last restart, your virus protection, the last Windows update and whether Windows is activated. Each check
  says what it found and what to do about it, and where Refreshify has a tool for it, a button opens that tool. Home
  shows a line when something needs attention. Nothing is changed and no administrator approval is needed.
- **Disk space**: a page that scans your files or a drive and lists the folders and files that take the most space,
  largest first, each with its size and its share of the folder. Select a folder to go into it, or switch to
  **Largest files** to see the 100 largest files below where you are. Every row can open its place in File Explorer.
  Nothing is deleted from this page.
- A **What's new** dialog written for you: it shows only what changed, grouped **New**, **Improved** and **Fixed**,
  with the release date and one link to the full notes on GitHub, and it offers **Update now**. The first time you
  start a new version, Refreshify shows what changed in that version, once, instead of the welcome dialog.

### Changed

- The update notice is worded more plainly, and its buttons no longer sit against its bottom edge.
- Release notes on GitHub put the changes first, so they are the first thing you see.

### Fixed

- A folder or file whose name contains a comma now opens the right place in File Explorer.
- A folder more than 512 levels deep is counted as unreadable instead of failing the scan.

## [1.2.1] - 2026-10-07

### Changed

- Nothing in the app. 1.2.1 is the first release that 1.2.0's update check can find, so updating to it shows that
  the updater works end to end.

## [1.2.0] - 2026-10-05

### Added

- Refreshify checks GitHub for a newer release when it starts (at most once a day) and from **Settings** > **Updates**,
  shows a banner with the release notes when one exists, and installs it after verifying the installer against the
  SHA-256 checksum published with the release. The automatic check can be turned off.

## [1.1.0] - 2026-09-30

### Added

- The run page shows the steps and one step's details side by side. The details follow the running step, or show the
  step you select: what it does, what it's doing now, the last lines of its output and how long it took.
- Live numbers during a run: time elapsed, steps finished, space freed and steps that need attention. Cleanups show how
  many files and how much space they have deleted so far.
- History lists each run's duration, how its steps ended and the space it freed.

### Changed

- Run and History use more of a wide window. **Current run** moved below **History** in the navigation.

### Fixed

- Pages sat off-center in wide windows.
- The result icons in History and on the run page looked stretched.
- The navigation items overlapped for a moment when **Current run** appeared.
- A stopped run's summary said that every step finished.

## [1.0.0] - 2026-09-27

The first release.

### Added

- 27 maintenance tools in five categories: Cleanup, Repair, Network, Updates & security and Troubleshooting. Each one
  explains what it does, and troubleshooting tools say when to use them.
- *Run all*, which runs the 16 tools it includes by default in a safe order, and lets you choose which tools it runs.
  You can also run a category or a single tool.
- A System Restore point before anything changes, skipped when one from the last 24 hours exists.
- Known problems: Refreshify recognizes 15 common failures, explains them in plain language and fixes the ones it
  safely can, such as a damaged Windows Update cache or a disabled repair service.
- **Get help**: a report about a failed step to paste into an AI assistant, with personal details hidden by default.
- Run history with the results of the last 50 runs, and the tools' technical output when you want it.
- An optional reminder to refresh your PC every 1, 2, 3 or 6 months.
- Light and dark themes that follow Windows, or a theme you choose.

[Unreleased]: https://github.com/lukastojiljkovic/Refreshify/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/lukastojiljkovic/Refreshify/compare/v1.2.1...v2.0.0
[1.2.1]: https://github.com/lukastojiljkovic/Refreshify/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/lukastojiljkovic/Refreshify/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/lukastojiljkovic/Refreshify/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/lukastojiljkovic/Refreshify/releases/tag/v1.0.0
