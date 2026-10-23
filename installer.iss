#ifndef PublishDir
#define PublishDir "bin\Release\net8.0-windows\win-x64\publish"
#endif

[Setup]
AppName=Windows Quick Scaler
AppVersion=0.9
DefaultDirName={autopf}\WindowsQuickScaler
DefaultGroupName=Windows Quick Scaler
OutputDir=.
OutputBaseFilename=WindowsQuickScaler_Setup_0.9
SetupIconFile=Assets\WindowsQuickScaler.ico
Compression=lzma
SolidCompression=yes
PrivilegesRequired=lowest

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Windows Quick Scaler"; Filename: "{app}\WindowsQuickScaler.exe"
Name: "{autodesktop}\Windows Quick Scaler"; Filename: "{app}\WindowsQuickScaler.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\WindowsQuickScaler.exe"; Description: "Launch Windows Quick Scaler now"; Flags: nowait postinstall skipifsilent