#ifndef AppVersion
#define AppVersion "0.3.2"
#endif
#ifndef PublishDir
#define PublishDir "..\artifacts\publish"
#endif
#ifdef TestInstall
#define ProductName "AxiWidgets Installer Test"
#define ProductId "AxiWidgets.InstallerTest"
#define StartupValue "AxiWidgets.InstallerTest"
#else
#define ProductName "AxiWidgets"
#define ProductId "AxiWidgets"
#define StartupValue "AxiWidgets"
#endif
[Setup]
AppId={#ProductId}
AppName={#ProductName}
AppVersion={#AppVersion}
AppPublisher=AxiWidgets contributors
DefaultDirName={localappdata}\Programs\{#ProductName}
DefaultGroupName={#ProductName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22621
OutputDir=..\artifacts\installer
OutputBaseFilename=AxiWidgets-Setup-{#AppVersion}-x64
SetupIconFile=..\src\AxiWidgets\Assets\AxiWidgets.ico
UninstallDisplayIcon={app}\AxiWidgets.exe
LicenseFile=..\LICENSE
WizardStyle=modern dynamic
WizardResizable=no
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
UsePreviousTasks=yes
UsePreviousAppDir=yes
[Languages]
Name: english; MessagesFile: compiler:Default.isl
Name: russian; MessagesFile: compiler:Languages\Russian.isl
[Tasks]
Name: desktopicon; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked
Name: autostart; Description: "{cm:AutoStart}"; Flags: unchecked
[CustomMessages]
english.AutoStart=Start AxiWidgets when I sign in to Windows
russian.AutoStart=Запускать AxiWidgets при входе в Windows
english.Launch=Launch AxiWidgets
russian.Launch=Запустить AxiWidgets
english.CloseApp=Please exit AxiWidgets from its tray menu and retry.
russian.CloseApp=Закройте AxiWidgets через меню в трее и повторите попытку.
[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,.local\*"
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{userprograms}\{#ProductName}"; Filename: "{app}\AxiWidgets.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\{#ProductName}"; Filename: "{app}\AxiWidgets.exe"; WorkingDir: "{app}"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#StartupValue}"; ValueData: """{app}\AxiWidgets.exe"" --startup"; Tasks: autostart
[Run]
Filename: "{app}\AxiWidgets.exe"; Description: "{cm:Launch}"; Flags: nowait postinstall skipifsilent
[Code]
procedure InitializeWizard;
begin
  if RegValueExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#StartupValue}') then
    WizardSelectTasks('autostart');
end;
function StopRunningApp: Boolean;
var Code, I: Integer;
begin
  Result := True;
  if not CheckForMutexes('Local\AxiWidgets.Manager') then exit;
  if FileExists(ExpandConstant('{app}\AxiWidgets.exe')) then
    Exec(ExpandConstant('{app}\AxiWidgets.exe'), '--shutdown', '', SW_HIDE, ewWaitUntilTerminated, Code);
  for I := 1 to 60 do begin
    if not CheckForMutexes('Local\AxiWidgets.Manager') then exit;
    Sleep(50);
  end;
  Result := False;
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not StopRunningApp then Result := CustomMessage('CloseApp');
end;
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and not WizardIsTaskSelected('autostart') then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#StartupValue}');
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then begin
    if not StopRunningApp then RaiseException(CustomMessage('CloseApp'));
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#StartupValue}');
  end;
end;
