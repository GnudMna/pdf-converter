<div align="center">

<img src="src/PdfConverter/Assets/app-icon.png" alt="PDF Converter" width="140" />

# PDF Converter

**PDF / Word 文書のページを高品質な画像へ変換する、シンプルな Windows アプリ**

[![Version](https://img.shields.io/badge/Version-0.6.0-blue)](#)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows&logoColor=white)](#)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](#)
[![Language](https://img.shields.io/badge/Language-C%23-239120?logo=csharp&logoColor=white)](#)
[![UI](https://img.shields.io/badge/UI-WPF%20(MVVM)-2C68C4?logo=windowsxp&logoColor=white)](#)
[![Engine](https://img.shields.io/badge/Engine-PDFium%20(PDFiumCore)-FF5722)](#)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

</div>

---

## ✨ 概要

**PDF Converter** は、PDF または Word 文書の各ページを **PNG / JPEG / BMP** の画像として書き出せる WPF デスクトップアプリです。
Word ファイルは Microsoft Word または LibreOffice で PDF に変換したうえで画像化します。レンダリングには [PDFium](https://pdfium.googlesource.com/pdfium/) ([PDFiumCore](https://github.com/Dtronix/PDFiumCore)) を採用します。変換前にプレビューを確認しながら、解像度や出力範囲を細かく指定できます。

---

## 🚀 主な機能

| | 機能 | 説明 |
|:---:|:---|:---|
| 🖼️ | **ページプレビュー** | 変換前に各ページを表示。前後移動・ページ番号ジャンプに対応 |
| 🗂️ | **柔軟な出力範囲** | 単一ページ・ページ範囲 (例: `1-3,5`)・全ページの保存 |
| 📐 | **解像度の指定** | 幅 (px) / 高さ (px) / DPI で出力サイズをコントロール |
| 🎨 | **複数の画像形式** | `PNG` / `JPEG` / `BMP` を選択可能 |
| 🫧 | **透過の保持** | PNG 出力時に背景の透明度を保持 |
| ⚡ | **並列処理** | CPU コア数に応じてページを並列レンダリングし高速保存 |
| 📋 | **クリップボードコピー** | プレビュー画像をワンクリックでコピー |
| 📄 | **Word 対応** | `.doc` / `.docx` を Word または LibreOffice 経由で PDF 化し、画像へ変換 |
| 🖱️ | **ドラッグ＆ドロップ** | PDF / Word をウィンドウへドロップするだけで読み込み |
| 🚀 | **コマンドライン起動** | 起動時引数で PDF / Word を指定し、開いた状態で起動 |
| ⚙️ | **バッチ / ヘッドレス** | `--output` などのオプションで GUI なしの一括変換に対応 |
| 🌗 | **テーマ切替** | ライト / ダーク / システム設定に追従 |
| 🧠 | **メモリキャッシュ** | 64 MB 以下の PDF をメモリにキャッシュし、I/O を削減 |
| 🛑 | **キャンセル対応** | 処理途中での中断が可能 |
| ♿ | **アクセシビリティ** | スクリーンリーダー対応 (ラベル関連付け・Live Region によるステータス読み上げ) |
| 🔄 | **更新確認** | バージョン情報から最新インストーラーを確認・ダウンロードして適用 |

---

## 🛠️ 技術スタック

<div align="left">

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)
![.NET](https://img.shields.io/badge/.NET%2010-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-2C68C4?style=for-the-badge&logo=windowsxp&logoColor=white)
![Windows](https://img.shields.io/badge/Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)

</div>

- **アーキテクチャ**: MVVM (パネル単位の View / ViewModel、`MainViewModelCoordinatorHost` による Coordinator 連携、Service の責務分離)
- **DI コンテナ**: `Microsoft.Extensions.DependencyInjection`
- **PDF レンダリング**: `PDFiumCore` (PDFium、[pdfium-binaries](https://github.com/bblanchon/pdfium-binaries) のビルドを同梱)
- **Word → PDF 変換**: Microsoft Word COM または LibreOffice headless (設定で切り替え)
- **ダイアログ**: `Ookii.Dialogs.Wpf`
- **テスト**: `xUnit v3` + `Moq` による単体テスト (`PdfConverter.Tests`)

---

## 📦 必要要件

- Windows 10 / 11 (x86 / x64 いずれのビルドも利用可能)
- .NET ランタイムのインストールは不要 (アプリに同梱する self-contained 配布)
- Word ファイルを変換する場合: **Microsoft Word** (デスクトップ版) または **LibreOffice** のいずれか
- LibreOffice 使用時: 一般的なインストール先から `soffice.exe` を自動検出 (必要に応じてアプリ内でパス指定可能)
- ビルドする場合: [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) 以降 (Visual Studio 2026 など SDK 10 を含む環境でも可)
- インストーラーを作成する場合: [Inno Setup 6](https://jrsoftware.org/isinfo.php)、[mdBook](https://github.com/rust-lang/mdBook/releases) 0.5.4 (利用者向けドキュメントの HTML 化。`mdbook.exe` を PATH に置く)

---

## 🏗️ ビルド & 実行

ソリューションは `src/PdfConverter.slnx` を使用します。ビルドには .NET SDK 10 以降 (dotnet CLI) が必要です。

```powershell
# リポジトリの取得
git clone <repo_url>
cd pdf-converter

# (任意) 更新ダウンロード用キーを埋め込む
# UpdateReleaseKey.local.props.example を UpdateReleaseKey.local.props にコピーして
# PdfConverterReleaseKey を設定するか、環境変数 PDF_CONVERTER_KEY を設定する
# (キー未設定でもビルド・起動は可能。更新の「適用」時のみ失敗する)

# ビルド (Release / x64 構成 — 既定)。パッケージ復元も自動で行われる
dotnet build src/PdfConverter.slnx -c Release -p:Platform=x64

# x86 向けにビルドする場合
dotnet build src/PdfConverter.slnx -c Release -p:Platform=x86

# 配布用の出力 (.NET ランタイム同梱) を作成する場合
dotnet publish src/PdfConverter/PdfConverter.csproj -c Release -p:Platform=x64 -o src/PdfConverter/bin/x64/publish
```

PDFium のネイティブ DLL (`pdfium.dll`) はプラットフォーム固有のため、**Any CPU 構成はサポートしていません**。ビルド時は `x64` または `x86` を明示してください (省略時は x64)。

アプリは .NET ランタイムを同梱する **self-contained** 構成でビルドされるため、実行環境に .NET のインストールは不要です。ビルド後、プラットフォームに応じて次の場所に `PDF Converter.exe` が生成されます。

| プラットフォーム | ビルド出力 | 配布用 (publish) 出力 |
|:---|:---|:---|
| `x64` | `src/PdfConverter/bin/x64/Release/` | `src/PdfConverter/bin/x64/publish/` |
| `x86` | `src/PdfConverter/bin/x86/Release/` | `src/PdfConverter/bin/x86/publish/` |

出力フォルダーは `PDF Converter.exe` と依存 DLL (`pdfium.dll` を含む) を同じ階層に置くフラット構成です。Release ビルドでは PDB は出力されません。

---

## 📦 インストーラー

[Inno Setup 6](https://jrsoftware.org/isinfo.php) を使ったセットアップ EXE を `installer/` から作成できます。管理者権限を要求しない**ユーザー単位インストール**で、`%LOCALAPPDATA%\Programs\PDF Converter` に配置されます。

```powershell
cd installer

# x64 版 (既定) — dotnet publish・インストーラー生成を一括実行
.\build-installer.ps1

# x86 と x64 の両方
.\build-installer.ps1 -Platform all

# publish 済みの出力をそのまま使う場合
.\build-installer.ps1 -SkipBuild

# ターミナル向け (完了後のキー入力待ちを省略)
.\build-installer.ps1 -NoPause
```

`docs/user/` は mdBook で HTML 化され (`installer/build-user-docs.ps1`)、publish 出力の `docs\` としてインストーラーに収録されます。mdBook の版は出力のファイル名と同梱素材が変わるため 0.5.4 に固定しており、異なる版ではビルドがエラーになります。

生成物は `dist/` に出力されます (例: `PdfConverter-x.x.x-x64-setup.exe`)。バージョン番号は `src/PdfConverter/Properties/AssemblyInfo.cs` から自動取得され、Inno Setup へ `/DAppVersion` として渡されます。`AppVersion` 未指定で `PdfConverter.iss` を直接コンパイルするとエラーになります。

| 項目 | 内容 |
|:---|:---|
| 前提条件 | なし (.NET ランタイムを同梱) |
| スタートメニュー | アプリとドキュメントのショートカットを自動作成 |
| デスクトップショートカット | オプション |
| ファイル関連付け | オプション (`.pdf` / `.doc` / `.docx`) |
| ウィザード UI | 日本語 / 英語 |

### リリース

`installer/release.ps1` で、バージョン更新からインストーラー作成までを一括実行できます。`release/v{version}` ブランチ上で実行し、事前に [CHANGELOG.md](CHANGELOG.md) の `[Unreleased]` 節へ変更項目を書いておきます。

```powershell
git fetch origin develop
git switch -c release/v0.6.0 origin/develop

cd installer

# バージョンを明示指定 / 現在のバージョンからインクリメント
.\release.ps1 -Version 0.6.0 -NoPause
.\release.ps1 -Bump Minor -NoPause

# 更新内容の確認のみ (ファイル・Git は変更しない)
.\release.ps1 -Version 0.6.0 -DryRun -NoPause
```

スクリプトは次を順に行います。

1. ブランチ名と新バージョンの一致・作業ツリーがクリーンであること・更新ダウンロード用キー (`PDF_CONVERTER_KEY` または `UpdateReleaseKey.local.props`) の設定を確認
2. `AssemblyInfo.cs` のバージョン 3 属性、`CHANGELOG.md` (`[Unreleased]` を `## [x.y.z] - 日付` へ移し比較リンクを更新)、`README.md` のバッジを更新
3. `build-and-test.ps1` で品質チェック (Release 構成。対象プラットフォームごと)
4. `Release: vx.y.z` で Git コミット
5. `build-installer.ps1` でインストーラーを作成

| オプション | 説明 |
| :--- | :--- |
| `-Version x.y.z` | 新バージョンを明示指定 (`-Bump` と排他) |
| `-Bump Patch\|Minor\|Major` | 現在のバージョンからインクリメント |
| `-Date yyyy-MM-dd` | リリース日 (既定: 本日) |
| `-Platform x64\|x86\|all` | 品質チェックとインストーラーの対象 (既定: `all`) |
| `-DryRun` | 更新予定を表示するのみ |
| `-SkipCheck` / `-SkipInstaller` / `-NoCommit` | 品質チェック / インストーラー作成 / Git コミットを省略 |
| `-AllowDirty` | 未コミットの変更があっても続行 |
| `-NoPause` | 完了後のキー入力待ちを省略 |

実行後、`release/v{version}` を push して PR で `develop` と `main` へマージし、Gitea で `v{version}` タグを付与して、GitHub Release へ `dist\` のインストーラーをアップロードします。

---

## 📖 使い方

使い方・コマンドライン (バッチ変換)・トラブルシューティングは、利用者向けドキュメント [`docs/user/`](docs/user/introduction.md) にまとめています。

| ページ | 内容 |
|:---|:---|
| [はじめに](docs/user/introduction.md) | できること・動作環境 |
| [使い方](docs/user/usage.md) | ファイルの開き方・プレビュー・変換設定・保存・Word 設定・テーマ・更新 |
| [コマンドライン](docs/user/cli.md) | 起動時のファイル指定と、バッチ変換のオプション・終了コード |
| [トラブルシューティング](docs/user/troubleshooting.md) | ログの場所・よくあるエラー・設定の初期化・アンインストール |

インストール版には HTML 化したドキュメントが `docs\` に同梱され、スタートメニューの **PDF Converter ドキュメント**、アプリ右上の **?** ボタン、`F1` キーから開けます。

---

## 🧪 テスト

単体テストは [xUnit.net v3](https://xunit.net/) で実装されています。テストプロジェクトは **`net10.0-windows`** で、ビルド後に生成されるテスト実行ファイルを直接起動して実行できます。

```powershell
cd installer

# 復元・ビルド・テストを一括実行 (推奨)
.\build-and-test.ps1

# Release 構成で実行
.\build-and-test.ps1 -Configuration Release

# x86 向けに実行 / 復元を省略 / 完了後のキー入力待ちを省略
.\build-and-test.ps1 -Platform x86 -SkipRestore -NoPause
```

手動で実行する場合:

```powershell
# 1. テストプロジェクトをビルド (パッケージ復元も自動で行われる)
dotnet build src/PdfConverter.Tests/PdfConverter.Tests.csproj -c Debug -p:Platform=x64

# 2. テスト実行 (xUnit v3 In-Process Runner)
src/PdfConverter.Tests/bin/x64/Debug/net10.0-windows/PdfConverter.Tests.exe
```

Visual Studio のテストエクスプローラーからも実行できます (`xunit.runner.visualstudio` を使用)。

---

## 🤝 開発

- **コード解析**: `AnalysisLevel=10.0-recommended` のコード解析とコードスタイル検査を有効にしており、警告はビルドエラーになります。ルールの抑止は理由を添えて `.editorconfig` に記述します
- **Nullable 参照型**: アプリ本体で有効です。null を取り得る値は `?` で明示します
- **コーディング規約**: C# のセクション区切りコメントと日本語表記 (括弧・半角スペース) の規則は [`.claude/rules/`](.claude/rules/) を参照してください
- **自動生成ファイル**: `Settings.Designer.cs` / `Resources.Designer.cs` は手で編集しません
- **ブランチ**: `develop` から `feature/<name>` / `fix/<name>` などを作成し、PR で `develop` へマージします
- **コミットメッセージ**: `Update: ...` / `Fix: ...` 形式。機能変更時は [CHANGELOG.md](CHANGELOG.md) を更新します
- **PDFiumCore の更新**: `installer/licenses/PDFium.txt` を同じバージョンの [pdfium-binaries](https://github.com/bblanchon/pdfium-binaries/releases) リリースのライセンスに差し替えます (不一致は `generate-third-party-notices.ps1` がエラーにします)

---

## 📂 プロジェクト構成

```text
pdf-converter/
├─ src/
│  ├─ PdfConverter.slnx            # ソリューション
│  ├─ Directory.Build.props        # 全プロジェクト共通の MSBuild 設定
│  ├─ PdfConverter/                # アプリ本体 (WPF / MVVM)
│  │  ├─ Commands/                 # コマンド実装
│  │  ├─ Converters/               # 値変換
│  │  ├─ Infrastructure/           # ログ・例外処理・DI 構成・設定初期化・起動補助・CLI 解析
│  │  │  ├─ BatchCliParser.cs      # バッチ / ヘッドレスモードの引数解析
│  │  │  ├─ BatchCliOptions.cs     # バッチモードのオプション
│  │  │  └─ BatchConsoleHelper.cs  # バッチモードのコンソール出力
│  │  ├─ Models/                   # ドメインモデル / 列挙型
│  │  ├─ Properties/               # AssemblyInfo・リソース・ユーザー設定定義
│  │  ├─ Services/                 # 変換・I/O・Word/LibreOffice 連携・バッチ変換・更新確認
│  │  │  ├─ PdfConversionService.cs                # PDF の読み込み・描画・画像保存
│  │  │  ├─ PdfiumDocument.cs                      # PDFium のラッパー
│  │  │  ├─ MicrosoftWordToPdfConversionService.cs # Word COM による PDF 変換
│  │  │  ├─ LibreOfficeToPdfConversionService.cs   # LibreOffice headless による PDF 変換
│  │  │  ├─ BatchExportService.cs                  # バッチ / ヘッドレスモードの変換実行
│  │  │  └─ UpdateService.cs                       # 更新確認・ダウンロード
│  │  ├─ Themes/                   # ライト / ダークテーマ
│  │  ├─ ViewModels/               # ViewModel 群
│  │  │  ├─ Coordinators/          # プレビュー・保存フローの調停
│  │  │  ├─ MainViewModel.cs       # メイン画面の統合 ViewModel
│  │  │  ├─ MainViewModelCoordinatorHost.cs  # Coordinator 向けホスト実装
│  │  │  ├─ PreviewPanelViewModel.cs
│  │  │  ├─ ImageExportSettingsViewModel.cs
│  │  │  ├─ WordConversionSettingsViewModel.cs
│  │  │  ├─ ThemeSettingsViewModel.cs
│  │  │  └─ StatusBarViewModel.cs
│  │  ├─ Views/                    # 画面 (XAML)
│  │  │  ├─ Panels/                # メイン画面を構成する各パネル
│  │  │  ├─ Behaviors/             # UI ビヘイビア (D&D など)
│  │  │  ├─ MainWindow.xaml        # メイン画面
│  │  │  ├─ AboutDialog.xaml       # バージョン情報・更新確認・設定リセット
│  │  │  ├─ DropOverlay.xaml       # ドロップ受付オーバーレイ
│  │  │  └─ ConfirmDialog.xaml     # 確認ダイアログ
│  │  ├─ Assets/                   # アイコンなどのリソース
│  │  └─ UpdateReleaseKey.local.props.example  # 更新ダウンロード用キーの設定例
│  └─ PdfConverter.Tests/          # xUnit v3 単体テスト
│     ├─ Commands/
│     ├─ Converters/
│     ├─ Infrastructure/
│     ├─ Services/
│     ├─ Themes/
│     ├─ ViewModels/
│     │  └─ Coordinators/
│     ├─ Views/
│     │  └─ Behaviors/
│     └─ Helpers/                  # テスト補助
├─ installer/                      # Inno Setup スクリプトとビルド用 PowerShell
│  ├─ PdfConverter.iss
│  ├─ build-common.ps1             # .NET SDK 検出・復元など共通関数
│  ├─ build-installer.ps1          # インストーラー生成
│  ├─ build-and-test.ps1           # 復元・ビルド・テスト一括実行
│  ├─ build-user-docs.ps1          # 利用者向けドキュメントの HTML 化 (mdBook)・検証
│  ├─ release.ps1                  # リリース手順の一括実行 (バージョン更新・コミット・インストーラー作成)
│  ├─ release-version.ps1          # release.ps1 のバージョン更新関数
│  ├─ generate-third-party-notices.ps1  # THIRD-PARTY-NOTICES.txt の生成
│  └─ licenses/                    # NuGet パッケージに含まれないライセンス全文・ドキュメント素材の表示
├─ docs/user/                      # 利用者向けドキュメント (mdBook の原稿)
├─ dist/                           # インストーラー出力先 (gitignore 対象)
├─ .claude/rules/                  # コーディング規約 (セクションコメント・日本語表記)
├─ .editorconfig                   # コードスタイル・コード解析ルール
├─ CHANGELOG.md                    # 変更履歴
├─ CLAUDE.md                       # 開発ガイド (AI エージェント向け)
├─ LICENSE
└─ README.md
```

---

## 📜 ライセンス

本プロジェクトは [MIT License](LICENSE) の下で公開されています。

### オープンソースソフトウェア

本アプリは以下のオープンソースコンポーネントを使用しています。

| コンポーネント | バージョン | ライセンス | 用途 |
|:---|:---|:---|:---|
| PDFiumCore | 156.0.8076 | Apache 2.0 | PDF レンダリング (.NET バインディング) |
| PDFium | 156.0.8076 | BSD-3-Clause / Apache 2.0 | PDF ネイティブエンジン (pdfium-binaries) |
| Microsoft.Extensions.DependencyInjection | 10.0.8 | MIT | DI コンテナ |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.8 | MIT | DI 抽象化 |
| Ookii.Dialogs.Wpf | 5.0.1 | BSD-3-Clause | フォルダー選択ダイアログ |
| .NET Runtime / Windows Desktop Runtime | 10 | MIT | 実行ランタイム (self-contained で同梱) |

インストール版には `THIRD-PARTY-NOTICES.txt` が同梱され、各コンポーネントのライセンス全文が記載されています。同梱ドキュメント (mdBook の HTML) に含まれる素材 (mdBook テーマ・highlight.js・clipboard.js・Font Awesome) の表示も末尾に記載しています。アプリ右上の **ⓘ ボタン** (バージョン情報) からも開けます。

バージョン情報ダイアログの **更新を確認** から、公開中の最新インストーラーを確認できます。更新を適用するとインストーラーを起動したうえでアプリを終了します。

### 外部ソフトウェア

以下はユーザーの環境に別途インストールが必要です (本アプリに同梱されません)。

- **Microsoft Word** — Word 文書の変換時 (COM 利用。正規ライセンスが必要)
- **LibreOffice** — Word 文書の変換時の代替手段

### 商標

PDF は Adobe Inc. の登録商標です。Microsoft、Word、Windows は Microsoft Corporation の米国およびその他の国における登録商標または商標です。LibreOffice は The Document Foundation の商標です。これらの名称は、各権利者との提携・後援・承認を示すものではありません。
