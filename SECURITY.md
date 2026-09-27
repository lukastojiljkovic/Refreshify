# Security Policy

## Supported versions

Security fixes go into the latest release. Update to it before reporting.

## Reporting a vulnerability

Please don't open a public issue. Report it privately through
[GitHub's private vulnerability reporting](https://github.com/lukastojiljkovic/Refreshify/security/advisories/new),
with the steps to reproduce it, the Refreshify and Windows versions, and the impact you expect.

You'll get a reply in the advisory. Once a fix is released, the advisory is published with credit to you, unless you
prefer otherwise.

## What's in scope

Refreshify runs tools as administrator, so these parts matter most:

- **The elevated helper.** Refreshify starts its own exe as administrator (`--worker`), which connects back to the
  window over a named pipe. The window creates the pipe with a random name as a single instance that only the current
  user and Administrators can open, with network access denied. Each side checks the other's process ID: the window
  accepts only the helper it started, and the helper only serves the window that started it. The helper runs only
  catalog tools that need administrator rights, with validated options, and starts executables by full path. Anything
  that lets another process or user run code or commands through the helper is a vulnerability.
- **The installer.** Refreshify installs to Program Files, where only administrators can replace the exe that gets
  elevated.
- **Files Refreshify deletes.** A cleanup that follows a junction or symbolic link out of the folder it cleans, or
  deletes files outside the documented locations, is a vulnerability.

Out of scope: problems in Windows tools, winget or the apps winget updates. Report those to Microsoft or to the app's
publisher.
