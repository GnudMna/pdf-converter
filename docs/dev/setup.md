# 開発環境とビルド

## 必要なツール

| ツール | 用途 |
| :--- | :--- |
| Windows 10 / 11 | WPF アプリのため Windows でのみビルド・実行できる |
| [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) 以降 | ビルド・テスト (dotnet CLI)。Visual Studio 2026 など SDK 10 を含む環境でもよい |
| PowerShell | `installer/` のスクリプトの実行。Windows PowerShell 5.1 でも動くように書いている |
| [Inno Setup 6](https://jrsoftware.org/isinfo.php) | インストーラーの作成時のみ (`winget install JRSoftware.InnoSetup`) |
| [mdBook](https://github.com/rust-lang/mdBook/releases) 0.5.4 | インストーラーの作成時のみ。利用者向けドキュメントの HTML 化に使う。`mdbook.exe` を PATH に置く |
| Microsoft Word または LibreOffice | Word 文書の変換を手で確かめる場合のみ。単体テストには不要 |

## ビルド

ソリューションは `src/PdfConverter.slnx`。プラットフォームは `x64` / `x86` のどちらかを明示する。`pdfium.dll` がプラットフォーム固有のため Any CPU には対応しておらず、それ以外を指定すると `ValidateSupportedPlatform` ターゲットがエラーにする。省略時は `Debug` / `x64` になる。

```powershell
# ビルド (パッケージ復元も自動で行われる)
dotnet build src/PdfConverter.slnx -c Debug -p:Platform=x64
dotnet build src/PdfConverter.slnx -c Release -p:Platform=x86

# 復元・ビルド・単体テストの一括実行 (作業の完了前に通す品質チェック)
./installer/build-and-test.ps1 -NoPause
```

`installer/` の各スクリプトは、エクスプローラーからのダブルクリック起動と判定すると終了時にキー入力を待つ。ターミナルから実行するときは `-NoPause` を付ける。

### ビルド出力

出力先は `bin\<Platform>\<Configuration>\` にそろえている (`src/Directory.Build.props`)。アプリは .NET ランタイムを同梱する self-contained 構成で、出力フォルダーには `PDF Converter.exe` と依存 DLL (`pdfium.dll` を含む) が同じ階層に並ぶ。

| 構成 | 出力先 |
| :--- | :--- |
| ビルド | `src/PdfConverter/bin/<Platform>/<Configuration>/` |
| 配布用 (publish) | `src/PdfConverter/bin/<Platform>/publish/` |

Release ビルドでは PDB を出力しない。配布用の出力は `installer/build-installer.ps1` が作る ([インストーラーとリリース](release.md) を参照)。

### コード解析

`src/Directory.Build.props` で `AnalysisLevel=10.0-recommended` と `EnforceCodeStyleInBuild` を有効にし、警告をすべてエラーにしている。CI がないため、ローカルのビルドで必ず検出するための設定である。

- ルールの抑止・オプションは理由を添えて `.editorconfig` に書く
- コード中の `#pragma warning disable` は意図的な違反に限る
- NuGet の脆弱性監査 (NU1901 ～ NU1904) だけは、勧告の公開でビルドが止まらないよう警告のままにしている

## 実行

ビルド出力の `PDF Converter.exe` を起動する。Visual Studio から実行する場合は、構成を `x64` または `x86` にする。

起動引数で GUI とバッチ変換を切り替えられる ([アーキテクチャ](architecture.md#起動の流れ) を参照)。

```powershell
& "src/PdfConverter/bin/x64/Debug/PDF Converter.exe" "C:\docs\report.pdf"                # ファイルを開いて起動
& "src/PdfConverter/bin/x64/Debug/PDF Converter.exe" -o "C:\out" "C:\docs\report.pdf"    # バッチ変換
```

開発中の実行も、ログ・設定・一時ファイルはインストール版と同じ場所を使う ([アーキテクチャ](architecture.md#ファイルの置き場所) を参照)。

## 更新ダウンロード用キー

バージョン情報ダイアログの **更新を確認** は、更新サーバー (gh-release-bridge) からインストーラーをダウンロードする。ダウンロードには、ビルド時に埋め込むキーが必要である。

キーは次のどちらかで設定する。キーは Git に含めない。

- `src/PdfConverter/UpdateReleaseKey.local.props.example` を `UpdateReleaseKey.local.props` にコピーし、`PdfConverterReleaseKey` に値を書く (Git 管理外)
- 環境変数 `PDF_CONVERTER_KEY` を設定する

ビルド時に `GenerateUpdateReleaseKey` ターゲットが `obj/` 配下へ `UpdateReleaseKey.g.cs` を生成し、値を埋め込む。キーがなくてもビルド・起動・更新の確認はでき、更新の **適用** だけが失敗する。配布するビルドにはキーが必須のため、`installer/release.ps1` は未設定ならエラーにする。

ダウンロードしたインストーラーは、リリース時に作る署名を埋め込み済みの公開鍵で検証できたときだけ起動する。公開鍵は Git に含めて構わず (`UpdateSigningKey.cs`)、空のままだと更新の適用は失敗する。署名用の鍵の作り方は [インストーラーとリリース](release.md#更新の署名) を参照する。
