<div align="center">

<img src="src/PdfConverter/Assets/app-icon.png" alt="PDF Converter" width="140" />

# PDF Converter

**PDF のページを高品質な画像へ変換する、シンプルな Windows アプリ**

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
| 🌗 | **テーマ切替** | ライト / ダーク / システム設定に追従 |
| 🧠 | **メモリキャッシュ** | 一定サイズ以下の PDF をキャッシュし、I/O を削減 |
| 🛑 | **キャンセル対応** | 処理途中での中断が可能 |

---

## 🛠️ 技術スタック

<div align="left">

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)
![.NET](https://img.shields.io/badge/.NET%20Framework%204.8.1-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-2C68C4?style=for-the-badge&logo=windowsxp&logoColor=white)
![Windows](https://img.shields.io/badge/Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)

</div>

- **アーキテクチャ**: MVVM（View / ViewModel / Coordinator / Service の責務分離）
- **DI コンテナ**: `Microsoft.Extensions.DependencyInjection`
- **PDF レンダリング**: `Docnet.Core` (PDFium)
- **Word → PDF 変換**: Microsoft Word COM または LibreOffice headless（設定で切り替え）
- **ダイアログ**: `Ookii.Dialogs.Wpf`
- **テスト**: `xUnit v3` + `Moq` による単体テスト（`PdfConverter.Tests`）

---

## 📦 必要要件

- Windows 10 / 11
- [.NET Framework 4.8.1](https://dotnet.microsoft.com/download/dotnet-framework/net481)
- Word ファイルを変換する場合: **Microsoft Word**（デスクトップ版）または **LibreOffice** のいずれか
- LibreOffice 使用時: 一般的なインストール先から `soffice.exe` を自動検出（必要に応じてアプリ内でパス指定可能）
- ビルドする場合: Visual Studio 2022 など（NuGet パッケージの復元が必要）

---

## 🏗️ ビルド & 実行

基本は `src/PdfConverter.slnx` を使用します（`src/PdfConverter.sln` は旧ツールチェーン向けの互換用です）。  
以下のコマンドは、`msbuild`/`nuget` が利用できる **Visual Studio Developer PowerShell** での実行を想定しています。

```powershell
# リポジトリの取得
git clone <repo_url>
cd pdf-converter

# NuGet パッケージの復元
nuget restore src/PdfConverter.slnx

# ビルド（Release 構成）
msbuild src/PdfConverter.slnx /p:Configuration=Release
```

ビルド後、`src/PdfConverter/bin/Release/` に `PDF Converter.exe` が生成されます。

---

## 📖 使い方

1. **ファイルを開く** — `参照` ボタンから PDF / Word を選択するか、ウィンドウへ **ドラッグ＆ドロップ**
2. **プレビュー確認** — ページを移動しながら出力内容をチェック
3. **出力設定** — 画像形式・解像度・透過の有無を指定
4. **範囲を選ぶ** — 単一ページ / 範囲指定 / 全ページから選択
5. **保存** — 出力先フォルダを指定して変換実行（`page_1.png` のように連番出力）

---

## 🧪 テスト

単体テストは [xUnit.net v3](https://xunit.net/) で実装されています。テストプロジェクトは **SDK 形式の `net481`** で、ビルド後に生成されるテスト実行ファイルを直接起動して実行できます。

```powershell
# 1. NuGet パッケージの復元（未実施の場合）
nuget restore src/PdfConverter.slnx

# 2. テストプロジェクトをビルド
msbuild src/PdfConverter.Tests/PdfConverter.Tests.csproj /p:Configuration=Debug /p:Platform="Any CPU"

# 3. テスト実行（xUnit v3 In-Process Runner）
src/PdfConverter.Tests/bin/Debug/net481/PdfConverter.Tests.exe
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
│  │  ├─ Infrastructure/           # 例外処理・DI 構成
│  │  ├─ Models/                   # ドメインモデル / 列挙型
│  │  ├─ Services/                 # 変換・I/O・Word/LibreOffice 連携
│  │  ├─ Themes/                   # ライト / ダークテーマ
│  │  ├─ ViewModels/               # ViewModel 群
│  │  │  └─ Coordinators/          # 画面フロー調停
│  │  ├─ Views/                    # 画面 (XAML)
│  │  │  └─ Behaviors/             # UI ビヘイビア
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
├─ LICENSE
└─ README.md
```

---

## 📜 ライセンス

本プロジェクトは [MIT License](LICENSE) の下で公開されています。
