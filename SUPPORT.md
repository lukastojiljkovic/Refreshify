# Support

## Where to ask

| You want to | Go to |
| --- | --- |
| Report something that is broken | **Issues** — use the bug report form |
| Ask for a feature or a new tool | **Issues** — use the feature or tool request form |
| Report a security problem | **Not an issue.** See [SECURITY.md](SECURITY.md) |
| Read the licence or the terms | [LICENSE](LICENSE), [TERMS.md](TERMS.md) |
| Check what the app stores or sends | [PRIVACY.md](PRIVACY.md) |
| Build or contribute | [CONTRIBUTING.md](CONTRIBUTING.md) |

This project has no Discussions. Issues and the issue forms are the only channel.

## Before opening an issue

- Say which Refreshify version you are running (**Settings** > **About**) and your Windows edition, version and build
  (**Home** > **Windows**), for example `Windows 11 Pro, version 25H2, build 26200`.
- Say what you did, what you expected, and what happened instead.
- If a step failed, select **Get help** on it, leave **Hide personal details** on, copy the report and paste it into
  the issue.
- **Do not paste personal data.** With **Hide personal details** off, tool output and the report can contain your user
  name, computer name and file paths. Replace anything you would not publish with `[redacted]`.

## What is in scope

The desktop application, the installer and the source in this repository. Support covers the tools Refreshify runs,
the elevated helper, the run engine, the Get help report and the history.

Refreshify runs Windows' own maintenance tools and winget, so a problem in one of those tools themselves, or in an app
that winget updates, belongs with Microsoft or that app's publisher.

## What is not in scope

- Support for a modified build, or for a build from a fork.
- Support for Windows 10: Refreshify runs on Windows 10 version 1809 or later, but it has only been tested on
  Windows 11.
- Response-time guarantees. This is a project maintained by one person, with no support contract and no paid tier.
