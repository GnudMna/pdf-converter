; PDF Converter — Inno Setup スクリプト
; ビルド例:
;   iscc /DAppVersion=x.x.x /DPlatform=x64 /DSourceDir="..\src\PdfConverter\bin\x64\Release" PdfConverter.iss
;
; AppVersion は必須です。通常は build-installer.ps1 が AssemblyInfo.cs から取得して渡します。
;
; 管理者権限を要求しないユーザー単位インストール（PrivilegesRequired=lowest）。
; UAC の昇格ダイアログ（「不明な発行元」）を表示させない構成です。

#ifndef AppVersion
  #error AppVersion is required. Pass /DAppVersion=x.y.z (e.g. via installer\build-installer.ps1).
#endif

#ifndef Platform
  #define Platform "x64"
#endif

#ifndef SourceDir
  #if Platform == "x86"
    #define SourceDir "..\src\PdfConverter\bin\x86\Release"
  #else
    #define SourceDir "..\src\PdfConverter\bin\x64\Release"
  #endif
#endif

#define MyAppName "PDF Converter"
#define MyAppPublisher "Yuto Hirata"
#define MyAppExeName "PDF Converter.exe"
#define MyAppId "{{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=no
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=PdfConverter-{#AppVersion}-{#Platform}-setup
SetupIconFile=..\src\PdfConverter\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed={#Platform}compatible
ArchitecturesInstallIn64BitMode={#Platform}compatible
MinVersion=10.0
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} セットアップ
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "fileassoc"; Description: "PDF / Word (.pdf, .doc, .docx) をこのアプリで開けるように登録する"; GroupDescription: "ファイルの関連付け:"; Flags: unchecked

[Files]
; THIRD-PARTY-NOTICES.txt は build-installer.ps1 がビルド出力へ生成します
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; スタートメニュー（%AppData%\Microsoft\Windows\Start Menu\Programs）
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "PDF のページを画像へ変換"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; スタートメニュー検索・アプリ一覧用の登録
Root: HKA; Subkey: "Applications\{#MyAppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Applications\{#MyAppExeName}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey
Root: HKA; Subkey: "Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey
; ファイル関連付け（オプション）
Root: HKA; Subkey: ".pdf\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppExeName}"; ValueData: ""; Tasks: fileassoc; Flags: uninsdeletevalue
Root: HKA; Subkey: ".doc\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppExeName}"; ValueData: ""; Tasks: fileassoc; Flags: uninsdeletevalue
Root: HKA; Subkey: ".docx\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppExeName}"; ValueData: ""; Tasks: fileassoc; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function IsDotNet481Installed: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(
    HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release)
    and (Release >= 533320);
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if not IsDotNet481Installed then
  begin
    MsgBox(
      '.NET Framework 4.8.1 がインストールされていません。' + #13#10 +
      'セットアップを続行する前に、次のページからダウンロードしてインストールしてください。' + #13#10 + #13#10 +
      'https://dotnet.microsoft.com/download/dotnet-framework/net481',
      mbError, MB_OK);
    Result := False;
  end;
end;
