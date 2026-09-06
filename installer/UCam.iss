#ifndef MyAppVersion
  #define MyAppVersion "3.0.1"
#endif
#ifndef SourceRoot
  #define SourceRoot "..\release-dist\installer-staging\app"
#endif
#ifndef OutputDir
  #define OutputDir "..\release-dist\installer"
#endif

#define CameraClsid "{{87493A16-2CF8-4781-9A51-8B0674F10010}"
#define PropertiesClsid "{{87493A16-2CF8-4781-9A51-8B0674F10011}"
#define CameraCategory "{{860BB310-5D01-11D0-BD3B-00A0C911CE86}"

[Setup]
AppId={{B665EAAD-CE41-43EA-BCEE-3A7207A87C52}
AppName=U镜
AppVerName=U镜 {#MyAppVersion}
AppVersion={#MyAppVersion}
AppPublisher=wtgjx
AppPublisherURL=https://github.com/wtgjx/usb-phone-camera-no-watermark
AppSupportURL=https://github.com/wtgjx/usb-phone-camera-no-watermark/issues
AppUpdatesURL=https://github.com/wtgjx/usb-phone-camera-no-watermark/releases
DefaultDirName={localappdata}\Programs\UCam
DefaultGroupName=U镜
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=UCam-Setup-win64
SetupIconFile=..\assets\ucam.ico
UninstallDisplayIcon={app}\U镜.exe
LicenseFile=..\LICENSE
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany=wtgjx
VersionInfoDescription=U镜 Windows 安装程序
VersionInfoProductName=UCam
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "chinesesimp"; MessagesFile: ".\ChineseSimplified.isl"

[Files]
Source: "{#SourceRoot}\U镜.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\PhoneCameraNative.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\PhoneUsbCameraFilter.dll"; DestDir: "{app}\components"; Flags: ignoreversion
Source: "{#SourceRoot}\scrcpy\*"; DestDir: "{app}\scrcpy"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\LICENSE"; DestDir: "{app}\licenses"; Flags: ignoreversion
Source: "{#SourceRoot}\THIRD_PARTY_NOTICES.md"; DestDir: "{app}\licenses"; Flags: ignoreversion
Source: "{#SourceRoot}\third_party\UnityCapture\*"; DestDir: "{app}\licenses\UnityCapture"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: ".\ChineseSimplified.LICENSE"; DestDir: "{app}\licenses"; DestName: "Inno-Setup-Chinese-Translation-LICENSE"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\U镜\U镜"; Filename: "{app}\U镜.exe"; WorkingDir: "{app}"; Comment: "把 Android 手机变成 USB 摄像头"
Name: "{autodesktop}\U镜"; Filename: "{app}\U镜.exe"; WorkingDir: "{app}"; Comment: "把 Android 手机变成 USB 摄像头"

[Registry]
Root: HKCU64; Subkey: "Software\Classes\CLSID\{#CameraClsid}"; ValueType: string; ValueName: "PhoneUsbCamera.Owner"; ValueData: "PhoneUsbCamera.v3"; Flags: uninsdeletekey
Root: HKCU64; Subkey: "Software\Classes\CLSID\{#CameraClsid}\InprocServer32"; ValueType: string; ValueName: ""; ValueData: "{app}\components\PhoneUsbCameraFilter.dll"
Root: HKCU64; Subkey: "Software\Classes\CLSID\{#CameraClsid}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"
Root: HKCU64; Subkey: "Software\Classes\CLSID\{#PropertiesClsid}"; ValueType: string; ValueName: "PhoneUsbCamera.Owner"; ValueData: "PhoneUsbCamera.v3"; Flags: uninsdeletekey
Root: HKCU64; Subkey: "Software\Classes\CLSID\{#PropertiesClsid}\InprocServer32"; ValueType: string; ValueName: ""; ValueData: "{app}\components\PhoneUsbCameraFilter.dll"
Root: HKCU64; Subkey: "Software\Classes\CLSID\{#PropertiesClsid}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"
Root: HKCU64; Subkey: "Software\Classes\CLSID\{#CameraCategory}\Instance\{#CameraClsid}"; ValueType: string; ValueName: "PhoneUsbCamera.Owner"; ValueData: "PhoneUsbCamera.v3"; Flags: uninsdeletekey
Root: HKCU64; Subkey: "Software\Classes\CLSID\{#CameraCategory}\Instance\{#CameraClsid}"; ValueType: string; ValueName: "CLSID"; ValueData: "{#CameraClsid}"
Root: HKCU64; Subkey: "Software\Classes\CLSID\{#CameraCategory}\Instance\{#CameraClsid}"; ValueType: string; ValueName: "FriendlyName"; ValueData: "Phone USB Camera"
Root: HKCU64; Subkey: "Software\Classes\CLSID\{#CameraCategory}\Instance\{#CameraClsid}"; ValueType: string; ValueName: "DevicePath"; ValueData: "phone-usb-camera:v3"

[Run]
Filename: "{app}\U镜.exe"; Description: "启动 U镜"; Flags: nowait postinstall skipifsilent

[Code]
const
  RegistrationOwner = 'PhoneUsbCamera.v3';

function RegistrationIsOwnedOrAbsent(const KeyName: String): Boolean;
var
  ExistingOwner: String;
begin
  Result := True;
  if RegKeyExists(HKCU64, KeyName) then
  begin
    Result := RegQueryStringValue(HKCU64, KeyName,
      'PhoneUsbCamera.Owner', ExistingOwner) and
      (ExistingOwner = RegistrationOwner);
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result :=
    RegistrationIsOwnedOrAbsent('Software\Classes\CLSID\{87493A16-2CF8-4781-9A51-8B0674F10010}') and
    RegistrationIsOwnedOrAbsent('Software\Classes\CLSID\{87493A16-2CF8-4781-9A51-8B0674F10011}') and
    RegistrationIsOwnedOrAbsent('Software\Classes\CLSID\{860BB310-5D01-11D0-BD3B-00A0C911CE86}\Instance\{87493A16-2CF8-4781-9A51-8B0674F10010}');

  if not Result then
    MsgBox('检测到相同标识但不属于 U镜的摄像头注册。为避免覆盖其他软件，安装已停止。',
      mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  RegisteredDll: String;
begin
  if CurStep = ssPostInstall then
  begin
    if (not RegQueryStringValue(HKCU64,
      'Software\Classes\CLSID\{87493A16-2CF8-4781-9A51-8B0674F10010}\InprocServer32',
      '', RegisteredDll)) or
      (CompareText(RegisteredDll,
        ExpandConstant('{app}\components\PhoneUsbCameraFilter.dll')) <> 0) or
      (not FileExists(RegisteredDll)) then
      RaiseException('Phone USB Camera 注册校验失败，请查看安装日志。');
  end;
end;
