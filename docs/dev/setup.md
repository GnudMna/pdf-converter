# 開発環境とビルド

## 必要なツール

| ツール | 用途 |
| :--- | :--- |
| Windows 10 / 11 | WPF アプリのため Windows でのみビルド・実行できる |
| [.NET SDK 11](https://dotnet.microsoft.com/download/dotnet/11.0) 以降 | ビルド・テスト (dotnet CLI)。Visual Studio 2026 など SDK 11 を含む環境でもよい。最低版はリポジトリ直下の `global.json` (`11.0.100`・`rollForward: latestMajor`) で固定し、それより新しい SDK はそのまま使う |
| PowerShell | `scripts/` のスクリプトの実行 (PowerShell 7 以降。`pwsh` で実行する) |
| [Inno Setup 6](https://jrsoftware.org/isinfo.php) | インストーラーの作成時のみ (`winget install JRSoftware.InnoSetup`) |
| [mdBook](https://github.com/rust-lang/mdBook/releases) 0.5.4 | インストーラー・ポータブル版の作成時のみ。利用者向けドキュメントの HTML 化に使う。`mdbook.exe` を PATH に置く |
| Microsoft Word または LibreOffice | Word 文書の変換を手で確かめる場合のみ。単体テストには不要 |

## ビルド

ソリューションは `src/PdfConverter.slnx`。プラットフォームは `x64` / `x86` のどちらかを明示する。`pdfium.dll` がプラットフォーム固有のため Any CPU には対応しておらず、それ以外を指定すると `ValidateSupportedPlatform` ターゲットがエラーにする。省略時は `Debug` / `x64` になる。

```powershell
# ビルド (パッケージ復元も自動で行われる)
dotnet build src/PdfConverter.slnx -c Debug -p:Platform=x64
dotnet build src/PdfConverter.slnx -c Release -p:Platform=x86

# 復元・ビルド・単体テストの一括実行 (作業の完了前に通す品質チェック)
./scripts/build-and-test.ps1 -NoPause
```

`scripts/` の各スクリプトは、エクスプローラーからのダブルクリック起動と判定すると終了時にキー入力を待つ。ターミナルから実行するときは `-NoPause` を付ける。

### ビルド出力

出力先は `bin\<Platform>\<Configuration>\` にそろえている (`src/Directory.Build.props`)。アプリは .NET ランタイムを同梱する self-contained 構成で、出力フォルダーには `PDF Converter.exe` と依存 DLL (`pdfium.dll` を含む) が同じ階層に並ぶ。

| 構成 | 出力先 |
| :--- | :--- |
| ビルド | `src/PdfConverter/bin/<Platform>/<Configuration>/` |
| 配布用 (publish) | `src/PdfConverter/bin/<Platform>/publish/` |

Release ビルドでは PDB を出力しない。配布用の出力は `scripts/build-installer.ps1` (インストーラー) と `scripts/build-portable.ps1` (ポータブル版の zip) が作る ([インストーラーとリリース](release.md) を参照)。

### コード解析

`src/Directory.Build.props` で `AnalysisLevel=10.0-recommended` と `EnforceCodeStyleInBuild` を有効にし、警告をすべてエラーにしている。CI がないため、ローカルのビルドで必ず検出するための設定である。

- ルールの抑止・オプションは理由を添えて `.editorconfig` に書く
- コード中の `#pragma warning disable` は意図的な違反に限る
- NuGet の脆弱性監査 (NU1901 ～ NU1904) だけは、勧告の公開でビルドが止まらないよう警告のままにしている

### 書式チェック

`build-and-test.ps1` は、ビルドの前に `dotnet format whitespace --verify-no-changes` で `src/` 配下の C# ファイルの書式 (改行コード・文字コード・インデント・末尾の空白など `.editorconfig` の設定) を確かめる。命名・スタイル・解析のルールはビルド (`EnforceCodeStyleInBuild`) で検出するため、ここでは扱わない。違反は次で直す。

```powershell
./scripts/format.ps1 -NoPause
```

Claude Code では、`.claude/settings.json` の PostToolUse フック (`.claude/hooks/format-cs.ps1`) が、編集した C# ファイルに同じ修正を自動でかける。

### 依存パッケージのロック

`src/Directory.Build.props` の `RestorePackagesWithLockFile` で、推移的な依存も含めて各プロジェクトの `packages.lock.json` に版を固定している。`RuntimeIdentifiers` に `win-x64` と `win-x86` を両方指定しているため、どちらのプラットフォームで復元してもロックファイルは同じになる。

`build-and-test.ps1` と `build-installer.ps1` は locked mode で復元し、ロックファイルと食い違うとエラー (NU1004 など) にする。`PackageReference` を変えたら、次でロックファイルを更新して同じ変更に含める。

```powershell
dotnet restore src/PdfConverter.slnx -p:Platform=x64
```

## 実行

ビルド出力の `PDF Converter.exe` を起動する。Visual Studio から実行する場合は、構成を `x64` または `x86` にする。

VS Code では `.vscode/launch.json` の次の構成でデバッグできる (いずれも Debug / x64 でビルドしてから起動する)。

| 構成 | 内容 |
| :--- | :--- |
| PDF Converter | GUI で起動する |
| PDF Converter バッチ変換 | 入力ファイルと出力フォルダーを尋ね、`-o <出力フォルダー> <入力ファイル>` で起動する |
| pdfconv 起動プログラム | `pdfconv.exe` を同じ引数で起動する。本体は子プロセスのため、ブレークポイントは起動プログラム側だけで止まる |
| PdfConverter.Tests | テスト実行ファイルを起動する |

起動引数で GUI とバッチ変換を切り替えられる ([アーキテクチャ](architecture.md#起動の流れ) を参照)。

```powershell
& "src/PdfConverter/bin/x64/Debug/PDF Converter.exe" "C:\docs\report.pdf"                # ファイルを開いて起動
& "src/PdfConverter/bin/x64/Debug/PDF Converter.exe" -o "C:\out" "C:\docs\report.pdf"    # バッチ変換
```

開発中の実行も、ログ・設定・一時ファイルはインストール版と同じ場所を使う ([アーキテクチャ](architecture.md#ファイルの置き場所) を参照)。ポータブル版の動作を確かめるときは、ビルド出力の `PDF Converter.exe` の隣に `portable.txt` (`scripts/portable/portable.txt`) を置く。設定とログはビルド出力の `UserData\` に入る。

## 更新の取得先

バージョン情報ダイアログの **更新を確認** は、GitHub Releases API (`https://api.github.com/repos/GnudMna/pdf-converter/releases/latest`) で最新版を照会し、インストーラー (ポータブル版では zip) を GitHub Release からダウンロードする。リポジトリは公開のため、認証・キーは要らない。未認証の API は IP アドレスごとに 1 時間 60 回までに制限される。

ダウンロードした配布物は、リリース時に作る署名を埋め込み済みの公開鍵で検証できたときだけ起動する。公開鍵は Git に含めて構わず (`UpdateSigningKey.cs`)、空のままだと更新の適用は失敗する。署名用の鍵の作り方は [インストーラーとリリース](release.md#更新の署名) を参照する。

インストール版では、インストーラーを `/SILENT /NORESTART /CLOSEAPPLICATIONS /RELAUNCH=1 /LOG=<作業フォルダー>\setup.log` で起動し、アプリは起動後すぐに終了する。`/SILENT` はウィザードのページを省いて進捗ウィンドウだけを出し、エラーはメッセージボックスで知らせる (`/VERYSILENT` や `/SUPPRESSMSGBOXES` を使わないのは、失敗が利用者に見えなくなるため)。デスクトップアイコン・関連付けなどのタスクは前回の選択を引き継ぐ。`/RELAUNCH=1` は `PdfConverter.iss` の独自引数で、画面なしのインストールの完了後にアプリを起動し直す。手作業でサイレントインストールしたときにアプリが起動しないよう、この引数がなければ起動しない。ポータブル版の適用の流れは [アーキテクチャ](architecture.md#ポータブル版) を参照する。
