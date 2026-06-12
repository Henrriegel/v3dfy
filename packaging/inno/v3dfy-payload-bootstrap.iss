; v3dfy payload bootstrap installer.
; Compile with scripts\package-release-installers.ps1 so HelperExe and
; ManifestFile are generated for the selected release.

#ifndef MyAppVersion
#define MyAppVersion "0.1.0-preview.1"
#endif

#ifndef OutputDir
#define OutputDir "..\..\artifacts\installer"
#endif

#ifndef OutputBaseFilename
#define OutputBaseFilename "v3dfy-v" + MyAppVersion + "-web-setup"
#endif

#ifndef HelperExe
#error HelperExe must be defined.
#endif

#ifndef ManifestFile
#error ManifestFile must be defined.
#endif

#ifdef OfflineInstaller
#define InstallerMode "offline"
#define InstallerName "v3dfy Offline Setup"
#else
#define InstallerMode "web"
#define InstallerName "v3dfy Web Setup"
#endif

#define MyAppName "v3dfy"
#define MyAppPublisher "v3dfy"
#define MyAppExeName "V3dfy.App.exe"
#define HelperExeName "V3dfy.SetupHelper.exe"
#define ManifestFileName "payload-manifest.json"

[Setup]
AppId={{BEEFB38B-5640-49F7-94E8-57EBE668198F}
AppName={#MyAppName}
AppVerName={#MyAppName} {#MyAppVersion}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\v3dfy
DefaultGroupName=v3dfy
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
SetupIconFile=..\..\src\V3dfy.App\Assets\Branding\v3dfy-installer.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=no
RestartIfNeededByRun=no

[Files]
Source: "{#HelperExe}"; DestDir: "{tmp}"; DestName: "{#HelperExeName}"; Flags: dontcopy
Source: "{#ManifestFile}"; DestDir: "{tmp}"; DestName: "{#ManifestFileName}"; Flags: dontcopy

[Tasks]
Name: "desktopicon"; Description: "Create a Desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Icons]
Name: "{group}\v3dfy"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\v3dfy"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[UninstallDelete]
Type: filesandordirs; Name: "{app}\*"
Type: dirifempty; Name: "{app}"

[Code]
var
  RequirementsPage: TOutputMsgWizardPage;

function Quote(Value: String): String;
begin
  Result := '"' + Value + '"';
end;

function ReadHelperLog(LogPath: String): String;
var
  LogText: AnsiString;
begin
  Result := '';
  if LoadStringFromFile(LogPath, LogText) then
  begin
    if Length(LogText) > 4000 then
      Result := Copy(LogText, Length(LogText) - 3999, 4000)
    else
      Result := LogText;
  end;
end;

function BuildHelperArguments(LogPath: String): String;
begin
  Result :=
    '--ui' +
    ' --mode {#InstallerMode}' +
    ' --manifest ' + Quote(ExpandConstant('{tmp}\{#ManifestFileName}')) +
    ' --target-dir ' + Quote(ExpandConstant('{app}')) +
    ' --work-dir ' + Quote(ExpandConstant('{tmp}\v3dfy-payload-work')) +
    ' --log ' + Quote(LogPath);

  if '{#InstallerMode}' = 'offline' then
    Result := Result + ' --parts-dir ' + Quote(ExpandConstant('{src}'));
end;

function RunPayloadHelper(LogPath: String): String;
var
  ResultCode: Integer;
  HelperPath: String;
  HelperLog: String;
begin
  Result := '';
  HelperPath := ExpandConstant('{tmp}\{#HelperExeName}');

  WizardForm.StatusLabel.Caption := 'Preparing v3dfy payload...';

  if not Exec(HelperPath, BuildHelperArguments(LogPath), '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'Could not start the v3dfy setup helper.';
    exit;
  end;

  if ResultCode <> 0 then
  begin
    HelperLog := ReadHelperLog(LogPath);
    if HelperLog <> '' then
      Result := 'v3dfy payload installation failed.' + #13#10#13#10 + HelperLog
    else
      Result := 'v3dfy payload installation failed. Helper exit code: ' + IntToStr(ResultCode);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  LogPath: String;
begin
  ExtractTemporaryFile('{#HelperExeName}');
  ExtractTemporaryFile('{#ManifestFileName}');

  LogPath := ExpandConstant('{tmp}\v3dfy-setup-helper.log');
  if FileExists(LogPath) then
    DeleteFile(LogPath);

  Result := RunPayloadHelper(LogPath);
end;

procedure InitializeWizard();
begin
#ifdef OfflineInstaller
  RequirementsPage := CreateOutputMsgPage(
    wpWelcome,
    'Offline payload required',
    'Keep all payload .part files beside this setup EXE.',
    'This offline installer does not require internet or PowerShell during installation.' + #13#10#13#10 +
    'Before continuing, put v3dfy-v{#MyAppVersion}-win-x64-portable.zip.part01, .part02, and .part03 in the same folder as this setup EXE.' + #13#10#13#10 +
    'The installed app works offline after installation.');
#else
  RequirementsPage := CreateOutputMsgPage(
    wpWelcome,
    'Internet required',
    'This web installer downloads the v3dfy payload during installation.',
    'Continue only if this PC has internet access during installation.' + #13#10#13#10 +
    'The installer downloads the shared .part01, .part02, and .part03 payload files from the configured GitHub Release, verifies SHA256 checksums, rebuilds the ZIP, and installs v3dfy.' + #13#10#13#10 +
    'The installed app works offline after installation.');
#endif
end;
