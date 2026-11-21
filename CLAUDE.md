# PDF Converter

PDF / Word 文書のページを画像 (PNG / JPEG / BMP) へ変換する Windows デスクトップアプリ。機能・使い方は [README.md](README.md)、利用者向けドキュメントの原稿は [docs/user/](docs/user/)、変更履歴は [CHANGELOG.md](CHANGELOG.md) を参照する。

## 技術スタック

- C# / .NET 10 (`net10.0-windows`, self-contained 配布) / WPF (MVVM)
- PDF レンダリング: PDFium (PDFiumCore。ネイティブ DLL は bblanchon.PDFium.Win32)
- DI: Microsoft.Extensions.DependencyInjection
- テスト: xUnit v3 + Moq (`net10.0-windows`)

## 構成

- `src/PdfConverter/` — アプリ本体
  - `Commands/` `Converters/` `Models/` `Themes/` `Views/` `ViewModels/` (`Coordinators/` でプレビュー・保存フローを調停)
  - `Services/` — 変換・I/O・Word/LibreOffice 連携・バッチ変換
  - `Infrastructure/` — ログ・例外処理・DI 構成・設定初期化・CLI 解析
- `src/PdfConverter.Tests/` — 単体テスト (本体と同じフォルダ構成)
- `installer/` — Inno Setup スクリプトとビルド用 PowerShell。共通処理は `build-common.ps1`
- `docs/user/` — 利用者向けドキュメント (mdBook の原稿。`changelog.md` は `CHANGELOG.md` を取り込むだけのため直接編集しない)

## コマンド

.NET SDK 10 以降 (dotnet CLI) でビルドする。ソリューションは `src/PdfConverter.slnx`。プラットフォームは `x64` / `x86` を必ず明示する (Any CPU は `pdfium.dll` がプラットフォーム固有のため非対応)。

```powershell
# 復元・ビルド・単体テストの一括実行 (完了前の品質チェック。警告もビルドエラーになる)
./installer/build-and-test.ps1 -NoPause
./installer/build-and-test.ps1 -NoPause -Configuration Release -Platform x86
./installer/build-and-test.ps1 -NoPause -SkipRestore   # 復元済みなら省略できる

# ビルドだけ
dotnet build src/PdfConverter.slnx -c Debug -p:Platform=x64

# テストだけ (ビルド後に実行ファイルを直接起動する。xUnit v3 の絞り込みオプションが使える)
./src/PdfConverter.Tests/bin/x64/Debug/net10.0-windows/PdfConverter.Tests.exe
./src/PdfConverter.Tests/bin/x64/Debug/net10.0-windows/PdfConverter.Tests.exe -class "PdfConverter.Tests.Services.BatchExportServiceTests"
```

`-NoPause` を付けないと、ダブルクリック起動と判定された場合に終了時のキー入力待ちで止まる。

配布用出力は `installer/build-installer.ps1` が `dotnet publish` で `src/PdfConverter/bin/<Platform>/publish/` に作り、利用者向けドキュメントの HTML 化 (`build-user-docs.ps1`) とサードパーティ通知の生成 (`generate-third-party-notices.ps1`) も行う。

## ドキュメント同期

次を変えたら、同じ変更で対応する文書も更新する。

| 変更 | 更新先 |
| :--- | :--- |
| 画面の操作・使い方 | `docs/user/usage.md`、必要なら `README.md` |
| CLI オプション・終了コード | `docs/user/cli.md`、必要なら `README.md` |
| 既知の不具合・回避策 | `docs/user/troubleshooting.md` |
| 利用者に見える挙動 | `CHANGELOG.md` の `[Unreleased]` (スキル `/changelog`) |
| mdBook の版 | `installer/licenses/user-docs-notices.txt` (出力に含まれる素材を確認する。不一致は `build-user-docs.ps1` がエラーにする) |
| PDFiumCore の版 | `installer/licenses/PDFium.txt` を同じ版の pdfium-binaries リリースのライセンスに差し替える (不一致は `generate-third-party-notices.ps1` がエラーにする) |

## Git

- ブランチは `develop` を checkout / pull せず、`git fetch origin develop` のうえ `origin/develop` から切る (`git switch -c <prefix>/<kebab-case> origin/develop`)。PR で `develop` へマージする
- ブランチの prefix は `feature` / `fix` / `docs` / `test` / `refactor` / `chore`。新機能の prefix は `feat` ではなく `feature`
- worktree の自動命名ブランチ (`GnudMna/...` など) は、最初のコミットの前に `git branch -m` で規約の名前へ改める
- コミットメッセージの件名は `<種別>: <日本語の要約>`。種別は `Feat` (新機能) / `Update` (既存機能の変更) / `Fix` / `Docs` / `Test` / `Refactor` / `Chore` / `Release`
- 利用者に影響する変更は、`CHANGELOG.md` の追記を同じコミットに含める
- リリース (`Release: vX.Y.Z`) では `Properties/AssemblyInfo.cs` のバージョン 3 属性・`CHANGELOG.md`・`README.md` を更新する。`release/vX.Y.Z` ブランチで `installer/release.ps1` を使うと、これらの更新・品質チェック・コミット・インストーラー作成を一括で行う。インストーラーの版は `build-installer.ps1` が `AssemblyInfo.cs` から取得する

## コーディング規約

規約は `.claude/rules/` に置き、対象ファイルを扱うときに自動で読み込まれる。品質チェックと CHANGELOG 追記はスキル `/check`・`/changelog` (`.claude/skills/`) で行う。共有の権限設定は `.claude/settings.json`、個人用の上書きは `.claude/settings.local.json` (Git 管理外) に置く。

- `cs-section-comments.md` — C# のセクション区切りコメント
- `cs-tests.md` — 単体テストの書き方
- `japanese-text-style.md` — 日本語コメント・UI 文言・Markdown の括弧と半角スペース
- `docs.md` — Markdown 文書 (README・CHANGELOG・`docs/user/`・Claude 向け規約)

## 注意点

- 本体 (`PdfConverter`) は Nullable 参照型が有効。null を取り得る値は `?` で明示し、Try 系メソッドの out 引数には `[NotNullWhen]` を付ける。テストプロジェクトは無効のまま
- コード解析 (`AnalysisLevel=10.0-recommended`) の警告はビルドエラーになる (`src/Directory.Build.props`)。ルールの抑止は理由を添えて `.editorconfig` に書き、コード中の `#pragma warning disable` は意図的な違反に限る
- `Settings.Designer.cs` / `Resources.Designer.cs` は Visual Studio のカスタムツールが生成し、dotnet CLI では再生成されない。設定・リソースを足すときは `Settings.settings` / `Resources.resx` を変え、Designer 側も同じ変更で生成形式どおりに更新する (既存の生成コードの書式・属性に合わせ、手書きのロジックやセクション区切りは足さない)
- AssemblyInfo 属性は `Properties/AssemblyInfo.cs` で手で管理し、SDK 生成との重複は `src/Directory.Build.props` で無効化している
- インデントは 4 スペース、改行は CRLF (`.editorconfig` に従う)
