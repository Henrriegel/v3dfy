; Initial v3dfy Inno Setup template.
; Adjust metadata and add a real icon after release assets are available.

#define MyAppName "v3dfy"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "v3dfy"
#define MyAppExeName "V3dfy.App.exe"

[Setup]
AppId={{BEEFB38B-5640-49F7-94E8-57EBE668198F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\v3dfy
DefaultGroupName=v3dfy
DisableProgramGroupPage=yes
OutputDir=..\..\artifacts\installer
OutputBaseFilename=v3dfy-setup-{#MyAppVersion}-win-x64
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Add SetupIconFile after a production icon is available.

[Files]
; The publish script prepares the complete offline bundle in this directory.
Source: "..\..\artifacts\publish\v3dfy-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\v3dfy"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch v3dfy"; Flags: nowait postinstall skipifsilent
