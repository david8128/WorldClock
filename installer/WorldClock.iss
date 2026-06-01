; WorldClock — Inno Setup installer script
; Requires Inno Setup 6 or 7: https://jrsoftware.org/isdl.php
;
; Build via:
;   scripts\Build-WindowsInstaller.ps1
; or manually (ISCC resolves PublishDir as a relative path):
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\WorldClock.iss
;   "C:\Program Files (x86)\Inno Setup 7\ISCC.exe" installer\WorldClock.iss

#define AppName      "WorldClock"
; AppVersion is injected by Build-WindowsInstaller.ps1 via /DAppVersion=<version>
; (sourced from the VERSION file at the repo root).
; Fall back to the value below for manual ISCC runs.
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "WorldClock Team"
#define AppExeName   "WorldClock.exe"
; PublishDir is injected as an absolute path by Build-WindowsInstaller.ps1
; via /DPublishDir=...  Fall back to the relative path for manual ISCC runs.
#ifndef PublishDir
  #define PublishDir "..\publish\win-x64"
#endif

; ── Setup metadata ────────────────────────────────────────────────────────────
[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/worldclock
AppSupportURL=https://github.com/worldclock/issues
AppUpdatesURL=https://github.com/worldclock/releases

; Install into %LocalAppData%\Programs (no UAC elevation needed)
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Output
OutputDir=Output
OutputBaseFilename=WorldClockSetup
SetupIconFile=..\WorldClock\Images\Logo.ico
UninstallDisplayIcon={app}\{#AppExeName}

; Compression
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; UI
WizardStyle=modern
WizardSizePercent=120
ShowLanguageDialog=no

; 64-bit Windows required (.NET 8 WPF)
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

; ── Languages ─────────────────────────────────────────────────────────────────
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; ── Install tasks ─────────────────────────────────────────────────────────────
[Tasks]
Name: "desktopicon";  Description: "{cm:CreateDesktopIcon}";          GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon";  Description: "Launch WorldClock with Windows";   GroupDescription: "Startup:";            Flags: unchecked

; ── Files ─────────────────────────────────────────────────────────────────────
[Files]
; All files from the self-contained publish output
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; ── Icons / shortcuts ─────────────────────────────────────────────────────────
[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";     Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}";     Filename: "{app}\{#AppExeName}"; Tasks: startupicon

; ── Post-install run ──────────────────────────────────────────────────────────
[Run]
Filename: "{app}\{#AppExeName}"; \
    Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; \
    Flags: nowait postinstall skipifsilent

; ── Registry (App Paths so users can type "WorldClock" in Run dialog) ─────────
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#AppExeName}"; \
    ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#AppExeName}"; \
    ValueType: string; ValueName: "Path"; ValueData: "{app}"

; ── Uninstall: remove user settings (optional, prompted) ─────────────────────
[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  SettingsDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    SettingsDir := ExpandConstant('{localappdata}\WorldClock');
    if DirExists(SettingsDir) then
    begin
      if MsgBox('Remove WorldClock settings and saved data?' + #13#10 +
                '(' + SettingsDir + ')',
                mbConfirmation, MB_YESNO) = IDYES then
        DelTree(SettingsDir, True, True, True);
    end;
  end;
end;
