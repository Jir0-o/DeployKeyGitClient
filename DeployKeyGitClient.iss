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
Source: "publish\win-x64\DeployKeyGitClient.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\win-x64\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\DeployKeyGitClient"; Filename: "{app}\DeployKeyGitClient.exe"
Name: "{commondesktop}\DeployKeyGitClient"; Filename: "{app}\DeployKeyGitClient.exe"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "Create a &desktop icon"; Flags: unchecked

; IMPORTANT: machine-wide startup, visible in Task Manager → Startup
[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "DeployKeyGitClient"; \
    ValueData: """{app}\DeployKeyGitClient.exe"""; Flags: uninsdeletevalue

[Run]
Filename: "{app}\DeployKeyGitClient.exe"; Description: "Launch DeployKeyGitClient"; Flags: shellexec postinstall; Verb: runas
