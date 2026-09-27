# Refreshify

Native Windows 11 maintenance utility (WinUI 3). It runs built-in Windows tools (DISM, SFC, chkdsk, cache cleanup,
network resets, Windows Update, winget, Defender) as hidden processes and shows everything in the GUI. The design is in
[docs/DESIGN.md](docs/DESIGN.md).

## Commands

```powershell
dotnet test tests/Refreshify.Core.Tests                     # unit tests (Microsoft.Testing.Platform, xUnit v3)
dotnet build src/Refreshify -c Release -p:Platform=x64      # WinUI projects need an explicit Platform
dotnet run --project src/Refreshify -p:Platform=x64
dotnet format Refreshify.slnx --verify-no-changes           # the CI lint step
.\build.ps1                                                 # tests, publish, licenses, installer
```

## Layout

- `src/Refreshify.Core`: all logic, no UI. Tools (`Tools/`), the catalog (`Catalog/ToolCatalog.cs`), process runner
  and Windows helpers (`Platform/`), known issues and the LLM report (`Diagnostics/`), run engine and history
  (`Engine/`), elevated worker protocol (`Worker/`), PowerShell scripts embedded as resources (`Scripts/`).
- `src/Refreshify`: WinUI 3 app. `Program.cs` also dispatches `--worker`, `--reminder` and `--uninstall`.
- `tests/Refreshify.Core.Tests`: unit tests. Tests tagged `Category=System` touch the real system and are excluded in CI.

## Rules

- English only: code, UI text, docs, commits.
- Never show invented numbers. Only measured values (bytes deleted, updates installed) reach the UI or docs.
- Detection must not depend on localized console text: use exit codes, HRESULTs, `DISM /English`, `CBS.log`, and
  JSON emitted by the PowerShell scripts.
- The elevated worker runs only catalog tools whose context is Admin, with validated options, and starts executables
  by full path. Never pass command lines over the pipe.
- Plain-language copy for every tool: what it does and, for troubleshooting tools, when to use it.
- Git: plain `git commit` with the global identity, no co-author trailers or generated-by footers. Everything green
  before pushing.
