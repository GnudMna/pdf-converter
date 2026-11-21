# Changelog

このプロジェクトの重要な変更はすべてこのファイルに記録します。

形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に準拠し、
[セマンティックバージョニング](https://semver.org/lang/ja/) に従います。

## [Unreleased]

## [0.4.0] - 2026-07-08

### Added

- バッチ / ヘッドレスモード（`--output` などの CLI オプションで GUI なしの一括変換）
- バッチモード向けの CLI 解析（`BatchCliParser`）、変換実行（`BatchExportService`）、コンソール出力補助（`BatchConsoleHelper`）
- バッチモードの単体テスト（`BatchCliParserTests`、`BatchExportServiceTests`）

### Changed

- ビルドスクリプトで `.slnx` / `.sln` の自動フォールバックに対応（MSBuild 17.10 未満では `PdfConverter.sln` を使用。テストは `PdfConverter.slnx` 専用）
- README にバッチ / ヘッドレスモードの使い方を追記

## [0.3.2] - 2026-07-04

### Added

- アプリ内と README にオープンソースクレジット表示を追加
- インストーラー作成時に `THIRD-PARTY-NOTICES.txt` を自動生成・同梱
- ビルド用共通スクリプト `installer/build-common.ps1` と、復元・ビルド・テスト一括実行用 `installer/build-and-test.ps1`
- スクリーンリーダー向けのアクセシビリティ強化（AutomationProperties、Live Region、ラベル関連付け、動的ステータスの読み上げ）

### Changed

- 一時ファイル管理を `PdfConverterTempDirectory` に集約
- `build-installer.ps1` で NuGet 復元を自動実行（`nuget.exe` 未検出時は MSBuild / `dotnet restore` にフォールバック）
- エラーハンドリングとログ記録を強化（操作ログとエラーログの分離、ローテーション、例外メッセージ整形）
- PostBuildEvent を MSBuild Target に置き換え、Any CPU 構成を廃止
- MainViewModel の Coordinator Host 実装を `MainViewModelCoordinatorHost` アダプターに分離
- Coordinator と ThemeManager の重複コードを共通化

### Fixed

- Word → PDF 変換用一時ファイルがクラッシュや変換失敗後に残留する問題を修正（起動時・終了時にクリーンアップ）
- インストーラーの AppVersion フォールバックを廃止し、未指定時はビルドを失敗させるよう変更

## [0.3.1] - 2026-06-28

### Added

- Inno Setup によるインストーラー作成用ファイル（`installer/`）

### Fixed

- LibreOffice プロセスの stdout/stderr デッドロックを回避

### Changed

- デッドコードを削除
- テストギャップを改善
- README を更新

## [0.3.0] - 2026-06-21

### Added

- コマンドライン引数で PDF / Word を指定し、開いた状態で起動できるように

### Changed

- UI 関連コードをパネル単位に分割（リファクタリング）
- 入力パス検証の一貫性を向上
- README を更新

## [0.2.1] - 2026-06-10

### Fixed

- x64 環境で PDF 処理ができない問題を修正
- 画像品質が低下する問題を修正

## [0.2.0] - 2026-06-10

### Added

- Word の変換設定（Microsoft Word / LibreOffice）を指定できるように

### Changed

- UI を改善
- 永続化する設定を拡充
- ビルド設定を変更し、`slnx` 形式のソリューションファイルを追加
- 保守性向上のためのリファクタリング
- テストプロジェクトの NuGet パッケージを更新
- テストパターンを追加
- README を更新

## [0.1.0] - 2026-06-07

### Added

- Word 文書（`.doc` / `.docx`）の変換機能を追加

### Changed

- Word 変換にタイムアウトを設定
- 一時フォルダの明示的なクリーンアップを追加
- Word ファイル検証を共通化
- テストプロジェクトを非 SDK 形式に変更

### Removed

- 使用していないコードを削除

## [0.0.2] - 2026-06-07

### Added

- グローバル例外ハンドラを追加
- ステータスバーとカスタムダイアログを導入（モーダルダイアログを廃止）

### Fixed

- 起動中に OS のシステムテーマが変更された場合に反映されない問題を修正

### Changed

- UI を改善
- UI 操作ガードを強化
- メモリピーク制限を強化
- fire-and-forget タスクの追跡を追加
- View 結合を緩和し、Coordinator の DI 登録を整理
- テストパターンを追加

## [0.0.1] - 2026-06-07

初回リリース。

### Added

- PDF の各ページを画像（PNG / JPEG / BMP）へ変換する WPF アプリ
- ページプレビュー、解像度指定、出力範囲の選択
- ダークテーマ対応（ライト / ダーク / システム設定追従）
- 出力フォーマットの指定
- 並列レンダリング、クリップボードコピー、ドラッグ＆ドロップ
- MVVM アーキテクチャによる責務分離
- xUnit による単体テスト
- アプリアイコン

### Fixed

- 意図しない例外が発生する可能性がある問題を修正

### Changed

- 実行ファイル名を `PDF Converter.exe` に変更
