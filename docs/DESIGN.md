# Refreshify design

Refreshify is a native Windows 11 utility that runs the built-in Windows maintenance tools (DISM, SFC, chkdsk, cache
cleanup, network resets, Windows Update, winget, Microsoft Defender) in the background and presents everything through
a WinUI 3 interface. Users never see a console window: progress, results and errors are shown in plain language.

## Goals

- Run all maintenance at once, one category, or a single tool.
- Explain every tool in plain language, and name the problem it solves.
- Recognize common failures. Fix them automatically where that's safe, ask first where it isn't, and otherwise hand the
  user a ready-to-paste report for an LLM.
- Show only measured numbers (bytes actually deleted, updates actually installed). Never estimate.
- Ask for administrator approval once per session, not once per tool.

## Non-goals

These are deliberately excluded because they are ineffective, risky or irreversible:

- registry cleaners, prefetch clearing, "RAM optimizers"
- disabling services, telemetry tweaks, debloating
- `DISM /ResetBase` (makes installed updates impossible to uninstall)
- clearing event logs (destroys diagnostic data)
- browser caches (logs users out; every browser differs)

## Tool catalog

`Context` is where a tool runs: **User** runs in the unelevated app, **Admin** runs in the elevated worker.
Tools marked **opt-in** are not part of *Run all* until the user includes them.

### Safety (runs before every run when enabled in Settings)

| Id | Context | Mechanism |
| --- | --- | --- |
| `restore-point` | Admin | `Checkpoint-Computer`. Skipped when a restore point from the last 24 hours exists (Windows' own limit). System Protection off → ask to enable (`Enable-ComputerRestore`). |

### Cleanup

| Id | Name | Context | Default | Mechanism |
| --- | --- | --- | --- | --- |
| `temp-files` | Your temporary files | User | on | `%TEMP%`, `INetCache`, `%LOCALAPPDATA%\CrashDumps`; files older than the configured age |
| `windows-temp` | Windows temporary files | Admin | on | `%SystemRoot%\Temp`; files older than the configured age |
| `explorer-caches` | Thumbnail and icon caches | User | on | Restart Manager shuts File Explorer down, `thumbcache_*.db` / `iconcache_*.db` / `IconCache.db` are deleted, Explorer is restarted |
| `update-downloads` | Windows Update downloads | Admin | on | Stop `wuauserv`/`bits`, empty `SoftwareDistribution\Download`, restore service state. Skipped while a restart is pending |
| `delivery-optimization` | Delivery Optimization cache | Admin | on | `Delete-DeliveryOptimizationCache -Force` |
| `crash-dumps` | Error reports and crash dumps | Admin | on | WER `ReportArchive`/`ReportQueue`/`Temp`, `Minidump`, `MEMORY.DMP`, `LiveKernelReports` |
| `recycle-bin` | Empty Recycle Bin | User | opt-in | `SHQueryRecycleBin` + `SHEmptyRecycleBin` |

### Repair

| Id | Name | Context | Mechanism |
| --- | --- | --- | --- |
| `dism-restorehealth` | System image repair | Admin | `DISM /Online /Cleanup-Image /RestoreHealth /English`; optional `/Source` from a user-selected ISO |
| `sfc` | System file check | Admin | `sfc /scannow`, always after DISM; result from the console text and the new `[SR]` lines in `CBS.log` |
| `component-cleanup` | Component store cleanup | Admin | `DISM /Online /Cleanup-Image /StartComponentCleanup /English` |
| `disk-check` | Disk check | Admin | `chkdsk <system drive> /scan`; exit code 3 → offer offline repair at the next restart (`fsutil dirty set`) |

### Network

| Id | Name | Context | Mechanism |
| --- | --- | --- | --- |
| `network-caches` | Network caches | Admin | `ipconfig /flushdns`, `netsh interface ip delete arpcache` |
| `time-sync` | Time sync | Admin | Start `W32Time` if needed, `w32tm /resync`. Skipped when the service is disabled |

### Updates & security

| Id | Name | Context | Mechanism |
| --- | --- | --- | --- |
| `defender-definitions` | Defender definitions | Admin | `Update-MpSignature`. Skipped when Defender isn't the active antivirus |
| `windows-update` | Windows Update | Admin | Windows Update Agent COM API: search `IsInstalled=0 and IsHidden=0 and BrowseOnly=0`, download and install one update at a time |
| `app-updates` | App updates | Admin | `winget upgrade --all --silent --accept-source-agreements --accept-package-agreements --disable-interactivity` |
| `defender-scan` | Defender quick scan | Admin | `Start-MpScan -ScanType QuickScan`, then detections newer than the scan start |

### Troubleshooting (all opt-in, each described by the symptom it fixes)

| Id | Name | Context | Mechanism |
| --- | --- | --- | --- |
| `renew-ip` | Renew IP address | Admin | `ipconfig /release`, `ipconfig /renew` |
| `network-reset` | Network stack reset | Admin | `netsh winsock reset`, `netsh int ip reset`; restart required |
| `reset-windows-update` | Reset Windows Update | Admin | Stop `wuauserv`, `bits`, `cryptsvc`; rename `SoftwareDistribution` and `catroot2` to `*.bak`; restart the services |
| `store-cache` | Microsoft Store cache | User | `wsreset.exe` |
| `font-cache` | Font cache | Admin | Stop `FontCache`, delete the cache files, start it again; restart recommended |
| `print-queue` | Print queue | Admin | Stop `Spooler`, empty `spool\PRINTERS`, start it again |
| `search-index` | Search index | Admin | Stop `WSearch`, set `SetupCompletedSuccessfully=0`, start it again (Windows rebuilds the index) |
| `restart-audio` | Audio services | Admin | Restart `AudioEndpointBuilder` and `Audiosrv` |
| `restart-shell` | Start menu and taskbar | User | Restart File Explorer through Restart Manager, end `StartMenuExperienceHost`, `ShellExperienceHost`, `SearchHost` (Windows restarts them) |
| `shader-cache` | Graphics shader cache | User | `D3DSCache` and the NVIDIA, AMD and Intel shader cache folders |

### Run order

*Run all* runs the selected tools in catalog order: restore point → Cleanup → Repair → Network → Updates & security →
Troubleshooting. Cleanup comes first because DISM and Windows Update need free space. Repair comes before updates
because a damaged component store makes updates fail. DISM always runs before SFC.

## Architecture

```text
src/Refreshify.Core          All logic, no UI: catalog, tools, process runner, parsers, run engine, known issues,
                             LLM report, worker protocol, history. Unit tested.
src/Refreshify               WinUI 3 app. The same executable hosts the elevated worker (--worker) and the
                             reminder (--reminder).
tests/Refreshify.Core.Tests  xUnit v3
installer/Refreshify.iss     Inno Setup 6
```

### Process model

```text
Refreshify.exe (medium integrity, UI)
  ├─ runs User tools in-process
  └─ named pipe ──► Refreshify.exe --worker <pipe> <ui-pid> (elevated, started once per session with Verb=runas)
                       └─ runs Admin tools, starts hidden child processes (dism, sfc, powershell, winget, …)
```

- The worker starts on the first Admin step of a session and stays alive until the app closes, so there is only one
  UAC prompt per session. If the user declines it (`ERROR_CANCELLED`, 1223), the run's Admin steps are skipped with an
  explanation; the next run asks again.
- User tools stay in the user's context on purpose: File Explorer must be restarted unelevated, and per-user caches
  belong to the signed-in user even when an administrator approves elevation with different credentials.

### Worker protocol and security

- JSON lines over a named pipe (`System.Text.Json`, source-generated, polymorphic messages): `run` (tool id + options),
  `cancel`, `event` (progress/status/output), `result`.
- The UI creates the pipe with a random name, a single instance and an ACL granting only the current user and
  Administrators. It accepts the connection only from the process it launched (`GetNamedPipeClientProcessId`). The
  worker only accepts a server whose process id is the one it was given (`GetNamedPipeServerProcessId`).
- The worker never runs command lines from the pipe. It only runs catalog tools whose context is Admin, with typed and
  validated options (temp file age in range; ISO path rooted, existing, `.iso`).
- The installer puts the binaries in Program Files, so the elevated executable isn't writable by the user.

### Tools

Every tool implements `Tool.RunAsync(ToolContext, CancellationToken) → ToolResult`. Most are configured instances of
a few generic tools:

- `CommandTool`: run a sequence of commands; success on the documented exit codes.
- `CleanupTool`: delete files from a list of targets (optional age filter), optionally with services stopped and restored.
- `ServiceRestartTool`: stop and start services in dependency order.

DISM, SFC, chkdsk, Windows Update, winget, Defender, restore point, File Explorer caches, Recycle Bin, search index and
time sync have their own classes.

`ToolResult` carries the outcome (Succeeded, Warning, Failed, Skipped, Cancelled), a one-line plain-language summary,
measured bytes freed, a restart-required flag, a detected known-issue id, the error code, and a trace (command lines,
exit codes, output tail, relevant log excerpt) for the LLM report.

### Process runner

- Executables are resolved to full paths under `%SystemRoot%\System32` (no `PATH` lookup in the elevated worker).
- `CreateNoWindow`, redirected stdout/stderr, stdin closed at once.
- Output is split on both `\r` and `\n`, because DISM and chkdsk redraw progress with carriage returns.
- Encodings: SFC writes UTF-16LE when redirected; console tools use the OEM code page; PowerShell scripts force UTF-8.
- PowerShell runs as `powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand`. Scripts
  report results as JSON lines, including the exception's `HResult`.
- Cancellation kills the process tree. Tools marked *not interruptible* (DISM, SFC, component cleanup, update installs)
  are never killed; the run stops after they finish. Windows Update checks a cancel flag file between updates.

### Locale independence

Detection never depends on localized console text alone:

- DISM runs with `/English` and reports errors as HRESULT exit codes.
- chkdsk exit codes are documented (0 clean, 1 fixed, 2 cleanup, 3 errors not fixed).
- SFC is judged from the new `[SR]` lines in `CBS.log` (English on every locale), with the English console text as a
  first check; pending repairs and a disabled TrustedInstaller service are detected before SFC starts.
- PowerShell scripts return HRESULTs and enum values, not messages.
- winget results come from its documented `0x8A15xxxx` exit codes.

## Known issues and fixes

A tool that recognizes a failure returns a known-issue id. Each issue has a title, a plain-language explanation and a
remedy:

| Remedy | Behavior |
| --- | --- |
| Automatic | Runs the fix tools, then retries the failed tool once. With "Fix known problems automatically" off, it asks first. |
| Ask first | Explains the fix and asks for consent (system changes such as turning on System Protection). |
| Restart | Offers to restart Windows. |
| Windows image | Guides the user to pick a Windows ISO; DISM repairs from it, then the failed tool is retried. |
| Manual | Explains what to do; the user can retry. |

| Issue | Detected by | Remedy |
| --- | --- | --- |
| SFC couldn't repair some files | `Cannot repair member file` in CBS.log, or the English console text | Automatic: DISM RestoreHealth, retry SFC |
| SFC repair service disabled | TrustedInstaller start type Disabled | Automatic: set it to Manual, retry |
| Restart pending | CBS `RebootPending`, `WinSxS\pending.xml`, DISM `0x800F082F` | Restart |
| DISM repair source unavailable | `0x800F081F`, `0x800F0906`, `0x800F0907` | Windows image |
| Windows Update service disabled | `0x80070422` | Automatic: set `wuauserv` to Manual, retry |
| Windows Update cache damaged | `0x80070002`, `0x80070003`, `0x80248007`, `0x80246007` | Automatic: Reset Windows Update, retry |
| Component store damaged | `0x80073712` | Automatic: DISM RestoreHealth, retry |
| Windows Update busy | `0x80240016` | Manual |
| Not enough disk space | `0x80070070`, winget `0x8A150105` | Automatic: cleanup tools, retry |
| No connection | `0x8024402C`, `0x80072EE2`, `0x80072EE7`, `0x80072EFD`, winget `0x8A150107` | Manual |
| winget missing | `winget.exe` not found | Ask first: re-register App Installer |
| winget sources damaged | `0x8A15000B`, `0x8A15000F`, `0x8A150045` | Automatic: `winget source reset --force`, retry |
| Some apps couldn't update | `0x8A15002C`, `0x8A150101`, `0x8A150103`, `0x8A150111` | Manual: close the apps and retry |
| System Protection off | restore point `0x80070422` | Ask first: turn it on for the system drive, retry |
| Disk errors need offline repair | chkdsk exit code 3 | Ask first: `fsutil dirty set`, restart |

During a run, automatic fixes happen inline. Issues that need consent or a decision are collected. The step shows
**Fix it** and the run continues. The restore point is the exception: if it fails, the user decides right away whether
to fix it, continue without it or cancel, because it's the safety net for everything after it.

Each remedy is attempted at most once per step, so fixes can't loop.

### LLM report

When a failure has no remedy, or its remedy didn't work, **Get help** opens a dialog with a copyable Markdown report:

- Windows edition, version, build, architecture and display language; Refreshify version
- every step of the run with its outcome
- the failed step: command lines, exit code / HRESULT, detected issue, fixes already tried
- the last lines of its output and the relevant log excerpt (`CBS.log` `[SR]` lines, `dism.log` errors)
- a short instruction asking the LLM for step-by-step help

With "Hide personal details" on (default), the user name, profile path and computer name are replaced with
placeholders. Nothing is sent anywhere; the user copies the text.

## User interface

WinUI 3 with Mica, the WinUI `TitleBar`, and a `NavigationView`:

- **Home**: last full refresh, free space on the system drive, pending restart, number of tools in *Run all*, Windows
  version, and the **Run all** button.
- **Cleanup, Repair, Network, Updates & security, Troubleshooting**: one card per tool with name, plain-language
  description (and "Use it when…" for troubleshooting), badges (restart required, takes a while, restarts File
  Explorer, may close apps), an *Include in Run all* checkbox, **Run** and a details flyout with the commands. Each page
  has **Run selected** for the category.
- **Run**: four numbers at the top (time elapsed, or the run's duration once it ends; steps finished; space freed;
  steps that need attention), **Cancel** while it runs, and afterwards a summary with **Restart now** when a restart is
  required. Below, two panes: the steps with their status and how long each took, and the details of the running step,
  or of the step the user selects. The details show what the tool does, its live status and percentage when it reports
  one, its last six lines of output while it runs (without the scripts' JSON messages), the known problem and the fixes
  tried, **Fix it** / **Try again** / **Get help**, and the raw output when "Show technical details" is on. Cleanups
  count the files and space they have deleted as they go. After a run, the details open on the first step that needs
  attention.
- **History**: a table of past runs with their result, start, duration, how the steps ended and the space freed.
  Opening one shows the same run view, including **Get help**. Runs saved by 1.0 have no step times.

Run and History are up to 1400 px wide, the other pages 1000 px. Both panes fit down to the window's minimum width, so
the Run page keeps its layout at every size.
- **Settings**: theme, restore point before running, fix known problems automatically, temporary file age, show
  technical details, hide personal details in reports, reminder, welcome screen, history and logs, About (version,
  MIT license, source, Terms, Privacy).
- **Welcome** dialog on first launch: what the app does in three steps, a note about restore points and restarts,
  links to the Terms and the privacy statement, and an unchecked "Don't show this again".

Before a run, a confirmation dialog lists what will happen: the tool count, the restore point, UAC, File Explorer
restarting, and apps that may close during app updates. Closing the window during a run asks first, and the run
continues until the current step finishes.

## Settings and data

- Preferences: DWORDs in `HKCU\Software\Refreshify`. Per-tool *Run all* selection: `HKCU\Software\Refreshify\Tools`.
- History: `%LOCALAPPDATA%\Refreshify\History\<run>.json`, the last 50 runs.
- Raw output logs: `%LOCALAPPDATA%\Refreshify\Logs\<run>.log`.
- The uninstaller removes the registry key, the data folder, the reminder task and the notification registration.

## Reminder

Off by default. When set to every 1, 2, 3 or 6 months, Refreshify registers a per-user scheduled task that starts
`Refreshify.exe --reminder` daily. That mode checks the last full refresh and the last reminder. If a reminder is due,
it shows a Windows notification that opens Refreshify when clicked, then exits. The click opens `refreshify:`, a
per-user link that starts Refreshify without arguments. A second start hands over to the open window.

## Testing

- Unit tests (Core): catalog invariants, parsers (DISM progress, SFC log analysis, chkdsk and winget exit codes,
  Windows Update events), known-issue mapping, file cleanup (age filter, locked files, reparse points not followed),
  LLM report content and redaction, run engine (ordering, remedies retried once, elevation declined, cancellation), and
  the worker protocol over a real named pipe.
- UI verification with UI Automation and screenshots (see the playbook): Home, every category, a User-context run,
  Settings persistence, History, dialogs.
- Tools that need administrator rights are verified manually from an elevated session; see the README.

## Build, CI/CD and release

- .NET 10 (`net10.0-windows10.0.26100.0`), Windows App SDK 2.x (WinUI 3), unpackaged and self-contained, x64.
- `build.ps1`: test → publish (ReadyToRun) → collect third-party licenses → Inno Setup installer.
- GitHub Actions:
  - **CI** on pushes and pull requests: format check, tests, build and installer artifact, and an install and
    uninstall smoke test of the installer.
  - **Release** on `v*` tags: checks that the tag matches the version, builds, publishes the installer with its SHA-256
    and build provenance attestation, and uses the matching `CHANGELOG.md` section as release notes.
  - **CodeQL** for C# and workflows; **Dependabot** for NuGet and Actions.
- Legal: MIT `LICENSE`, `TERMS.md` (shown by the installer), `PRIVACY.md`, `THIRD-PARTY-NOTICES.md`, `SECURITY.md`.
