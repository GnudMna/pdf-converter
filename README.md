<div align="center">

<img src="src/PdfConverter/Assets/app-icon.png" alt="PDF Converter" width="140" />

# PDF Converter

**PDF / Word 文書のページを高品質な画像へ変換する、シンプルな Windows アプリ**

[![Version](https://img.shields.io/badge/Version-0.4.0-blue)](#)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows&logoColor=white)](#)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8.1-512BD4?logo=dotnet&logoColor=white)](#)
[![Language](https://img.shields.io/badge/Language-C%23-239120?logo=csharp&logoColor=white)](#)
[![UI](https://img.shields.io/badge/UI-WPF%20(MVVM)-2C68C4?logo=windowsxp&logoColor=white)](#)
[![Engine](https://img.shields.io/badge/Engine-PDFium%20(Docnet.Core)-FF5722)](#)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

</div>

---

## ✨ 概要

**PDF Converter**は、PDF または Word 文書の各ページを**PNG / JPEG / BMP** の画像として書き出せる WPF デスクトップアプリです。
Word ファイルは Microsoft Word 経由で PDF に変換したうえで、レンダリングには[PDFium](https://pdfium.googlesource.com/pdfium/)（[Docnet.Core](https://github.com/GowenGit/docnet)）を採用します。変換前にプレビューを確認しながら、解像度や出力範囲を細かく指定できます。

---

## 🚀 主な機能

| | 機能 | 説明 |
|:---:|:---|:---|
| 🖼️ | **ページプレビュー** | 変換前に各ページを表示。前後移動・ページ番号ジャンプに対応 |
| 🗂️ | **柔軟な出力範囲** | 単一ページ・ページ範囲（例: `1-3,5`）・全ページの保存 |
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
| 🧠 | **メモリキャッシュ** | 一定サイズ以下の PDF をキャッシュし、I/O を削減 |
| 🛑 | **キャンセル対応** | 処理途中での中断が可能 |
| ♿ | **アクセシビリティ** | スクリーンリーダー対応（ラベル関連付け・Live Region によるステータス読み上げ） |

---

## 🛠️ 技術スタック

<div align="left">

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)
![.NET](https://img.shields.io/badge/.NET%20Framework%204.8.1-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-2C68C4?style=for-the-badge&logo=windowsxp&logoColor=white)
![Windows](https://img.shields.io/badge/Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)

</div>

- **アーキテクチャ**: MVVM（パネル単位の View / ViewModel、`MainViewModelCoordinatorHost` による Coordinator 連携、Service の責務分離）
- **DI コンテナ**: `Microsoft.Extensions.DependencyInjection`
- **PDF レンダリング**: `Docnet.Core` (PDFium)
- **Word → PDF 変換**: Microsoft Word COM または LibreOffice headless（設定で切り替え）
- **ダイアログ**: `Ookii.Dialogs.Wpf`
- **テスト**: `xUnit v3` + `Moq` による単体テスト（`PdfConverter.Tests`）

---

## 📦 必要要件

- Windows 10 / 11（x86 / x64 いずれのビルドも利用可能）
- [.NET Framework 4.8.1](https://dotnet.microsoft.com/download/dotnet-framework/net481)
- Word ファイルを変換する場合: **Microsoft Word**（デスクトップ版）または **LibreOffice** のいずれか
- LibreOffice 使用時: 一般的なインストール先から `soffice.exe` を自動検出（必要に応じてアプリ内でパス指定可能）
- ビルドする場合: Visual Studio 2019 以降など（NuGet パッケージの復元が必要）
- インストーラーを作成する場合: [Inno Setup 6](https://jrsoftware.org/isinfo.php)

---

## 🏗️ ビルド & 実行

基本は `src/PdfConverter.slnx` を使用します（`src/PdfConverter.sln` は旧ツールチェーン向けの互換用です）。  
テストプロジェクトは `PdfConverter.slnx` にのみ含まれます。`installer/` のビルドスクリプトは MSBuild 17.10 未満の環境では自動的に `PdfConverter.sln` へフォールバックします（テスト実行には MSBuild 17.10 以降が必要です）。  
以下のコマンドは、`msbuild`/`nuget` が利用できる **Visual Studio Developer PowerShell** での実行を想定しています。

```powershell
# リポジトリの取得
git clone <repo_url>
cd pdf-converter

# NuGet パッケージの復元
nuget restore src/PdfConverter.slnx

# ビルド（Release / x64 構成 — 既定）
msbuild src/PdfConverter.slnx /p:Configuration=Release /p:Platform=x64

# x86 向けにビルドする場合
msbuild src/PdfConverter.slnx /p:Configuration=Release /p:Platform=x86

# x86 と x64 の両方をまとめてビルドする場合
msbuild src/PdfConverter/PdfConverter.csproj /t:BuildAllPlatforms /p:Configuration=Release
```

PDFium のネイティブ DLL（`pdfium.dll`）はプラットフォーム固有のため、**Any CPU 構成はサポートしていません**。ビルド時は `x64` または `x86` を明示してください（Visual Studio では既定が x64）。

ビルド後、プラットフォームに応じて次の場所に `PDF Converter.exe` が生成されます。

| プラットフォーム | 出力先 |
|:---|:---|
| `x64` | `src/PdfConverter/bin/x64/Release/` |
| `x86` | `src/PdfConverter/bin/x86/Release/` |

ビルド後の出力フォルダ構成は次のとおりです。`PDF Converter.exe` はルートに配置され、依存するマネージド DLL と PDFium のネイティブ DLL（`pdfium.dll`）は MSBuild ターゲット `OrganizeOutputLayout` により `lib\` サブフォルダへ自動配置されます。Release ビルドでは PDB は出力から除外されます。起動時に `SetDllDirectory` で `lib\` を検索パスに追加するため、配布時もこの構成を維持してください。

```text
PDF Converter.exe
lib/
├─ Docnet.Core.dll
├─ pdfium.dll
└─ …（その他の依存 DLL）
```

---

## 📦 インストーラー

[Inno Setup 6](https://jrsoftware.org/isinfo.php) を使ったセットアップ EXE を `installer/` から作成できます。管理者権限を要求しない**ユーザー単位インストール**で、`%LOCALAPPDATA%\Programs\PDF Converter` に配置されます。

```powershell
cd installer

# x64 版（既定）— NuGet 復元・Release ビルド・インストーラー生成を一括実行
.\build-installer.ps1

# x86 と x64 の両方
.\build-installer.ps1 -Platform all

# アプリはビルド済みの場合
.\build-installer.ps1 -SkipBuild

# パッケージ復元をスキップ（直前に復元済みの場合）
.\build-installer.ps1 -SkipRestore

# ターミナル向け（完了後のキー入力待ちを省略）
.\build-installer.ps1 -NoPause
```

生成物は `dist/` に出力されます（例: `PdfConverter-x.x.x-x64-setup.exe`）。バージョン番号は `src/PdfConverter/Properties/AssemblyInfo.cs` から自動取得され、Inno Setup へ `/DAppVersion` として渡されます。`AppVersion` 未指定で `PdfConverter.iss` を直接コンパイルするとエラーになります。

| 項目 | 内容 |
|:---|:---|
| 前提条件チェック | .NET Framework 4.8.1 の有無を確認 |
| スタートメニュー | ショートカットを自動作成 |
| デスクトップショートカット | オプション |
| ファイル関連付け | オプション（`.pdf` / `.doc` / `.docx`） |
| ウィザード UI | 日本語 / 英語 |

---

## 📖 使い方

1. **ファイルを開く** — 次のいずれかの方法で PDF / Word を読み込む
   - `参照` ボタンからファイルを選択
   - ウィンドウへ **ドラッグ＆ドロップ**
   - コマンドライン引数で起動（例: `"PDF Converter.exe" "C:\docs\sample.pdf"`）
2. **プレビュー確認** — ページを移動しながら出力内容をチェック
3. **出力設定** — 画像形式・解像度・透過の有無を指定
4. **範囲を選ぶ** — 単一ページ / 範囲指定 / 全ページから選択
5. **保存** — 出力先フォルダを指定して変換実行（`page_1.png` のように連番出力）

### コマンドライン引数

#### GUI 起動

起動時に PDF / Word のパスを渡すと、アプリ起動後に自動で読み込みます。複数の引数が渡された場合は、先頭から順に検証し、最初の有効なパスを使用します。

```powershell
# 絶対パスで指定
& ".\PDF Converter.exe" "C:\docs\report.pdf"

# 相対パスでも指定可能（起動時の作業ディレクトリ基準）
& ".\PDF Converter.exe" ".\sample.docx"
```

エクスプローラーで PDF / Word をこのアプリにドラッグする、または「プログラムから開く」に登録して使うこともできます。

#### バッチ / ヘッドレスモード

`--output` などのスイッチを 1 つでも指定すると、ウィンドウを表示せずに変換だけを実行して終了します。スクリプトやタスクスケジューラからの自動化に使えます。進捗やエラーは標準出力 / 標準エラーへ表示され、詳細は GUI モードと同じログファイルにも記録されます。

```powershell
# PDF の全ページを PNG 画像として出力
& ".\PDF Converter.exe" --output "C:\out" "C:\docs\report.pdf"

# ページ範囲・形式・解像度を指定（-o=value 形式も可）
& ".\PDF Converter.exe" -o "C:\out" -f png -p "1-3,5" --resolution-mode width -r 1920 "report.pdf"

# 複数ファイルを一括変換（各ファイルは <出力先>\<ファイル名>\page_1.png 形式）
& ".\PDF Converter.exe" -o "C:\out" "a.pdf" "b.pdf"

# Word を PDF として保存（出力先に .pdf を指定するとそのパスへ直接保存）
& ".\PDF Converter.exe" --export-pdf -o "C:\out\proposal.pdf" "proposal.docx"

# 既存ファイルを上書き
& ".\PDF Converter.exe" -o "C:\out" --overwrite "report.pdf"

# 先頭が - のパスを渡す場合は -- で区切る
& ".\PDF Converter.exe" -o "C:\out" -- "-report.pdf"

# ヘルプを表示
& ".\PDF Converter.exe" --help
```

**出力先の構成**

| 入力 | 画像出力先 |
|:---|:---|
| 1 ファイル | `<出力先>\page_1.png`、`<出力先>\page_2.png`、… |
| 複数ファイル | `<出力先>\<ファイル名>\page_1.png`、… |

| オプション | 説明 |
|:---|:---|
| `--output`, `-o <path>` | 画像の出力先フォルダー、または PDF 出力先（必須） |
| `--pages`, `-p <範囲>` | 出力ページ（例: `1-3,5`）。省略時は全ページ |
| `--format`, `-f <形式>` | `png` / `jpeg`（`jpg` 可）/ `bmp`（省略時は保存済み設定） |
| `--resolution-mode <方式>` | `default` / `width` / `height` / `dpi` |
| `--resolution`, `-r <数値>` | 解像度の数値 |
| `--transparent` | 透明度を保持（PNG のみ） |
| `--no-transparent` | 白背景に合成 |
| `--overwrite` | 既存ファイルを確認せず上書き |
| `--export-pdf` | Word を PDF として保存（画像変換は行わない） |
| `--quiet`, `-q` | 進捗メッセージを抑制 |
| `--help`, `-h`, `/?`, `--?` | ヘルプを表示 |

終了コード: `0` = 成功、`1` = 変換エラー、`2` = 引数エラー

### トラブルシューティング

問題が発生した場合、次のログを確認できます（UTF-8 テキスト、最大 2 MB でローテーション）。

| ファイル | 内容 |
|:---|:---|
| `%LOCALAPPDATA%\PDF Converter\logs\app.log` | 操作ログ・警告 |
| `%LOCALAPPDATA%\PDF Converter\logs\error.log` | エラーと例外の詳細 |

---

## 🧪 テスト

単体テストは [xUnit.net v3](https://xunit.net/) で実装されています。テストプロジェクトは **SDK 形式の `net481`** で、ビルド後に生成されるテスト実行ファイルを直接起動して実行できます。

```powershell
cd installer

# 復元・ビルド・テストを一括実行（推奨）
.\build-and-test.ps1

# Release 構成で実行
.\build-and-test.ps1 -Configuration Release
```

手動で実行する場合:

```powershell
# 1. NuGet パッケージの復元（未実施の場合）
nuget restore src/PdfConverter.slnx

# 2. テストプロジェクトをビルド
msbuild src/PdfConverter.Tests/PdfConverter.Tests.csproj /p:Configuration=Debug /p:Platform=x64

# 3. テスト実行（xUnit v3 In-Process Runner）
src/PdfConverter.Tests/bin/x64/Debug/net481/PdfConverter.Tests.exe
```

Visual Studio のテストエクスプローラーからも実行できます（`xunit.runner.visualstudio` を使用）。

---

## 📂 プロジェクト構成

```text
pdf-converter/
├─ src/
│  ├─ PdfConverter.slnx            # メインで使用するソリューション
│  ├─ PdfConverter.sln             # 互換用途（旧ツールチェーン向け）
│  ├─ PdfConverter/                # アプリ本体 (WPF / MVVM)
│  │  ├─ Commands/                 # コマンド実装
│  │  ├─ Converters/               # 値変換
│  │  ├─ Infrastructure/           # ログ・例外処理・DI 構成・起動補助・CLI 解析
│  │  │  ├─ BatchCliParser.cs      # バッチ / ヘッドレスモードの引数解析
│  │  │  ├─ BatchCliOptions.cs   # バッチモードのオプション
│  │  │  └─ BatchConsoleHelper.cs # バッチモードのコンソール出力
│  │  ├─ Models/                   # ドメインモデル / 列挙型
│  │  ├─ Services/                 # 変換・I/O・Word/LibreOffice 連携・バッチ変換
│  │  │  ├─ BatchExportService.cs # バッチ / ヘッドレスモードの変換実行
│  │  │  └─ IBatchExportService.cs
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
│  │  │  ├─ Behaviors/             # UI ビヘイビア（D&D など）
│  │  │  ├─ DropOverlay.xaml       # ドロップ受付オーバーレイ
│  │  │  └─ ConfirmDialog.xaml     # 確認ダイアログ
│  │  └─ Assets/                   # アイコンなどのリソース
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
│  ├─ build-common.ps1             # 復元・MSBuild 検出など共通関数
│  ├─ build-installer.ps1          # インストーラー生成
│  ├─ build-and-test.ps1           # 復元・ビルド・テスト一括実行
│  └─ generate-third-party-notices.ps1
├─ dist/                           # インストーラー出力先（gitignore 対象）
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
| Docnet.Core | 2.6.0 | MIT | PDF レンダリング (.NET ラッパー) |
| PDFium | Docnet.Core 同梱 | Apache 2.0 | PDF ネイティブエンジン |
| Microsoft.Extensions.DependencyInjection | 10.0.8 | MIT | DI コンテナ |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.8 | MIT | DI 抽象化 |
| Microsoft.Bcl.AsyncInterfaces | 10.0.8 | MIT | 非同期インターフェース |
| Ookii.Dialogs.Wpf | 5.0.1 | BSD-3-Clause | フォルダー選択ダイアログ |
| System.Runtime.CompilerServices.Unsafe | 6.1.2 | MIT | 低レベル型操作 |
| System.Threading.Tasks.Extensions | 4.6.3 | MIT | タスク拡張 |

インストール版には `THIRD-PARTY-NOTICES.txt` が同梱され、各コンポーネントのライセンス全文が記載されています。アプリ右上の **ⓘ ボタン**（バージョン情報）からも開けます。

### 外部ソフトウェア

以下はユーザーの環境に別途インストールが必要です（本アプリに同梱されません）。

- **.NET Framework 4.8.1** — ランタイム（Microsoft）
- **Microsoft Word** — Word 文書の変換時（COM 利用。正規ライセンスが必要）
- **LibreOffice** — Word 文書の変換時の代替手段

### 商標

PDF は Adobe Inc. の登録商標です。Microsoft、Word、Windows は Microsoft Corporation の米国およびその他の国における登録商標または商標です。LibreOffice は The Document Foundation の商標です。これらの名称は、各権利者との提携・後援・承認を示すものではありません。
