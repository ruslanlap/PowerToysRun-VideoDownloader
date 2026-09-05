; Inno Setup script for PowerToys Run VideoDownloader plugin.
; Compiled in CI: installer job downloads the platform zip artifact,
; extracts it to installer\payload\, then runs ISCC with Platform/Version.
; Install dir mirrors the plugin's expected location (CLAUDE.md):
;   %LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\VideoDownloader

#ifndef Platform
  #error "Define Platform=x64 or Platform=arm64 on the ISCC command line"
#endif

[Setup]
AppId={{B8F9B9F5-C3E4-4A8B-9F1F-2E3D4C5B6A7B}
AppName=VideoDownloader for PowerToys Run
AppVersion={#Version}
AppPublisher=ruslanlap
AppPublisherURL=https://github.com/ruslanlap
AppSupportURL=https://github.com/ruslanlap/PowerToysRun-VideoDownloader/issues
AppUpdatesURL=https://github.com/ruslanlap/PowerToysRun-VideoDownloader/releases
DefaultDirName={localappdata}\Microsoft\PowerToys\PowerToys Run\Plugins\VideoDownloader
DirExistsWarning=no
DisableProgramGroupPage=yes
OutputDir=installer\out
OutputBaseFilename=VideoDownloader-Setup-{#Version}-{#Platform}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
Uninstallable=yes
SetupLogging=yes

#if "arm64" == Platform
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Files]
Source: "installer\payload\VideoDownloader\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs
