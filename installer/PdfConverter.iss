; PDF Converter — Inno Setup スクリプト
; ビルド例:
;   iscc /DAppVersion=x.x.x /DPlatform=x64 /DSourceDir="..\src\PdfConverter\bin\x64\publish" PdfConverter.iss
;
; AppVersion は必須です。通常は build-installer.ps1 が AssemblyInfo.cs から取得して渡します。
; SourceDir は dotnet publish (self-contained) の出力です。.NET ランタイムを同梱するため事前インストールは不要です。
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
    #define SourceDir "..\src\PdfConverter\bin\x86\publish"
  #else
    #define SourceDir "..\src\PdfConverter\bin\x64\publish"
  #endif
#endif

#define MyAppName "PDF Converter"
#define MyAppPublisher "Yuto Hirata"
#define MyAppExeName "PDF Converter.exe"
#define MyAppId "{{39D6BFFD-74AB-42F6-93F5-0CA2CCBE1506}"
; 0.6.0 以前の AppId (例示用の GUID のため他のアプリと重なるおそれがあり、上の値へ変更した)
; 旧 AppId で入っている版は [Code] の PrepareToInstall でアンインストールしてから入れ直す
#define LegacyAppId "{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
; 旧 AppId で入っている版があれば、そのインストール先を引き継ぐ
DefaultDirName={code:GetDefaultDirName}
; 旧 AppId の版から移行するとインストール先が既に存在するため、上書きの確認を出さない
DirExistsWarning=no
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
; ファイルの関連付けを登録・削除したことを、インストール・アンインストールの完了時にエクスプローラーへ通知する
ChangesAssociations=yes
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

[InstallDelete]
; .NET Framework 版 (0.4.x 以前) の残骸を削除する。.NET 版は lib\ を使わないフラット構成
Type: filesandordirs; Name: "{app}\lib"
Type: files; Name: "{app}\PDF Converter.exe.config"
; 利用者向けドキュメントは mdBook の静的ファイル名にハッシュが付くため、更新時に旧版のファイルが残らないよう作り直す
Type: filesandordirs; Name: "{app}\docs"
; スタートメニュー直下に置いていた旧版 (0.6.0 以前) のショートカットを削除する。{group} のフォルダーへ移したため
Type: files; Name: "{autoprograms}\{#MyAppName}.lnk"
Type: files; Name: "{autoprograms}\{#MyAppName} ドキュメント.lnk"

[Files]
; THIRD-PARTY-NOTICES.txt と docs\ (利用者向けドキュメント) は build-installer.ps1 がビルド出力へ生成します
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; スタートメニューの「PDF Converter」フォルダー (%AppData%\Microsoft\Windows\Start Menu\Programs\PDF Converter)
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "PDF のページを画像へ変換"
Name: "{group}\{#MyAppName} ドキュメント"; Filename: "{app}\docs\index.html"; Comment: "{#MyAppName} の使い方・コマンドライン・トラブルシューティング"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; 「プログラムから開く」に表示するための ProgID
#define MyProgId "PdfConverter.Document"

[Registry]
; ファイルの種類・アプリの登録は HKCU (または HKLM) の Software\Classes に書く (Windows はこれを HKEY_CLASSES_ROOT として参照する)
; スタートメニュー検索・アプリ一覧用の登録
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey
; ファイル関連付け (オプション)。OpenWithProgids の値名には ProgID を指定する
Root: HKA; Subkey: "Software\Classes\{#MyProgId}"; ValueType: string; ValueName: ""; ValueData: "{#MyAppName} 文書"; Tasks: fileassoc; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\{#MyProgId}"; ValueType: string; ValueName: "FriendlyTypeName"; ValueData: "{#MyAppName} 文書"; Tasks: fileassoc; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\{#MyProgId}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: fileassoc; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\{#MyProgId}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\.pdf\OpenWithProgids"; ValueType: string; ValueName: "{#MyProgId}"; ValueData: ""; Tasks: fileassoc; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.doc\OpenWithProgids"; ValueType: string; ValueName: "{#MyProgId}"; ValueData: ""; Tasks: fileassoc; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.docx\OpenWithProgids"; ValueType: string; ValueName: "{#MyProgId}"; ValueData: ""; Tasks: fileassoc; Flags: uninsdeletevalue
; 0.6.0 以前が誤って HKCU 直下に書いた登録を削除する (Windows からは参照されない)
; 空になったキーはアンインストール時に削除する。アンインストールは逆順に処理するため、親キーを先に書く
Root: HKCU; Subkey: "Applications"; ValueType: none; Flags: dontcreatekey uninsdeletekeyifempty
Root: HKCU; Subkey: "Applications\{#MyAppExeName}"; ValueType: none; Flags: deletekey dontcreatekey
Root: HKCU; Subkey: ".pdf"; ValueType: none; Flags: dontcreatekey uninsdeletekeyifempty
Root: HKCU; Subkey: ".pdf\OpenWithProgids"; ValueType: none; ValueName: "{#MyAppExeName}"; Flags: deletevalue dontcreatekey uninsdeletekeyifempty
Root: HKCU; Subkey: ".doc"; ValueType: none; Flags: dontcreatekey uninsdeletekeyifempty
Root: HKCU; Subkey: ".doc\OpenWithProgids"; ValueType: none; ValueName: "{#MyAppExeName}"; Flags: deletevalue dontcreatekey uninsdeletekeyifempty
Root: HKCU; Subkey: ".docx"; ValueType: none; Flags: dontcreatekey uninsdeletekeyifempty
Root: HKCU; Subkey: ".docx\OpenWithProgids"; ValueType: none; ValueName: "{#MyAppExeName}"; Flags: deletevalue dontcreatekey uninsdeletekeyifempty

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// 0.6.0 以前の AppId で入っている版を、新しい AppId の版へ移行する
// 旧版をすべての利用者が移行し終えたと判断できた時点で、LegacyAppId とこのセクションの移行処理は削除してよい

const
  // 旧 AppId の版のアンインストール情報 (Inno Setup は <AppId>_is1 に書き込む)
  LegacyUninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#LegacyAppId}_is1';
  // 旧 AppId の版のアンインストールが終わるまで待つ上限 (ミリ秒)
  LegacyUninstallTimeoutMs = 60000;
  // 旧 AppId の版のアンインストールの完了を確認する間隔 (ミリ秒)
  LegacyUninstallPollIntervalMs = 500;

var
  // 旧 AppId の版で選んでいたタスクを反映済みかどうか
  LegacyTasksApplied: Boolean;

// 旧 AppId で入っている PDF Converter を探し、アンインストール情報のルートキーを返す
// 例示用の GUID のため別のアプリが同じ AppId を使っている可能性があり、表示名と発行元が一致する場合のみ対象とする
function FindLegacyInstall(var RootKey: Integer): Boolean;
var
  DisplayName: String;
  Publisher: String;
begin
  Result := False;
  if RegQueryStringValue(HKCU, LegacyUninstallKey, 'DisplayName', DisplayName)
    and RegQueryStringValue(HKCU, LegacyUninstallKey, 'Publisher', Publisher)
    and (Pos('{#MyAppName}', DisplayName) = 1)
    and (Publisher = '{#MyAppPublisher}') then
  begin
    RootKey := HKCU;
    Result := True;
  end
  else if RegQueryStringValue(HKLM, LegacyUninstallKey, 'DisplayName', DisplayName)
    and RegQueryStringValue(HKLM, LegacyUninstallKey, 'Publisher', Publisher)
    and (Pos('{#MyAppName}', DisplayName) = 1)
    and (Publisher = '{#MyAppPublisher}') then
  begin
    RootKey := HKLM;
    Result := True;
  end;
end;

// 既定のインストール先を返す (旧 AppId の版があればそのインストール先を引き継ぐ)
function GetDefaultDirName(Param: String): String;
var
  RootKey: Integer;
begin
  if not (FindLegacyInstall(RootKey)
    and RegQueryStringValue(RootKey, LegacyUninstallKey, 'Inno Setup: App Path', Result)
    and (Result <> '')) then
  begin
    Result := ExpandConstant('{localappdata}\Programs\{#MyAppName}');
  end;
end;

// タスクの選択ページを初めて表示したときに、旧 AppId の版でのタスクの選択状態を反映する
// サイレントインストールでもページの遷移は内部で行われるため、この処理は呼ばれる
procedure CurPageChanged(CurPageID: Integer);
var
  RootKey: Integer;
  SelectedTasks: String;
  DeselectedTasks: String;
begin
  if (CurPageID <> wpSelectTasks) or LegacyTasksApplied then
  begin
    Exit;
  end;

  LegacyTasksApplied := True;
  if not FindLegacyInstall(RootKey) then
  begin
    Exit;
  end;

  if RegQueryStringValue(RootKey, LegacyUninstallKey, 'Inno Setup: Selected Tasks', SelectedTasks)
    and (SelectedTasks <> '') then
  begin
    WizardSelectTasks(SelectedTasks);
  end;

  // 選択を外していたタスクは、名前の先頭に "!" を付けると選択解除になる
  if RegQueryStringValue(RootKey, LegacyUninstallKey, 'Inno Setup: Deselected Tasks', DeselectedTasks)
    and (DeselectedTasks <> '') then
  begin
    StringChangeEx(DeselectedTasks, ',', ',!', True);
    WizardSelectTasks('!' + DeselectedTasks);
  end;
end;

// インストールの直前に、旧 AppId の版をサイレントでアンインストールする
// 旧版を残すと「インストールされているアプリ」に 2 件並び、片方をアンインストールすると共有のファイルが消えるため、
// アンインストールできない場合はインストールを中止する
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  RootKey: Integer;
  UninstallString: String;
  UninstallerPath: String;
  ResultCode: Integer;
  WaitedMs: Integer;
begin
  Result := '';
  if not FindLegacyInstall(RootKey) then
  begin
    Exit;
  end;

  if not RegQueryStringValue(RootKey, LegacyUninstallKey, 'UninstallString', UninstallString)
    or (UninstallString = '') then
  begin
    Result := '以前のバージョンの {#MyAppName} のアンインストール情報を読み取れませんでした。' + #13#10
      + '「設定」の「インストールされているアプリ」から以前のバージョンをアンインストールしてから、もう一度実行してください。';
    Exit;
  end;

  Log('Uninstalling legacy version: ' + UninstallString);
  UninstallerPath := RemoveQuotes(UninstallString);
  if not Exec('>', UninstallString + ' /VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := '以前のバージョンの {#MyAppName} をアンインストールできませんでした。' + #13#10
      + '「設定」の「インストールされているアプリ」から以前のバージョンをアンインストールしてから、もう一度実行してください。' + #13#10
      + #13#10
      + '詳細: ' + SysErrorMessage(ResultCode);
    Exit;
  end;

  // アンインストーラーは一時フォルダーへ自身を複製して処理を引き継ぐため、終了コードを待つだけでは完了を判断できない
  // アンインストール情報とアンインストーラーが消えるまで待つ
  WaitedMs := 0;
  while (RegKeyExists(RootKey, LegacyUninstallKey) or FileExists(UninstallerPath))
    and (WaitedMs < LegacyUninstallTimeoutMs) do
  begin
    Sleep(LegacyUninstallPollIntervalMs);
    WaitedMs := WaitedMs + LegacyUninstallPollIntervalMs;
  end;

  if RegKeyExists(RootKey, LegacyUninstallKey) then
  begin
    Result := '以前のバージョンの {#MyAppName} のアンインストールが完了しませんでした。' + #13#10
      + '「設定」の「インストールされているアプリ」から以前のバージョンをアンインストールしてから、もう一度実行してください。';
    Exit;
  end;

  Log('Legacy version uninstalled');
end;
