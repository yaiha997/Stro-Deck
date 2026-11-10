#define AppName "Stro-Deck"
#define AppVersion "1.1.0"
#define SourceDir AddBackslash(SourcePath) + ".."

[Setup]
AppId={{F76C542E-0BAE-46B9-B102-723D3D7F39AE}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Stro-Deck
DefaultDirName={localappdata}\Programs\Stro-Deck
DefaultGroupName=Stro-Deck
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\Stro-Deck.exe
OutputDir=output
OutputBaseFilename=Stro-Deck-Setup
SetupIconFile={#SourceDir}\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "startup"; Description: "Start Stro-Deck when I sign in to Windows (in the system tray)"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\Stro-Deck.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\System.Runtime.WindowsRuntime.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\icons\*"; DestDir: "{app}\icons"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceDir}\backgrounds\*"; DestDir: "{app}\backgrounds"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Stro-Deck"; Filename: "{app}\Stro-Deck.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Stro-Deck"; Filename: "{app}\Stro-Deck.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Stro-Deck"; ValueData: """{app}\Stro-Deck.exe"" --tray"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Stro-Deck.exe"; Description: "Launch Stro-Deck"; Flags: postinstall nowait skipifsilent

[UninstallRun]
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v ""Stro-Deck"" /f"; Flags: runhidden waituntilterminated
