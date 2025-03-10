#define MyAppName "Clipboard Translator"
#define MyAppVersion "1.0"
#define MyAppPublisher "Paulo Giavoni"
#define MyAppURL "https://github.com/DynaTools/Translator"
#define MyAppExeName "Translator.exe"
#define MyAppIcoName "translate_icon.ico"

[Setup]
AppId={{BB7C8DE2-38C9-4CF8-87DB-A32098FB8969}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={userdocs}\Translator AI
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputBaseFilename=TranslatorAI
Compression=lzma
SolidCompression=yes
WizardStyle=modern
OutputDir=C:\Users\ggiav\source\repos\Translator\Final
; Set the setup icon - this is the installer's icon
SetupIconFile=C:\Users\ggiav\source\repos\Translator\Resources\{#MyAppIcoName}
; Enable the uninstall icon in Add/Remove Programs
UninstallDisplayIcon={app}\Resources\{#MyAppIcoName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"
Name: "quicklaunchicon"; Description: "Create a &Quick Launch icon"; GroupDescription: "Additional icons:"; OnlyBelowVersion: 6.1

[Files]
Source: "C:\Users\ggiav\source\repos\Translator\bin\Debug\*.*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
; Make sure icon files are specifically included
Source: "C:\Users\ggiav\source\repos\Translator\Resources\{#MyAppIcoName}"; DestDir: "{app}\Resources"; Flags: ignoreversion

[Icons]
; Use explicit icon references for all shortcuts
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Resources\{#MyAppIcoName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Resources\{#MyAppIcoName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Resources\{#MyAppIcoName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Translator AI"; Flags: nowait postinstall

[Registry]
; Ensure the application icon is associated with the executable in Windows Registry
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Resources\{#MyAppIcoName}"