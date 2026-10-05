# Changelog

All notable changes to Refreshify are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/),
and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

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

[Unreleased]: https://github.com/lukastojiljkovic/Refreshify/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/lukastojiljkovic/Refreshify/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/lukastojiljkovic/Refreshify/releases/tag/v1.0.0
