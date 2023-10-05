; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!
#define public Dependency_NoExampleSetup
#include "CodeDependencies.iss"

#define MyAppName "SMZ3 Cas' Randomizer"
#define MyAppPublisher "Vivelin"
#define MyAppURL "https://github.com/Vivelin/SMZ3Randomizer"
#define MyAppExeName "Randomizer.App.exe"
#define MyAppVersion GetStringFileInfo("src\Randomizer.App\bin\Release\net7.0-windows\publish\win-x64\" + MyAppExeName, "ProductVersion")

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{C3CC1ADA-86E9-4C12-94DA-741538A9B36B}
AppName={#MyAppName}
AppVersion="{#MyAppVersion}"
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\SMZ3 Cas Randomizer
DisableProgramGroupPage=yes
; Remove the following line to run in administrative install mode (install for all users.)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
Compression=lzma
SolidCompression=yes
WizardStyle=modern
OutputBaseFilename=SMZ3CasRandomizerSetup_{#MyAppVersion}     

[Code]
function InitializeSetup: Boolean;
begin
  Dependency_AddDotNet70Desktop;
  Dependency_AddDotNet70Asp;
  Result := True;
end;

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
Type: filesandordirs; Name: "{app}\Sprites"

[Files]
Source: "netcorecheck.exe"; Flags: dontcopy noencryption
Source: "netcorecheck_x64.exe"; Flags: dontcopy noencryption
Source: "src\Randomizer.Sprites\*"; DestDir: "{app}\Sprites"; Excludes: "\bin\*,obj\*,*.cs,*.csproj"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "src\Randomizer.App\bin\Release\net7.0-windows\publish\win-x64\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion; Check: Is64BitInstallMode;
Source: "src\Randomizer.App\bin\Release\net7.0-windows\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: Is64BitInstallMode;
Source: "src\Randomizer.App\bin\Release\net7.0-windows\publish\win-x86\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion; Check: "not Is64BitInstallMode";
Source: "src\Randomizer.App\bin\Release\net7.0-windows\publish\win-x86\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: "not Is64BitInstallMode";
Source: "src\Randomizer.Data\maps.json"; DestDir: "{localappdata}\SMZ3CasRandomizer"; Flags: comparetimestamp
Source: "src\Randomizer.SMZ3.Tracking\AutoTracking\LuaScripts\*"; DestDir: "{localappdata}\SMZ3CasRandomizer\AutoTrackerScripts"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "src\Randomizer.Data\Configuration\Yaml\*"; DestDir: "{localappdata}\SMZ3CasRandomizer\Configs"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

