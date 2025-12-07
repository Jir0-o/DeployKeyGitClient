#define MyAppVersion "1.2.3"

[Setup]
AppName=DeployKeyGitClient
AppVersion={#MyAppVersion}
DefaultDirName={pf}\DeployKeyGitClient
DefaultGroupName=DeployKeyGitClient
DisableProgramGroupPage=yes
OutputBaseFilename=DeployKeyGitClient_Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
Uninstallable=yes
CloseApplications=yes

[Files]
; Main admin app
Source: "publish\win-x64\DeployKeyGitClient.exe"; DestDir: "{app}"; Flags: ignoreversion
; Background agent (non-admin)
Source: "publish\win-x64\DeployKeyGitClientAgent.exe"; DestDir: "{app}"; Flags: ignoreversion
; Other files
Source: "publish\win-x64\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\DeployKeyGitClient"; Filename: "{app}\DeployKeyGitClient.exe"
Name: "{commondesktop}\DeployKeyGitClient"; Filename: "{app}\DeployKeyGitClient.exe"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "Create a &desktop icon"; Flags: unchecked

; Startup entry: only the AGENT runs on boot
[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "DeployKeyGitClientAgent"; \
    ValueData: """{app}\DeployKeyGitClientAgent.exe"""; Flags: uninsdeletevalue

[Run]
; After install, launch main app once (admin) if you want:
Filename: "{app}\DeployKeyGitClient.exe"; Description: "Launch DeployKeyGitClient"; Flags: shellexec postinstall; Verb: runas
