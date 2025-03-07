#define MyAppName "Translator AI - Ctrl + C"
#define MyAppVersion "1.0"
#define MyAppPublisher "Paulo Giavoni"
#define MyAppURL "https://github.com/DynaTools/Translator"
#define MyAppExeName "Translator.exe"

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

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"

[Files]
Source: "C:\Users\ggiav\source\repos\Translator\bin\Debug\*.*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Translator AI"; Flags: nowait postinstall

