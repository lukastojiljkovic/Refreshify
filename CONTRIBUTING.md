# Contributing to Refreshify

Thanks for helping. Bug reports, fixes and new tools are welcome.

## Before you start

- For a bug, open an issue with the steps to reproduce it. A **Get help** report from the app, with personal details
  hidden, usually has everything needed.
- For a new tool or a bigger change, open an issue first, so we can agree on the approach before you write code.
- For a security problem, follow [SECURITY.md](SECURITY.md) instead of opening an issue.

## Building

You need Windows 10 version 1809 or later and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
The installer also needs [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

```powershell
dotnet test --project tests/Refreshify.Core.Tests            # unit tests
dotnet run --project src/Refreshify -p:Platform=x64           # WinUI projects need an explicit platform
dotnet format Refreshify.slnx --verify-no-changes             # the CI format check
.\build.ps1                                                   # tests, publish, licenses and the installer
```

Tools that need administrator rights ask for approval in a UAC prompt the first time they run.

## Rules the code follows

- **English only**, in code, UI text, docs and commits.
- **No invented numbers.** Only measured values, such as the bytes a cleanup deleted, reach the UI or the docs.
- **No localized text in detection.** Detect results with exit codes, HRESULTs, `DISM /English`, `CBS.log` and the JSON
  that the PowerShell scripts emit, never with console text that changes with the Windows language.
- **The elevated helper stays narrow.** It runs only catalog tools that need administrator rights, with validated
  options, and starts executables by full path. Never pass a command line over the pipe.
- **Plain language.** Every tool says what it does in words anyone can follow, and troubleshooting tools say when to
  use them.
- **Tests first.** Core behavior changes come with a test that failed before the change.

## Adding a tool

1. Add it to `src/Refreshify.Core/Catalog/ToolCatalog.cs`: its category, description, the technical command, whether
   it needs administrator rights, whether *Run all* includes it by default, and its traits (for example, it takes a
   while or needs the internet).
2. Reuse a tool type from `src/Refreshify.Core/Tools`, such as `CommandTool`, `CleanupTool` or `ScriptTool`, or add one.
3. Map the failures you know how to explain or fix to `src/Refreshify.Core/Diagnostics/KnownIssues.cs`.
4. Add tests: the catalog tests check every entry, and parsers and error mapping get their own tests.

## Pull requests

- Keep a pull request to one change, and describe what it changes and how you tested it.
- The format check, the tests and the build must pass. CI runs them on every pull request.
- Add a line to the *Unreleased* section of [CHANGELOG.md](CHANGELOG.md) for anything users notice. Refreshify shows
  these lines in its update dialogs, so write each one as the user would describe the change, not as the code does:
  "Refreshify tells you when a new version is out", not "Added UpdateService".

By contributing, you agree that your contribution is licensed under the [MIT License](LICENSE).
