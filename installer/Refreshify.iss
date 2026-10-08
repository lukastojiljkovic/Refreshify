; Inno Setup script for Refreshify. Built by build.ps1 from the self-contained publish output.

#ifndef AppVersion
  #define AppVersion "2.0.0"
#endif

#define AppName "Refreshify"
#define AppExeName "Refreshify.exe"
#define AppPublisher "Luka Stojiljkovic"
#define AppUrl "https://github.com/lukastojiljkovic/Refreshify"
#define PublishDir "..\artifacts\publish\win-x64"

[Setup]
AppId={{C702E743-6E7D-4276-9FDB-DB7B6FFC181A}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DisableDirPage=auto
DisableProgramGroupPage=yes
; Users must accept the Terms of Use, which also cover the redistributed Microsoft components.
LicenseFile=..\TERMS.md
OutputDir=..\artifacts\installer
OutputBaseFilename={#AppName}-{#AppVersion}-Setup
SetupIconFile=..\src\Refreshify\Assets\Refreshify.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
; Refreshify starts its own exe as administrator, so it must live where only administrators can replace it.
PrivilegesRequired=admin
; Also closes the elevated helper, which is the same exe.
CloseApplications=yes
CloseApplicationsFilter={#AppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\TERMS.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PRIVACY.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
; Removes the reminder task, the notification registration, the settings and the history. They belong to the account
; that approves the uninstaller, which is the signed-in user in the usual case of an administrator account.
Filename: "{app}\{#AppExeName}"; Parameters: "--uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveUserData"
