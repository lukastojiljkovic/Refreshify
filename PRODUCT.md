# Product

<!-- impeccable:product-schema 1 -->

## Platform
windows

## Users

People who maintain their own Windows PC and want the usual cleanup, repair and update tasks done without opening a
terminal. They understand what the underlying Windows tools do, or are willing to read a one-line explanation, and they
run Refreshify on a machine they are allowed to maintain. The app is used in English.

## Product Purpose

Refreshify runs the maintenance tools Windows already has, plus winget, from one window and shows what each one did.
It replaces a sequence of typed commands such as `DISM /RestoreHealth`, `sfc /scannow`, `chkdsk /scan` and
`winget upgrade` with a catalog the user can read, choose from and run in a safe order.

## Positioning

Refreshify is free and open source (MIT). It does not bundle its own cleaner: every tool it runs is a Windows tool or
winget, so its results depend on them. It offers no accounts, telemetry, analytics or ads, and the only request it makes
itself is the update check against GitHub. Its distinguishing choices are plain-language explanations, a restore point
before a run, a single administrator approval per session, and a report for a failed step that the user can paste into
an AI assistant with personal details hidden.

## Operating Context

- Windows 11 is the tested target (Windows 11 Pro 25H2, build 26200). It also runs on Windows 10 version 1809 or
  later, x64, but Windows 10 has not been tested.
- Administrator tools run in an elevated helper that starts on the first such step and lasts for the session.
- Refreshify works on a PC with no network except for the tools that need one: Windows Update, System image repair,
  Defender definitions, time sync, App updates (winget) and the update check.
- Runs are recorded in History with their duration and the space they freed, and raw output is kept as logs.

## Capabilities and Constraints

The tool catalog is grouped into Cleanup, Repair, Network, Updates & security and Troubleshooting. Cleanup deletes
temporary files, thumbnail and icon caches, Windows Update downloads, the Delivery Optimization cache, error reports
and crash dumps, and can empty the Recycle Bin. Repair runs System image repair, System file check, Component store
cleanup and Disk check. Network clears the DNS and ARP caches and syncs the clock. Updates & security updates Defender
definitions, installs Windows updates, updates apps with winget and runs a Defender quick scan. Troubleshooting covers
renewing the IP address, resetting the network stack and Windows Update, the Microsoft Store, font, print and search
caches, the audio services, File Explorer, and the graphics shader cache.

Constraints:

- x64 only, and the interface is English only.
- *Run all* runs the selected tools in a fixed order — restore point, Cleanup, Repair, Network, Updates & security,
  Troubleshooting — and DISM always runs before SFC.
- Cleanups skip files in use and never follow junctions or symbolic links.
- History keeps the last 50 runs; uninstalling removes the settings, history and reminder of the account that runs the
  uninstaller.
- The installer is not code-signed, so SmartScreen may warn; the release publishes a SHA-256 checksum to check the
  download.
- Refreshify can only fix what Windows' own tools can fix, and the Defender tools are skipped when another antivirus
  protects the PC.

## Brand Commitments

- **Plain language.** Every tool says what it does, and troubleshooting tools say when to use them. The user never sees
  a terminal.
- **Measured numbers only.** The app shows bytes and files actually deleted and updates actually installed; it does not
  estimate.
- **Locale-independent detection.** Problems are recognized from exit codes, HRESULTs, `CBS.log` and
  `DISM /English`, never from console text that changes with the Windows language.
- **Safe by default.** A run lists what it will do first and creates a System Restore point before it starts.
- **No accounts and no tracking.** No telemetry, analytics, crash reporting or ads.

## Evidence on Hand

- Screenshots in `docs/images/`: `home-light.png` and `home-dark.png`, `cleanup-light.png` and `cleanup-dark.png`,
  `run-light.png` and `run-dark.png`.
- The product site under `site/`: `index.html`, `site.css`, `sitemap.xml`, `llms.txt`, `icon.png` and the Archivo font
  files, published through the Pages workflow.
- CI gates in `.github/workflows/ci.yml`: a formatting check, the unit tests, the installer build, and an install and
  uninstall smoke test.
- Releases from `.github/workflows/release.yml`: the installer, a `.sha256` checksum file, build provenance
  attestation, and release notes taken from `CHANGELOG.md`.
- Unit tests in `tests/Refreshify.Core.Tests` (xUnit v3); the README states the suite runs 209 tests.
- `CHANGELOG.md`, `TERMS.md`, `PRIVACY.md`, `THIRD-PARTY-NOTICES.md` and `SECURITY.md`.

Not on hand: no user metrics, no download counts, no testimonials, no benchmarks, and no independent security audit.
None should be invented.

## Product Principles

- Run the tools Windows already has, in an order that avoids known failures.
- Recognize common failures, fix the safe ones and ask before the rest.
- Never invent a number; report only what was measured.
- Keep administrator use narrow: one approval per session, and an elevated helper that runs only catalog tools with
  validated options.
- When a problem cannot be fixed, hand the user everything needed to get help elsewhere.

## Accessibility & Inclusion

Refreshify uses standard WinUI 3 / Fluent controls and theme resources, so it follows the Windows theme, contrast and
text-scaling settings, and it ships light and dark themes plus a "use system setting" option. Controls carry
`AutomationProperties` names, and the README describes UI Automation coverage of the pages, dialogs, settings
persistence and themes. The interface is English only; that is a decision, not an oversight.
