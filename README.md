<div align="center">

<img src="src/PdfConverter/Assets/app-icon.png" alt="PDF Converter" width="140" />

# PDF Converter

**PDF / Word 文書のページを高品質な画像へ変換する、シンプルな Windows アプリ**

[![Version](https://img.shields.io/github/v/release/GnudMna/pdf-converter?label=Version&color=blue)](https://github.com/GnudMna/pdf-converter/releases/latest)
[![Platform](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6)](#動作環境)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

</div>

## 概要

**PDF Converter** は、PDF または Word 文書の各ページを **PNG / JPEG / BMP** の画像として書き出すデスクトップアプリである。
変換前にプレビューで確認しながら、解像度と出力するページを指定できる。Word 文書は Microsoft Word または LibreOffice で PDF に変換してから画像化する。

## 主な機能

**変換**

| 機能 | 説明 |
| :--- | :--- |
| 画像形式 | PNG / JPEG / BMP。JPEG は品質 (1〜100) も指定できる |
| 解像度 | 幅 (px)・高さ (px)・DPI のいずれかで出力サイズを指定 |
| 出力範囲 | 単一ページ・ページ範囲 (例: `1-3,5`)・全ページ |
| 透明度 | PNG で背景の透明度を保持 |
| Word 文書 | `.doc` / `.docx` を Word または LibreOffice で PDF 化して画像へ変換。PDF としての保存も可能 |
| 並列処理 | ページを並列に描画して高速に保存 |

**操作**

| 機能 | 説明 |
| :--- | :--- |
| プレビュー | 変換前に各ページを表示。前後移動・ページ番号での移動に対応 |
| ファイルの読み込み | 参照・パスの入力・ドラッグ&ドロップ・起動時の引数で指定 |
| クリップボード | 表示中のページを出力解像度でコピー |
| キャンセル | 変換・保存を途中で中断 |
| テーマ | ライト / ダーク / Windows の設定に追従 |
| アクセシビリティ | スクリーンリーダーに対応 (ラベルの関連付け・ステータスの読み上げ) |

**自動化・そのほか**

| 機能 | 説明 |
| :--- | :--- |
| バッチ変換 | ウィンドウを表示せずにコマンドラインで変換。複数ファイルの一括変換に対応 |
| `pdfconv.exe` | コンソール用の起動プログラム。変換の終了を待ち、終了コードを返す |
| 更新 | バージョン情報から最新版を確認し、そのまま更新 |

## 動作環境

- Windows 10 / 11 (x64 版・x86 版)
- .NET ランタイムのインストールは不要 (アプリに同梱する self-contained 配布)
- Word 文書を変換する場合: **Microsoft Word** (デスクトップ版) または **LibreOffice** のいずれか
  - LibreOffice は一般的なインストール先から `soffice.exe` を自動で探す (アプリ内でパスも指定できる)

## インストール

[GitHub Releases](https://github.com/GnudMna/pdf-converter/releases/latest) から、環境に合うインストーラー (`PdfConverter-<版>-x64-setup.exe` または `-x86-setup.exe`) をダウンロードして実行する。

- 管理者権限は不要。ユーザー単位で `%LOCALAPPDATA%\Programs\PDF Converter` にインストールする
- 以降の更新は、アプリのバージョン情報 (右上の **ⓘ** ボタン) の **更新を確認** から行える

インストールせずに使う場合は、ポータブル版 (`PdfConverter-<版>-x64-portable.zip` または `-x86-portable.zip`) を任意のフォルダーに展開し、`PDF Converter.exe` を実行する。設定とログは展開したフォルダーの `UserData\` に保存し、**更新を確認** からの更新にも対応する。違いは [インストール版とポータブル版](docs/user/introduction.md#インストール版とポータブル版) を参照する。

## ドキュメント

使い方は利用者向けドキュメント [`docs/user/`](docs/user/introduction.md) にまとめている。インストール版・ポータブル版とも HTML 版を同梱しており、アプリ右上の **?** ボタン・`F1` キー (インストール版はスタートメニューの **PDF Converter ドキュメント** も) から開ける。

| ページ | 内容 |
| :--- | :--- |
| [はじめに](docs/user/introduction.md) | できること・動作環境 |
| [使い方](docs/user/usage.md) | ファイルの開き方・プレビュー・変換設定・保存・Word 設定・テーマ・更新 |
| [コマンドライン](docs/user/cli.md) | 起動時のファイル指定と、バッチ変換のオプション・終了コード |
| [トラブルシューティング](docs/user/troubleshooting.md) | ログの場所・よくあるエラー・設定の初期化・アンインストール |
| [変更履歴](CHANGELOG.md) | バージョンごとの変更内容 |

## 開発

| 項目 | 内容 |
| :--- | :--- |
| 言語・UI | C# / .NET 10 / WPF (MVVM)。画面の手順は Coordinator、変換・I/O は Service に分離 |
| PDF レンダリング | [PDFium](https://pdfium.googlesource.com/pdfium/) ([PDFiumCore](https://github.com/Dtronix/PDFiumCore)。ネイティブ DLL は [pdfium-binaries](https://github.com/bblanchon/pdfium-binaries) のビルド) |
| Word → PDF 変換 | Microsoft Word (COM) または LibreOffice (headless) を設定で切り替え |
| DI | `Microsoft.Extensions.DependencyInjection` |
| テスト | xUnit v3 + Moq |

[.NET SDK 11](https://dotnet.microsoft.com/download/dotnet/11.0) 以降でビルドできる。プラットフォームは `x64` / `x86` を明示する (Any CPU は非対応)。

```powershell
# 復元・ビルド・単体テストの一括実行
./scripts/build-and-test.ps1 -NoPause

# ビルドだけ
dotnet build src/PdfConverter.slnx -c Release -p:Platform=x64
```

開発環境・設計・テスト・インストーラーとポータブル版の作成・リリース手順は、開発者向けドキュメント [`docs/dev/`](docs/dev/README.md) にまとめている。

| ページ | 内容 |
| :--- | :--- |
| [開発環境とビルド](docs/dev/setup.md) | 必要なツール・ビルド・実行・更新の取得先 |
| [アーキテクチャ](docs/dev/architecture.md) | 起動の流れ・DI・MVVM と Coordinator・各サービスの責務・ファイルの置き場所 |
| [テスト](docs/dev/testing.md) | 単体テストの実行と書き方 |
| [インストーラーとリリース](docs/dev/release.md) | インストーラー・ポータブル版の作成・同梱ドキュメント・サードパーティ通知・リリース手順 |
| [開発の進め方](docs/dev/contributing.md) | ブランチ・コミット・コーディング規約・ドキュメント同期・依存パッケージの更新 |

## ライセンス

本プロジェクトは [MIT License](LICENSE) の下で公開している。

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

インストール版・ポータブル版には、各コンポーネントのライセンス全文を記載した `THIRD-PARTY-NOTICES.txt` を同梱する。同梱ドキュメント (mdBook の HTML) に含まれる素材 (mdBook テーマ・highlight.js・clipboard.js・Font Awesome) の表示も末尾に記載している。アプリのバージョン情報 (右上の **ⓘ** ボタン) からも開ける。

### 外部ソフトウェア

次は利用者の環境に別途インストールが必要である (本アプリには同梱しない)。

- **Microsoft Word** — Word 文書の変換時 (COM 利用。正規ライセンスが必要)
- **LibreOffice** — Word 文書の変換時の代替手段

### 商標

PDF は Adobe Inc. の登録商標である。Microsoft、Word、Windows は Microsoft Corporation の米国およびその他の国における登録商標または商標である。LibreOffice は The Document Foundation の商標である。これらの名称は、各権利者との提携・後援・承認を示すものではない。
