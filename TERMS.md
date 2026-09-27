# Refreshify Terms of Use

Last updated: 27 September 2026

These terms apply to the Refreshify application and installer published at
https://github.com/lukastojiljkovic/Refreshify. Refreshify's source code is licensed under the MIT License (LICENSE).
Nothing in these terms limits the rights the MIT License gives you for the source code.

By installing or using Refreshify, you agree to these terms. If you don't agree, don't install or use it.

## 1. What Refreshify does

Refreshify runs maintenance tools that come with Windows, and winget, the Windows package manager, and shows their
results. Depending on the tools you run, it changes your PC. For example, it:

- deletes temporary files, caches, error reports and the contents of the Recycle Bin
- repairs Windows system files and the component store
- installs Windows updates and updates your apps
- resets network settings, the Windows Update cache, the font cache, the print queue and the search index
- restarts File Explorer and Windows services

Refreshify lists what a run will do and asks before it starts. By default, it creates a System Restore point first.

## 2. Your responsibility

- Save your work and close your apps before you start a run. Some tools restart File Explorer or close apps while they
  update them.
- Keep backups of anything you might need. Files that Refreshify deletes, including the contents of the Recycle Bin,
  can't be restored by Refreshify, and a restore point doesn't bring them back.
- Only run Refreshify on PCs you're allowed to maintain. On a PC managed by an organization, follow its policies.
- You're responsible for complying with the laws and agreements that apply to you.

## 3. Limits of Refreshify

- Refreshify relies on the tools Windows provides and on the services they connect to, such as Windows Update. It
  can't guarantee that a tool fixes a problem, and it can't fix problems those tools can't fix.
- Updates can change how Windows and your apps behave.
- A restore point can undo changes to system files, settings and installed programs. It doesn't cover personal files.

## 4. Third-party tools and services

- **winget.** *App updates* runs `winget upgrade --all` with `--accept-source-agreements` and
  `--accept-package-agreements`. When you run it, you accept the agreements of the winget sources on your PC, such as
  the Microsoft Store source, and the license terms of every app it updates. Refreshify doesn't choose, host or check
  these apps: winget updates the apps that are already installed, from their publishers.
- **Microsoft services.** Windows Update, Microsoft Defender Antivirus and the Windows time service are Microsoft
  services that come with Windows. Your use of them is governed by Microsoft's terms.

## 5. No warranty

Refreshify is provided free of charge, "as is" and "as available", without warranty of any kind, express or implied,
including the warranties of merchantability, fitness for a particular purpose and non-infringement. You bear the entire
risk of using Refreshify.

## 6. Limitation of liability

To the fullest extent permitted by applicable law, the author and contributors aren't liable for any damages arising
from the use of, or inability to use, Refreshify. This includes loss of data, loss of profits, business interruption
and any direct, indirect, incidental, special or consequential damages, even if they were advised of their possibility.
Some jurisdictions don't allow certain liability to be excluded or limited, for example liability for intent or gross
negligence. Where that's the case, liability is limited to the extent the law allows.

## 7. Third-party components

The installer includes the Microsoft .NET runtime and the Microsoft Windows App SDK, which are licensed under their own
terms. Their license files are installed in the `licenses` folder next to Refreshify.exe and listed in
THIRD-PARTY-NOTICES.md. By using Refreshify, you also agree to those terms. Microsoft and the other licensors provide
their components "as is" and have no liability to you in connection with Refreshify.

Refreshify isn't affiliated with or endorsed by Microsoft. Windows is a trademark of the Microsoft group of companies.

## 8. Privacy

Refreshify doesn't collect or send personal data. The tools it runs connect to Microsoft and to app publishers as
described in PRIVACY.md.

## 9. Changes

These terms may change in future releases. The terms included with a release apply to that release.

## Contact

Open an issue at https://github.com/lukastojiljkovic/Refreshify/issues.
