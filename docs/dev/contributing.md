# 開発の進め方

## ブランチ

- `develop` を checkout / pull せず、`origin/develop` から切る。PR で `develop` へマージする

  ```powershell
  git fetch origin develop
  git switch -c feature/page-rotation origin/develop
  ```

- 名前は `<prefix>/<kebab-case>`。prefix は次のいずれか

  | prefix | 用途 |
  | :--- | :--- |
  | `feature` | 新機能 (`feat` ではない) |
  | `fix` | 不具合修正 |
  | `docs` | ドキュメント |
  | `test` | テスト |
  | `refactor` | リファクタリング |
  | `chore` | ビルド・設定・依存更新など |

- リリースは `release/vX.Y.Z` で行う ([インストーラーとリリース](release.md#リリース) を参照)。`main` へは `release/*` からマージする

## コミット

件名は `<種別>: <日本語の要約>` にする。

| 種別 | 用途 |
| :--- | :--- |
| `Feat` | 新機能 |
| `Update` | 既存機能の変更 |
| `Fix` | 不具合修正 |
| `Docs` | ドキュメント |
| `Test` | テスト |
| `Refactor` | リファクタリング |
| `Chore` | ビルド・設定・依存更新など |
| `Release` | リリース (`Release: vX.Y.Z`) |

利用者に影響する変更は、`CHANGELOG.md` の `[Unreleased]` への追記を同じコミットに含める。書き方は `CHANGELOG.md` 冒頭の規約に従う。

コミット・PR の前に `./scripts/build-and-test.ps1 -NoPause` を通す。CI がないため、このローカルの品質チェックが唯一の検査である。

## コーディング規約

規約は `.claude/rules/` に置いている。Claude Code が対象ファイルを扱うときに自動で読み込むが、人が書くときも同じ規約に従う。

| 規約 | 対象 |
| :--- | :--- |
| [`cs-section-comments.md`](../../.claude/rules/cs-section-comments.md) | C# のセクション区切りコメント (区切り線の幅・見出しラベル) |
| [`cs-tests.md`](../../.claude/rules/cs-tests.md) | 単体テストの配置・命名・ドキュメントコメント |
| [`japanese-text-style.md`](../../.claude/rules/japanese-text-style.md) | 日本語のコメント・UI 文言・Markdown の括弧と半角スペース |
| [`docs.md`](../../.claude/rules/docs.md) | Markdown 文書の文体と文書ごとの規約 |

ほかに次の点に注意する。

- インデントは 4 スペース、改行は CRLF (`.editorconfig`)
- 本体は Nullable 参照型が有効。null を取り得る値は `?` で明示し、Try 系メソッドの `out` 引数には `[NotNullWhen]` を付ける。テストプロジェクトは無効のまま
- クラスとメンバーには XML ドキュメントコメントを書く。設計上の判断 (なぜそうしているか) は `<remarks>` に書く
- `Settings.Designer.cs` / `Resources.Designer.cs` は Visual Studio のカスタムツールの生成物で、dotnet CLI では再生成されない。設定・リソースを足すときは `Settings.settings` / `Resources.resx` を変え、Designer 側も同じ変更で生成形式どおりに更新する。手書きのロジックやセクション区切りは足さない
- AssemblyInfo 属性は `Properties/AssemblyInfo.cs` で手で管理する。SDK の自動生成との重複は `src/Directory.Build.props` で無効化している

## ドキュメント同期

次を変えたら、同じ変更で対応する文書も更新する。

| 変更 | 更新先 |
| :--- | :--- |
| 画面の操作・使い方 | `docs/user/usage.md`、必要なら `README.md` |
| CLI オプション・終了コード | `docs/user/cli.md`、必要なら `README.md` |
| 既知の不具合・回避策 | `docs/user/troubleshooting.md` |
| 利用者に見える挙動 | `CHANGELOG.md` の `[Unreleased]` |
| 構成・設計・ビルド手順・リリース手順 | `docs/dev/` の該当ページ |
| mdBook の版 | `scripts/licenses/user-docs-notices.txt` |
| PDFiumCore の版 | `scripts/licenses/PDFium.txt` |

`docs/user/` はインストーラーに同梱してオフラインで読むため、`docs/user/` の外へ相対リンクしない。`docs/user/changelog.md` は `CHANGELOG.md` を取り込むだけのため編集しない。

## 依存パッケージの更新

| パッケージ | 注意点 |
| :--- | :--- |
| PDFiumCore | ネイティブ DLL は [pdfium-binaries](https://github.com/bblanchon/pdfium-binaries/releases) のビルド。`scripts/licenses/PDFium.txt` を同じ版のリリースに含まれるライセンスに差し替える。不一致は `generate-third-party-notices.ps1` がエラーにする |
| そのほかの NuGet パッケージ | 更新後に `README.md` のオープンソース一覧の版を合わせる。ライセンスが変わっていないか確かめる |
| dotnet ローカルツール | `dotnet-tools.json` (カバレッジの計測・レポート生成)。`dotnet tool update <パッケージ名>` で更新する |
| .NET SDK | 最低版は `global.json` で固定している。コード解析のレベル (`AnalysisLevel`) は SDK の版に固定しているため、SDK を上げるときは `src/Directory.Build.props` の値と、増えたルールの警告を確かめる |

NuGet パッケージを変えたら、`dotnet restore src/PdfConverter.slnx -p:Platform=x64` で `packages.lock.json` を更新し、同じ変更に含める ([依存パッケージのロック](setup.md#依存パッケージのロック) を参照)。

依存を更新したら、x64 と x86 の両方でビルドとテストを通し、インストーラーを作って `THIRD-PARTY-NOTICES.txt` を確かめる。
