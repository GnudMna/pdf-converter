<div align="center">

<img src="src/PdfConverter/Assets/app-icon.png" alt="PDF Converter" width="140" />

# PDF Converter

**PDF / Word 文書のページを高品質な画像へ変換する、シンプルな Windows アプリ**

[![Version](https://img.shields.io/badge/Version-0.7.1-blue)](#)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows&logoColor=white)](#)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](#)
[![Language](https://img.shields.io/badge/Language-C%23-239120?logo=csharp&logoColor=white)](#)
[![UI](https://img.shields.io/badge/UI-WPF%20(MVVM)-2C68C4?logo=windowsxp&logoColor=white)](#)
[![Engine](https://img.shields.io/badge/Engine-PDFium%20(PDFiumCore)-FF5722)](#)
[![License](https://img.shields.io/badge/License-All%20Rights%20Reserved-F59E0B)](LICENSE)

</div>

---

## ✨ 概要

**PDF Converter** は、PDF または Word 文書の各ページを **PNG / JPEG / BMP** の画像として書き出す WPF デスクトップアプリである。
Word ファイルは Microsoft Word または LibreOffice で PDF に変換したうえで画像化する。レンダリングには [PDFium](https://pdfium.googlesource.com/pdfium/) ([PDFiumCore](https://github.com/Dtronix/PDFiumCore)) を使う。変換前にプレビューを確認しながら、解像度や出力範囲を細かく指定できる。

---

## 🚀 主な機能

| | 機能 | 説明 |
| :---: | :--- | :--- |
| 🖼️ | **ページプレビュー** | 変換前に各ページを表示。前後移動・ページ番号ジャンプに対応 |
| 🗂️ | **柔軟な出力範囲** | 単一ページ・ページ範囲 (例: `1-3,5`)・全ページの保存 |
| 📐 | **解像度の指定** | 幅 (px) / 高さ (px) / DPI で出力サイズを指定 |
| 🎨 | **複数の画像形式** | `PNG` / `JPEG` / `BMP` を選択可能 |
| 🫧 | **透過の保持** | PNG 出力時に背景の透明度を保持 |
| ⚡ | **並列処理** | ページを並列レンダリングして高速に保存 |
| 📋 | **クリップボードコピー** | プレビュー中のページを出力解像度でコピー |
| 📄 | **Word 対応** | `.doc` / `.docx` を Word または LibreOffice 経由で PDF 化し、画像へ変換。PDF としての保存も可能 |
| 🖱️ | **ドラッグ&ドロップ** | PDF / Word をウィンドウへドロップするだけで読み込み |
| ⚙️ | **コマンドライン** | 起動時のファイル指定と、ウィンドウを表示しないバッチ変換 |
| 🌗 | **テーマ切替** | ライト / ダーク / システム設定に追従 |
| 🛑 | **キャンセル対応** | 処理途中での中断が可能 |
| ♿ | **アクセシビリティ** | スクリーンリーダー対応 (ラベル関連付け・Live Region によるステータス読み上げ) |
| 🔄 | **更新確認** | バージョン情報から最新インストーラーを確認・ダウンロードして適用 |

---

## 📦 動作環境

- Windows 10 / 11 (x64 版・x86 版)
- .NET ランタイムのインストールは不要 (アプリに同梱する self-contained 配布)
- Word ファイルを変換する場合: **Microsoft Word** (デスクトップ版) または **LibreOffice** のいずれか
  - LibreOffice は一般的なインストール先から `soffice.exe` を自動で探す (アプリ内でパスも指定できる)

インストーラーは管理者権限を要求しないユーザー単位インストールで、`%LOCALAPPDATA%\Programs\PDF Converter` に入る。

---

## 📖 使い方

使い方・コマンドライン (バッチ変換)・トラブルシューティングは、利用者向けドキュメント [`docs/user/`](docs/user/introduction.md) にまとめている。

| ページ | 内容 |
| :--- | :--- |
| [はじめに](docs/user/introduction.md) | できること・動作環境 |
| [使い方](docs/user/usage.md) | ファイルの開き方・プレビュー・変換設定・保存・Word 設定・テーマ・更新 |
| [コマンドライン](docs/user/cli.md) | 起動時のファイル指定と、バッチ変換のオプション・終了コード |
| [トラブルシューティング](docs/user/troubleshooting.md) | ログの場所・よくあるエラー・設定の初期化・アンインストール |
| [変更履歴](CHANGELOG.md) | バージョンごとの変更内容 |

インストール版には HTML 化したドキュメントが `docs\` に同梱され、スタートメニューの **PDF Converter ドキュメント**、アプリ右上の **?** ボタン、`F1` キーから開ける。

---

## 🛠️ 開発

<div align="left">

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)
![.NET](https://img.shields.io/badge/.NET%2010-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-2C68C4?style=for-the-badge&logo=windowsxp&logoColor=white)
![Windows](https://img.shields.io/badge/Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)

</div>

- **アーキテクチャ**: WPF / MVVM。画面の手順は Coordinator、変換・I/O は Service に分離
- **DI コンテナ**: `Microsoft.Extensions.DependencyInjection`
- **PDF レンダリング**: `PDFiumCore` (PDFium。[pdfium-binaries](https://github.com/bblanchon/pdfium-binaries) のビルドを同梱)
- **Word → PDF 変換**: Microsoft Word COM または LibreOffice headless (設定で切り替え)
- **テスト**: `xUnit v3` + `Moq`

[.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) 以降があればビルドできる。プラットフォームは `x64` / `x86` を明示する (Any CPU は非対応)。

```powershell
# 復元・ビルド・単体テストの一括実行
./installer/build-and-test.ps1 -NoPause

# ビルドだけ
dotnet build src/PdfConverter.slnx -c Release -p:Platform=x64
```

開発環境・設計・テスト・インストーラーの作成・リリース手順は、開発者向けドキュメント [`docs/dev/`](docs/dev/README.md) にまとめている。

| ページ | 内容 |
| :--- | :--- |
| [開発環境とビルド](docs/dev/setup.md) | 必要なツール・ビルド・実行・更新ダウンロード用キー |
| [アーキテクチャ](docs/dev/architecture.md) | 起動の流れ・DI・MVVM と Coordinator・各サービスの責務 |
| [テスト](docs/dev/testing.md) | 単体テストの実行と書き方 |
| [インストーラーとリリース](docs/dev/release.md) | インストーラー作成・同梱ドキュメント・リリース手順 |
| [開発の進め方](docs/dev/contributing.md) | ブランチ・コミット・コーディング規約・ドキュメント同期 |

---

## 📜 ライセンス

本ソフトウェアの著作権は著作者が保有する (All Rights Reserved)。利用には著作者の事前の許可が必要で、許可のない複製・改変・再配布・商用利用を禁止する。条件は [LICENSE](LICENSE) を参照する。

### オープンソースソフトウェア

本アプリは次のオープンソースコンポーネントを使用している。

| コンポーネント | バージョン | ライセンス | 用途 |
| :--- | :--- | :--- | :--- |
| PDFiumCore | 157.0.8100 | Apache 2.0 | PDF レンダリング (.NET バインディング) |
| PDFium | 157.0.8100 | BSD-3-Clause / Apache 2.0 | PDF ネイティブエンジン (pdfium-binaries) |
| Microsoft.Extensions.DependencyInjection | 10.0.14 | MIT | DI コンテナ |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.14 | MIT | DI 抽象化 |
| Ookii.Dialogs.Wpf | 5.0.1 | BSD-3-Clause | フォルダー選択ダイアログ |
| .NET Runtime / Windows Desktop Runtime | 10 | MIT | 実行ランタイム (self-contained で同梱) |

インストール版には、各コンポーネントのライセンス全文を記載した `THIRD-PARTY-NOTICES.txt` を同梱する。同梱ドキュメント (mdBook の HTML) に含まれる素材 (mdBook テーマ・highlight.js・clipboard.js・Font Awesome) の表示も末尾に記載している。アプリ右上の **ⓘ** ボタン (バージョン情報) からも開ける。

### 外部ソフトウェア

次は利用者の環境に別途インストールが必要である (本アプリには同梱しない)。

- **Microsoft Word** — Word 文書の変換時 (COM 利用。正規ライセンスが必要)
- **LibreOffice** — Word 文書の変換時の代替手段

### 商標

PDF は Adobe Inc. の登録商標である。Microsoft、Word、Windows は Microsoft Corporation の米国およびその他の国における登録商標または商標である。LibreOffice は The Document Foundation の商標である。これらの名称は、各権利者との提携・後援・承認を示すものではない。
