# Refreshify Privacy Statement

Last updated: 27 September 2026

Refreshify doesn't collect or send personal data to its author or anyone else. It has no accounts, telemetry,
analytics, crash reporting or ads.

## What stays on your PC

- **Settings** are stored in the Windows registry under `HKEY_CURRENT_USER\Software\Refreshify`.
- **Run history** is stored in `%LOCALAPPDATA%\Refreshify\History` (the last 50 runs), and the raw output of the tools in
  `%LOCALAPPDATA%\Refreshify\Logs`. Tool output can contain file paths, your user name and your computer name. Delete
  both in **Settings** > **History and logs**.
- **The reminder**, if you turn it on, adds a scheduled task named `Refreshify reminder-<your account's SID>`, registers
  Refreshify as a notification sender under `HKEY_CURRENT_USER\Software\Classes\AppUserModelId\LukaStojiljkovic.Refreshify`,
  and adds a `refreshify:` link under `HKEY_CURRENT_USER\Software\Classes\refreshify` that opens Refreshify when you
  click the notification. Turning the reminder off removes the task.

Uninstalling Refreshify removes all of the above for the account that runs the uninstaller.

## Get help reports

**Get help** builds a report about a failed step on your PC: your Windows version, display language, the run's results
and the tool output. Refreshify doesn't send it anywhere. When you copy it and paste it into an AI assistant or anywhere
else, that service's privacy policy applies to it. **Hide personal details**, on by default, replaces your user name,
computer name and profile folder with placeholders.

## Connections made by the tools Refreshify runs

Refreshify itself doesn't connect to the internet. Some of the Windows tools it runs do, as they do when you run them
yourself:

- **Windows Update** and **System image repair** (DISM) download from Microsoft.
- **Defender definitions** and the **Defender quick scan** use Microsoft Defender Antivirus, which can send data to
  Microsoft according to its settings.
- **Time sync** contacts the time server set in Windows, `time.windows.com` by default.
- **App updates** runs winget, which reads its sources, such as Microsoft's winget and Microsoft Store sources, and
  downloads updates from the apps' publishers.

These connections are governed by the Microsoft Privacy Statement at https://aka.ms/privacy and by the publishers'
privacy policies.

## Other

- **Links** to GitHub open in your web browser, where GitHub's privacy statement applies.
- **Microsoft components.** The Microsoft Windows App SDK included with Refreshify may collect diagnostic information
  as described in its license terms (in the `licenses` folder) and the Microsoft Privacy Statement at
  https://aka.ms/privacy. Any such data goes to Microsoft, not to Refreshify's author.

## Contact

Open an issue at https://github.com/lukastojiljkovic/Refreshify/issues.
