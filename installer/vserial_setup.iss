; ==============================================================================
; VSerial Virtual COM Port Manager - Inno Setup Modern Installer Script
; ==============================================================================

#define MyAppName "VSerial"
#ifndef MyAppVersion
  #define MyAppVersion "2.0.0"
#endif
#define MyAppPublisher "VSerial Project"
#define MyAppGuiExe "vserial-gui.exe"
#define MyAppCliExe "vserial.exe"

[Setup]
; Unique application GUID
AppId={{8D626EA8-CB54-BC41-363A-217881BEBA6E}
AppName={cm:MyAppFullName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\VSerial
DefaultGroupName={cm:MyAppGroupName}
DisableProgramGroupPage=yes

; Expose version information without warning when AppName uses constants
VersionInfoDescription=VSerial Virtual COM Port Manager Setup
VersionInfoProductName=VSerial
VersionInfoVersion={#MyAppVersion}

; Require Administrator privileges for driver injection & PATH configuration
PrivilegesRequired=admin

; Modern Fluent / Wizard UI style
WizardStyle=modern

; Enforce 64-bit OS execution only
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

; Installer output configuration
OutputDir=..\dist
OutputBaseFilename=VSerial-Setup-v{#MyAppVersion}-x64
SetupIconFile=..\docs\vserial.ico

; Highest LZMA2 solid compression
Compression=lzma2/ultra64
SolidCompression=yes

; Uninstaller configuration
UninstallDisplayIcon={app}\{#MyAppGuiExe}
UninstallDisplayName={cm:MyAppFullName} (v{#MyAppVersion})

; Language configuration: Force English as default selection (disable OS UI auto-detection & previous selection)
LanguageDetectionMethod=none
UsePreviousLanguage=no
UsePreviousTasks=no

[Languages]
; English as default primary language (ordered first)
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[CustomMessages]
; English localization (Default)
english.MyAppFullName=VSerial Virtual COM Port Manager
english.MyAppGroupName=VSerial Virtual COM Port Manager
english.CliConsoleName=VSerial Command Console
english.UninstallProgram=Uninstall VSerial Virtual COM Port Manager
english.TaskDesktopIcon=Create a desktop shortcut
english.TaskDesktopGroup=Additional shortcuts:
english.TaskAddToPath=Add VSerial CLI tool to system PATH environment variable
english.TaskPathGroup=System Integration:
english.RunGuiDesc=Launch VSerial Virtual COM Port Manager
english.AdminRequired=Installing VSerial Virtual COM Port driver requires Administrator privileges.%n%nPlease right-click the setup file and select "Run as administrator".
english.DriverInstallNotice=VSerial Virtual COM Port Driver Installation Notice (Exit Code: %1).%n%nPlease ensure Test Signing mode is enabled on Windows:%nRun "bcdedit /set testsigning on" in an Administrator terminal and reboot the system.%n%nAfter installation, you can also click [Install / Repair Driver] inside VSerial GUI at any time.

; Simplified Chinese localization
chinesesimplified.MyAppFullName=VSerial 虚拟串口管理器
chinesesimplified.MyAppGroupName=VSerial 虚拟串口管理器
chinesesimplified.CliConsoleName=VSerial 命令行控制台
chinesesimplified.UninstallProgram=卸载 VSerial 虚拟串口管理器
chinesesimplified.TaskDesktopIcon=创建桌面快捷方式 (VSerial 虚拟串口管理器)
chinesesimplified.TaskDesktopGroup=附加快捷方式:
chinesesimplified.TaskAddToPath=将 VSerial 命令行工具添加到系统 PATH 环境变量 (可在任意终端直接运行 vserial 命令)
chinesesimplified.TaskPathGroup=系统环境集成:
chinesesimplified.RunGuiDesc=启动 VSerial 虚拟串口管理器
chinesesimplified.AdminRequired=安装 VSerial 虚拟串口驱动需要系统管理员权限。%n%n请右键点击安装程序，选择“以管理员身份运行”。
chinesesimplified.DriverInstallNotice=VSerial 虚拟串口驱动预安装提示 (退出码: %1)。%n%n请确保当前 Windows 系统已开启测试签名模式 (Test Signing)：%n以管理员身份在终端执行 "bcdedit /set testsigning on" 并重启系统。%n%n安装完成后，您也可以随时打开 VSerial 管理界面点击【安装/修复驱动】。

[Tasks]
Name: "desktopicon"; Description: "{cm:TaskDesktopIcon}"; GroupDescription: "{cm:TaskDesktopGroup}"
Name: "addtopath"; Description: "{cm:TaskAddToPath}"; GroupDescription: "{cm:TaskPathGroup}"

[Files]
; Package application binaries, single-file hosts, and driver package (0 pdb files)
Source: "..\build\staging\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu shortcuts
Name: "{group}\{cm:MyAppFullName}"; Filename: "{app}\{#MyAppGuiExe}"
Name: "{group}\{cm:CliConsoleName}"; Filename: "cmd.exe"; Parameters: "/K cd /d ""{app}"" & {#MyAppCliExe} --help"
Name: "{group}\{cm:UninstallProgram}"; Filename: "{uninstallexe}"

; Desktop shortcut
Name: "{autodesktop}\{cm:MyAppFullName}"; Filename: "{app}\{#MyAppGuiExe}"; Tasks: desktopicon

[Registry]
Root: HKLM; Subkey: "Software\VSerial"; ValueType: string; ValueName: "InstallLanguage"; ValueData: "{language}"; Flags: uninsdeletevalue

[Run]
; Launch GUI manager after successful installation
Filename: "{app}\{#MyAppGuiExe}"; Description: "{cm:RunGuiDesc}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Clean up all virtual COM port hardware nodes prior to removal
Filename: "{app}\{#MyAppCliExe}"; Parameters: "delete all"; WorkingDir: "{app}"; RunOnceId: "VSerialPairCleanup"; Flags: runhidden
; Deregister driver from Windows DriverStore
Filename: "{app}\{#MyAppCliExe}"; Parameters: "driver uninstall"; WorkingDir: "{app}"; RunOnceId: "VSerialDriverCleanup"; Flags: runhidden

[Code]
// -----------------------------------------------------------------------------
// System Environment PATH Management
// -----------------------------------------------------------------------------
const EnvironmentKey = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';

procedure AddDirToPath(DirName: string);
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', OrigPath) then
    OrigPath := '';

  if Pos(';' + Uppercase(DirName) + ';', ';' + Uppercase(OrigPath) + ';') = 0 then
  begin
    if (Length(OrigPath) > 0) and (OrigPath[Length(OrigPath)] <> ';') then
      OrigPath := OrigPath + ';';
    OrigPath := OrigPath + DirName;
    RegWriteStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', OrigPath);
  end;
end;

procedure RemoveDirFromPath(DirName: string);
var
  OrigPath: string;
  P, L: Integer;
begin
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', OrigPath) then
    Exit;

  P := Pos(';' + Uppercase(DirName) + ';', ';' + Uppercase(OrigPath) + ';');
  if P > 0 then
  begin
    L := Length(DirName);
    if P = 1 then
      Delete(OrigPath, 1, L + 1)
    else
      Delete(OrigPath, P, L + 1);
    RegWriteStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', OrigPath);
  end;
end;

// -----------------------------------------------------------------------------
// Post-install step: PATH injection, security trust & driver pre-installation
// -----------------------------------------------------------------------------
procedure CurStepChanged(CurStep: TSetupStep);
var
  AppPath: string;
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    AppPath := ExpandConstant('{app}');

    // 1. Add tool directory to system PATH if selected
    if WizardIsTaskSelected('addtopath') then
    begin
      AddDirToPath(AppPath);
    end;

    // 2. Pre-install driver package and certificate via CLI tool
    if not Exec(ExpandConstant('{app}\{#MyAppCliExe}'), 'driver install', AppPath, SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
    begin
      MsgBox(FmtMessage(CustomMessage('DriverInstallNotice'), [IntToStr(ResultCode)]), mbInformation, MB_OK);
    end;
  end;
end;

// -----------------------------------------------------------------------------
// Post-uninstall step: Clean up system PATH
// -----------------------------------------------------------------------------
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RemoveDirFromPath(ExpandConstant('{app}'));
  end;
end;

// -----------------------------------------------------------------------------
// Initialization: Ensure setup runs under Administrator privileges
// -----------------------------------------------------------------------------
function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsAdmin then
  begin
    MsgBox(CustomMessage('AdminRequired'), mbError, MB_OK);
    Result := False;
    Exit;
  end;
end;
