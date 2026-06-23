#define MyAppName "PC Wrapped"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "090TYPE"
#define MyAppExeName "PcWrapped.exe"

[Setup]
AppId={{7A2D5E3C-1B9F-4C6A-8E10-3F4B5C6D7E8F}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\PC Wrapped
DefaultGroupName=PC Wrapped
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=PCWrapped-Setup
SetupIconFile=..\src\PcWrapped\Assets\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\PcWrapped.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\PC Wrapped"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\PC Wrapped"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,PC Wrapped}"; Flags: nowait postinstall skipifsilent
