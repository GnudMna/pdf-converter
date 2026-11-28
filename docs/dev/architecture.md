# アーキテクチャ

アプリ本体 `src/PdfConverter/` の構造をまとめる。個々のクラスの詳細は、各ファイルの XML ドキュメントコメント (`<summary>` / `<remarks>`) を正とする。

## 全体像

```text
View (XAML)  ──バインディング・コマンド──▶  MainViewModel
                                              │  パネル単位の ViewModel を公開し、処理は Coordinator に委譲
                                              ▼
                                  PdfPreviewCoordinator / PdfSaveCoordinator
                                              │  IMainViewModelHost (MainViewModelCoordinatorHost) 経由で画面状態を読み書き
                                              ▼
                                           Services
           PdfConversionService ─▶ PdfiumDocument ─▶ PDFium (pdfium.dll)
           DocumentPdfSourceService ─▶ SelectableWordToPdfConversionService ─▶ Microsoft Word (COM) / LibreOffice (soffice.exe)
```

バッチ変換 (CLI) では View・ViewModel を作らず、`BatchExportService` が同じ Service を直接使う。

## 起動の流れ

エントリーポイントは `App.OnStartup` (`App.xaml.cs`)。

1. `GlobalExceptionHandler.Register` で未処理例外の捕捉を始める
2. `UserSettingsInitializer.Initialize` でユーザー設定を読み込む。会社名を個人名からブランド名へ変える前の旧フォルダー (`%LOCALAPPDATA%\Yuto_Hirata\`) に設定があり、新しいフォルダーが未作成なら `LegacySettingsMigrator` が複製し、旧フォルダーを削除する。そのうえで `user.config` が壊れていれば削除して既定値に戻し、バージョンアップ後の初回起動なら前の版の設定を引き継ぐ
3. `PdfConverterTempDirectory.CleanupStaleEntries` で、24 時間より古い他インスタンスの一時ファイルを削除する
4. `BatchCliParser.TryParse` で起動引数を調べる。`-` で始まる引数や `/?` が 1 つでもあればバッチ変換として扱う
   - **バッチ変換:** `ServiceConfigurator.Configure` で DI コンテナを組み、`BatchCancellationScope` (Ctrl+C をキャンセルに変換) の下で `IBatchExportService.RunAsync` を同期的に待つ。終了時は Word の後片付けを待ち、一時ファイルを削除して、終了コードを付けて `Shutdown` する
   - **GUI:** `ThemeManager.Initialize` でテーマを適用し、DI コンテナから `MainWindow` を取得して表示する。ファイルパスの引数があれば、`ApplicationStartupHelper.TryGetInitialDocumentPath` で最初に開けるパスを選んで読み込む
5. 終了時 (`App.OnExit`) は DI コンテナを破棄し、このインスタンスの一時ディレクトリを削除する

本体は GUI サブシステムの実行ファイルのため、バッチ変換の出力は `BatchConsoleHelper` が親プロセスのコンソールへ接続して書き込む。
cmd や PowerShell は GUI アプリの終了を待たず、標準入出力のリダイレクトも引き継がれないため、コンソールからの実行用に `src/PdfConverter.Console/` の `pdfconv.exe` を本体と同じフォルダーへ同梱する。本体を `ProjectReference` で参照して publish 出力へ含める (.NET ランタイムは本体と共有する)。標準入出力を 3 本とも中継し、本体の終了を待って終了コードを返す。Ctrl+C は本体が片付けを終えるまで待つため、`pdfconv.exe` 側では無視する。

## 依存関係の構成 (DI)

`Infrastructure/ServiceConfigurator.cs` で `Microsoft.Extensions.DependencyInjection` に登録する。GUI とバッチ変換で同じ構成を使う。

| 有効期間 | 登録するもの |
| :--- | :--- |
| Singleton | Service 全般 (PDF 描画・Word 変換・設定・更新・ダイアログ・クリップボード・バッチ変換) |
| Transient | Coordinator・`MainViewModel`・`MainWindow` |

`IWordToPdfConversionService` は、`MicrosoftWordToPdfConversionService` と `LibreOfficeToPdfConversionService` を設定で切り替える `SelectableWordToPdfConversionService` として登録する。Service を足すときはインターフェースを `Services/` に置き、ここに登録する。

## 画面 (MVVM)

### View

- `Views/MainWindow.xaml` はパネル (`Views/Panels/`) を並べるだけで、コードビハインドに処理を書かない。例外として、バージョン情報ダイアログ (`AboutDialog`) は更新の確認・適用と設定のリセットをコードビハインドで扱う
- ドラッグ&ドロップやイベントからのコマンド実行は、添付ビヘイビア (`Views/Behaviors/`) で ViewModel へ渡す
- テーマは `Themes/LightTheme.xaml` / `DarkTheme.xaml` の色と `SharedStyles.xaml` の共通スタイルで構成し、`ThemeManager` が切り替える。**システム** を選んだ場合は Windows の設定変更 (`SystemEvents.UserPreferenceChanged`) に追従する

### ViewModel

`MainViewModel` はメイン画面の ViewModel で、パネルごとの ViewModel をプロパティとして公開する。

| プロパティ | ViewModel | 担当 |
| :--- | :--- | :--- |
| `Preview` | `PreviewPanelViewModel` | プレビュー画像・ページ移動 |
| `ExportSettings` | `ImageExportSettingsViewModel` | 出力形式・解像度・JPEG 品質・保存ページ・透明度 |
| `WordSettings` | `WordConversionSettingsViewModel` | Word → PDF 変換の設定 |
| `Theme` | `ThemeSettingsViewModel` | テーマ選択 |
| `Status` | `StatusBarViewModel` | ステータスメッセージ・進捗 |

コマンドは `Commands/RelayCommand` と `AsyncRelayCommand` (実行中は再入を防ぐ) を使う。

### Coordinator

読み込み・プレビュー・保存の手順は `ViewModels/Coordinators/` の Coordinator に置き、`MainViewModel` はコマンドから呼び出すだけにする。

| Coordinator | 担当 |
| :--- | :--- |
| `PdfPreviewCoordinator` | ファイルの読み込み (Word は PDF へ変換)・パスワードの入力要求・ページ数の取得・プレビューの描画・ページ移動 |
| `PdfSaveCoordinator` | 保存ページの決定・上書き確認・画像の一括保存・PDF として保存 |

Coordinator は `MainViewModel` を直接参照せず、必要な状態だけを持つホストインターフェース (`IPreviewCoordinatorHost` / `ISaveCoordinatorHost`。まとめたものが `IMainViewModelHost`) を受け取る。実装は `MainViewModelCoordinatorHost` が `MainViewModel` へ委譲する。キャンセル・進捗・ステータスの制御は `ICoordinatorSession` で行う。

この分離により、Coordinator はテスト用のホスト (`Tests/Helpers/TestMainViewModelHost`) で単体テストできる。

## サービス

### PDF の描画 (`PdfConversionService` / `PdfiumDocument`)

- `PdfiumDocument` は PDFiumCore の薄いラッパー。PDFium はスレッドセーフでないため、呼び出しはすべてグローバルロックで直列化する
- 1 ページあたりの描画画素数は x64 で 3 億、x86 で 1 億までに制限する。超える場合は描画せずに失敗させる (保存では書き出しを始める前に中断する)
- 64 MB 以下の PDF はファイル全体をメモリにキャッシュする。それより大きい PDF は、PDFium が要求した範囲だけをファイルから読む
- プレビューとページ数の取得では、直近に開いた文書を開いたまま再利用する
- 保存はページ単位で並列化する (最大 4。PDF のサイズが大きいほど並列度を下げる)。PDFium の呼び出しは直列のため、並列で短縮されるのはビットマップ生成・エンコード・書き込みである。同時に描画する画素数の合計にも上限を設け、メモリ不足を防ぐ
- 保存は全ページを保存先フォルダー内のステージングディレクトリ (`.pdfconverter-staging-<識別子>`) へ書き出してから、保存先へまとめて反映する。`%TEMP%` を経由しないため書き込みは 1 回で済み、反映は同一ボリューム内のリネームだけで行う。既存の同名ファイルはステージングディレクトリへ退避し、途中で失敗した場合は配置済みのファイルを消して既存のファイルを元に戻す。ステージングディレクトリは隠しフォルダーとして作り、成功・失敗・キャンセルのいずれでも削除する。プロセスの強制終了で残ったものは `%TEMP%` 側の `CleanupStaleEntries` からは見えないため、次回の保存時に同じ保存先で、GUID 形式の名前かつ 24 時間以上更新のないものを削除する
- 内部の `await` はすべて `ConfigureAwait(false)` にし、PDF の解析やファイル書き込みを UI スレッドで実行しない
- パスワード付き PDF は、パスワードが無い・誤っている場合に `PdfPasswordRequiredException` (`WasPasswordProvided` で未入力か誤りかを区別) を投げる。パスワードは `SetPdfPassword` で 1 ファイル分だけ保持し、パスの比較は大文字小文字を区別せず、別のファイルを指定した時点で破棄する。画面では `PdfPreviewCoordinator` がページ数の取得で例外を受けて `IDialogService.ShowPasswordDialog` で入力を求め、再試行する (キャンセルで読み込みを中止)。バッチ変換では `--password` の値を全入力ファイルに設定し、不足・不一致は `--password` の案内を付けて報告する

### Word → PDF 変換

`DocumentPdfSourceService` が、入力ファイルから描画用の PDF パスを返す。PDF はそのまま返し、Word 文書は変換して一時 PDF を返す。変換結果は元ファイルの更新日時・サイズとともにキャッシュし、Word 設定が変わったらすべて破棄する。

| 実装 | 方式 |
| :--- | :--- |
| `MicrosoftWordToPdfConversionService` | Word を COM で操作する。COM 呼び出しは `StaTaskRunner` で STA スレッド上に載せる。信頼できない文書のマクロを実行しないよう、開く前にマクロを無効化する。キャンセル時は Word を終了させ、応答しなければプロセスを強制終了する |
| `LibreOfficeToPdfConversionService` | `soffice.exe` を headless で起動する。利用者の LibreOffice に影響しないよう、一時ディレクトリ配下の変換専用プロファイル (`-env:UserInstallation`) を使う。プロファイルを共有するため変換は 1 件ずつ実行する |

どちらも `WordToPdfConversionTimeouts.Conversion` (5 分) でタイムアウトする。`soffice.exe` のパス解決は `LibreOfficePathHelper` が行う。

### バッチ変換 (`BatchExportService`)

`BatchCliParser` が解析した `BatchCliOptions` に従い、ファイルごとに PDF の取得 (Word は変換)・画像の保存または PDF の保存を行う。終了コードは `BatchCliParser` の定数 (`0` 成功 / `1` 変換エラー / `2` 引数エラー) を返す。オプションと終了コードの仕様は利用者向けの [コマンドライン](../user/cli.md) を正とする。

パスワードは `--password` のほか、`--password-file`・`--password-env`・`--password-stdin` で渡せる。解析 (`BatchCliParser`) は指定先を `BatchCliOptions` に記録するだけで、読み取りは `App.TryRunBatchMode` が `BatchPasswordResolver` で行う。読み取れなければ変換を始めずに引数エラー (終了コード `2`) とする。

呼び出し元は UI スレッドで完了を同期的に待つため、処理全体を `Task.Run` でスレッドプールに載せてデッドロックを避けている。

### 更新 (`UpdateService`)

GitHub Release を直接参照する (`UpdateService.DefaultApiBaseUrl` / `DefaultDownloadBaseUrl`)。認証は使わない。

1. **確認:** GitHub Releases API の `/repos/<owner>/<repo>/releases/latest` (下書き・プレリリースを除く最新のリリース) を照会し、`GitHubReleaseJsonParser` がタグとアセット (名前・サイズ・digest) を読み取る。`UpdateEvaluator` が現在の版 (`ApplicationMetadata.Version`) と比較する。実行中のプラットフォーム (x64 / x86) に合うインストーラー (ポータブル版では `-portable.zip`) を `UpdateAssetSelector` が選ぶ
2. **適用:** `https://github.com/<owner>/<repo>/releases/download/<タグ>/<アセット名>` からインストーラーとその署名 (`<インストーラー名>.sig`) をダウンロードし、サイズ・SHA-256・署名を検証してから起動する。起動できたらアプリを終了する。ポータブル版の適用は [ポータブル版](#ポータブル版) を参照する

サイズと SHA-256 は GitHub が返す値のため、リポジトリやアカウントが侵害されるとインストーラーと一緒に差し替えられる。そこで、リリース時に手元の秘密鍵で作る ECDSA P-256 署名を、アプリに埋め込んだ公開鍵 (`UpdateSigningKey.PublicKey`) で `UpdateSignatureVerifier` が検証する。署名対象は版 (タグ)・アセット名・サイズ・SHA-256 をまとめた文字列で、古い署名済みインストーラーを新しい版として配られる巻き戻しも防ぐ。署名や公開鍵が無い場合は起動せず、ダウンロードしたファイルを削除する。署名の作り方は [インストーラーとリリース](release.md#更新の署名) を参照する

### ポータブル版

実行ファイルの隣にマーカー `portable.txt` があると、`PortableMode.IsEnabled` が真になる (起動中は判定を変えない)。インストール版との違いは次の 3 点だけで、ほかの処理は共通である。

- **設定:** `Settings` のコンストラクター (`Properties/Settings.cs`) が、全項目のプロバイダーを `PortableSettingsProvider` に差し替え、`UserData\user.config` (版によらない 1 ファイルの XML) に保存する。版ごとのフォルダーが無いため `Upgrade` は何もしない。読み込めないファイルはファイル名付きの `ConfigurationErrorsException` にして、既定のプロバイダーと同じく `UserSettingsInitializer` の破損回復に任せる。旧会社名フォルダーからの引き継ぎ (`LegacySettingsMigrator`) は行わない
- **ログ:** `ApplicationLogger` の既定の出力先を `UserData\logs\` にする
- **更新:** インストーラーの代わりに `PdfConverter-<版>-<Platform>-portable.zip` を選び、次の流れで適用する

1. 旧版の `UpdateService` が zip と署名を検証し、`%TEMP%\PdfConverter\updates\<版>\portable\` に展開する (`PortableUpdatePackage.Extract`。展開先の外を指すエントリー・過大な展開・必要なファイルの欠けを拒否する)
2. 展開した新しい版の `PDF Converter.exe` を `--apply-portable-update --target <アプリフォルダー> --wait-pid <旧版のプロセス ID>` で起動し、旧版は終了する。実行中の exe と DLL は自身で置き換えられないため、置き換えは新しい版が行う
3. 新しい版の `App.OnStartup` は、設定や一時ファイルに触れる前にこの引数を見つけて `PortableUpdateCommand.Run` へ分岐する。旧版の終了と、同じフォルダーから動く `PDF Converter.exe` / `pdfconv.exe` の終了を待ち、`PortableUpdateApplier.TryApply` でファイルを置き換えてから、アプリフォルダーのアプリを起動し直す
4. `TryApply` は、旧版と新しい版の `portable-manifest.txt` に載るファイルをアプリフォルダー内の `.update-backup-<識別子>\` へ移してから新しい版を複製する。失敗したら複製したファイルを消して元に戻す。成功したら退避フォルダーと、旧版にしか無いファイルで空になったフォルダーを消す。`UserData\` とマニフェストに無いファイル (利用者が置いたもの) には触れない。消せずに残った退避フォルダーは、次の起動時に削除する

`--apply-portable-update` の引数は旧版が組み立てて新しい版が解釈するため、形式 (`PortableUpdateCommand.BuildArguments`) は版をまたいで変えない。

### ユーザー設定

設定は .NET の `ApplicationSettingsBase` (`Properties/Settings.settings`) で `user.config` に保存する。画面・CLI からは直接触らず、`IImageExportSettings` / `IWordToPdfConversionSettings` / `IUserSettingsService` を通す。

設定項目を足すときは `Settings.settings` と `Settings.Designer.cs` を同じ変更で更新する (dotnet CLI は Designer を再生成しないため、既存の生成コードの書式に合わせて手で足す)。項目の追加・型の変更は、前の版からの引き継ぎ (`Settings.Default.Upgrade`) で値がどうなるかも確かめる。

### ログと例外

- `ApplicationLogger` がログを書く。`app.log` (操作の記録・警告) と `error.log` (エラーの詳細) に分け、2 MB を超えたら切り替えて 5 世代まで残す
- `GlobalExceptionHandler` が UI スレッド・バックグラウンドの未処理例外を捕捉し、ログに記録して利用者に通知する。終了させるのは、メモリ不足・ネイティブ障害・型の読み込み失敗など続行が危険な例外 (`IsFatalException`) だけで、`NullReferenceException` などの単発のバグはエラーを表示して操作を続けさせる
- バッチ変換中は `GlobalExceptionHandler.IsHeadless` が真になり、UI スレッドが完了待ちで塞がっていてもダイアログを出さず、標準エラーへ書き出す
- 利用者向けのエラーメッセージは `ExceptionFormattingHelper.GetUserMessage` で整形する

## ファイルの置き場所

| 用途 | 場所 | 管理するクラス |
| :--- | :--- | :--- |
| ログ | `%LOCALAPPDATA%\PDF Converter\logs\` (ポータブル版は `<アプリフォルダー>\UserData\logs\`) | `ApplicationLogger` |
| ユーザー設定 | `%LOCALAPPDATA%\PdfConverter\<実行ファイル名と識別子>\<版>\user.config` | `Settings` (.NET の既定。フォルダー名は `AssemblyInfo.cs` の `AssemblyCompany`。公開者名は `AssemblyMetadata("Publisher")` で別に持つ) / `LegacySettingsMigrator` |
| ユーザー設定 (ポータブル版) | `<アプリフォルダー>\UserData\user.config` | `PortableSettingsProvider` |
| 一時ファイル | `%TEMP%\PdfConverter\instance-<識別子>\` | `PdfConverterTempDirectory` |
| 更新のダウンロード | `%TEMP%\PdfConverter\updates\<版>\` (ポータブル版の展開先は `portable\`) | `UpdateService` |
| ポータブル版の更新の退避 | `<アプリフォルダー>\.update-backup-<識別子>\` | `PortableUpdateApplier` |

一時ファイルはプロセスごとのインスタンスディレクトリに作る。GUI とバッチ変換を同時に動かしても互いのファイルを消さないよう、インスタンスディレクトリ内のロックファイルを終了まで開き続け、使用中かどうかを判別している。
