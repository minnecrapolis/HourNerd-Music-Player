; Compile through Build-Windows-Installer.ps1, using the validated public payload.
#ifndef PayloadDir
  #error PayloadDir must be supplied by Build-Windows-Installer.ps1
#endif
#ifndef InstallerOutput
  #error InstallerOutput must be supplied by Build-Windows-Installer.ps1
#endif
#ifndef ProductVersion
  #define ProductVersion "1.1.3"
#endif

[Setup]
AppId=HourNerdMusicPlayer
AppName=HourNerd Music Player
AppVersion={#ProductVersion}
AppPublisher=Chris Dahlkvist / HourNerd
DefaultDirName={localappdata}\Programs\HourNerd Music Player
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#InstallerOutput}
OutputBaseFilename=HourNerd-Music-Player-{#ProductVersion}-Setup
SetupIconFile=..\HourNerdMusicPlayer\Resources\main_icon.ico
UninstallDisplayName=HourNerd Music Player
UninstallDisplayIcon={app}\HourNerdMusicPlayer.exe
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
LicenseFile={#PayloadDir}\LICENSE
SetupLogging=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: checkedonce

[Dirs]
Name: "{localappdata}\HourNerd Music Player\Working\Public"

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "Install-HourNerdMusicPlayer.ps1,Install-HourNerdMusicPlayer.cmd"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\HourNerd Music Player"; Filename: "{app}\HourNerdMusicPlayer.exe"; WorkingDir: "{localappdata}\HourNerd Music Player\Working\Public"
Name: "{userdesktop}\HourNerd Music Player"; Filename: "{app}\HourNerdMusicPlayer.exe"; WorkingDir: "{localappdata}\HourNerd Music Player\Working\Public"; Tasks: desktopicon

[Run]
Filename: "{app}\HourNerdMusicPlayer.exe"; WorkingDir: "{localappdata}\HourNerd Music Player\Working\Public"; Description: "Launch HourNerd Music Player"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
  InstallerType: String;
begin
  if RegQueryStringValue(HKCU, 'Software\HourNerd\Music Player\Installer', 'Type', InstallerType) then
    if InstallerType = 'MSI' then
    begin
      MsgBox('Uninstall the existing MSI installation before using the EXE installer. Your saved accounts and settings will be preserved.', mbInformation, MB_OK);
      Result := False;
      Exit;
    end;
  Result := IsDotNetInstalled(net481, 0);
  if not Result then
    MsgBox('Install the Microsoft .NET Framework 4.8.1 runtime, then run this installer again.' + #13#10 +
      'https://dotnet.microsoft.com/en-us/download/dotnet-framework/net481', mbInformation, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  OldLocation: String;
begin
  if CurStep = ssPostInstall then
  begin
    { Replace only this product's old PowerShell-installed entry at the same location.
      Inno Setup owns the new HourNerdMusicPlayer_is1 entry and its own uninstaller. }
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\HourNerdMusicPlayer',
      'InstallLocation', OldLocation) then
      if CompareText(RemoveBackslashUnlessRoot(OldLocation), RemoveBackslashUnlessRoot(ExpandConstant('{app}'))) = 0 then
        RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\HourNerdMusicPlayer');
  end;
end;

// No wildcard uninstall deletion: user profiles, credentials and work folders are preserved.
