# テスト

単体テストは `src/PdfConverter.Tests/` にあり、xUnit v3 と Moq で書いている。テストプロジェクトのターゲットは `net10.0-windows` で、ビルドするとテスト実行ファイル (xUnit v3 の In-Process Runner) ができる。

## 実行

```powershell
# 復元・ビルド・テストの一括実行 (推奨。作業の完了前に通す品質チェック)
./scripts/build-and-test.ps1 -NoPause
./scripts/build-and-test.ps1 -NoPause -Configuration Release -Platform x86
./scripts/build-and-test.ps1 -NoPause -SkipRestore   # 復元済みなら省略できる
```

テストだけを実行する場合は、ビルド後に実行ファイルを直接起動する。xUnit v3 の絞り込みオプションが使える。

```powershell
dotnet build src/PdfConverter.slnx -c Debug -p:Platform=x64

./src/PdfConverter.Tests/bin/x64/Debug/PdfConverter.Tests.exe
./src/PdfConverter.Tests/bin/x64/Debug/PdfConverter.Tests.exe -class "PdfConverter.Tests.Services.BatchExportServiceTests"
./src/PdfConverter.Tests/bin/x64/Debug/PdfConverter.Tests.exe -method "PdfConverter.Tests.Services.BatchExportServiceTests.RunAsync_ShowHelp_ReturnsSuccess"
```

Visual Studio のテストエクスプローラーからも実行できる (`xunit.runner.visualstudio` を使用)。

x86 で実行すると、x86 だけの制限 (描画画素数の上限など) を通る。プラットフォームに依存する変更をしたら、x64 と x86 の両方で通す。

`WpfClipboardServiceTests` は実際のクリップボードを使うため、ほかのアプリやほかのテスト実行がクリップボードを使っていると失敗することがある。再実行で通るならこの競合である。

## カバレッジ

`-Coverage` を付けると、単体テストを [dotnet-coverage](https://learn.microsoft.com/dotnet/core/additional-tools/dotnet-coverage) の下で実行し、[ReportGenerator](https://github.com/danielpalme/ReportGenerator) で HTML レポートを作る。

```powershell
./scripts/build-and-test.ps1 -NoPause -Coverage
```

| 出力 | 内容 |
| :--- | :--- |
| `artifacts/coverage/report/index.html` | HTML レポート |
| `artifacts/coverage/coverage.cobertura.xml` | Cobertura 形式の計測結果 (エディターの拡張機能などで読む) |
| `artifacts/coverage/test-results.xml` | テスト結果 (xUnit v2 XML 形式) |

- 2 つのツールの版はリポジトリ直下の `dotnet-tools.json` (dotnet ローカルツール) で固定し、スクリプトが `dotnet tool restore` で復元する
- 計測から外す対象 (テストコード・Designer の生成コード) は `scripts/coverage.settings.xml` で指定する
- テスト実行ファイルの既定のランナー (xUnit の In-Process Runner) は Microsoft.Testing.Platform の `--coverage` を受け付けないため、外から計測する `dotnet-coverage` を使う。`dotnet-coverage` は計測対象の終了コードを引き継がない場合があるため、テストの成否はテスト結果の XML で判定する

## 書き方

規約は [`.claude/rules/cs-tests.md`](../../.claude/rules/cs-tests.md) にまとめている。要点は次のとおり。

- テストクラスは本体と同じフォルダー・名前空間に `<対象クラス名>Tests` の名前で置く
- メソッド名は `<対象メンバー>_<条件>_<期待結果>` の英語 PascalCase にする
- Microsoft Word・LibreOffice がない環境で通るようにする。外部プロセスは `FakeSofficeHelper` やモックで置き換える
- `Skip` でテストを黙らせない。期待値を、原因を確かめないまま実装に合わせて書き換えない

## テスト補助 (`Helpers/`)

複数のテストクラスで使う処理は `Helpers/` に置く。既存の補助があれば再実装しない。

| 補助 | 用途 |
| :--- | :--- |
| `PdfTestAssetFactory` | テスト用 PDF などの一時ファイルの生成 |
| `StaTestHelper` | WPF のオブジェクトを扱う処理を STA スレッドで実行 |
| `GlobalStateTestCollection` | 静的な状態 (ログ出力先・一時ディレクトリのルートなど) を書き換えるテストを並列実行から外すコレクション |
| `MainViewModelTestFactory` | `MainViewModel` とモックサービスの生成 |
| `TestMainViewModelHost` | Coordinator のテスト用ホスト (`IMainViewModelHost` の実装) |
| `FakeSofficeHelper` | `soffice.exe` の振る舞いを模倣するラッパーの生成 |
| `BitmapTestHelper` | テスト用 WPF ビットマップの生成 |
| `SynchronousProgress<T>` | 進捗を呼び出し元のスレッドで即座に通知する `IProgress<T>` |
| `DocumentPdfSourceTestHelper` / `ImageExportSettingsTestHelper` / `WordToPdfConversionSettingsTestHelper` | 各サービス・設定インターフェースのモック生成 |
| `TrackingWordToPdfConversionSettings` | 保存回数を追跡できる Word 設定のテスト用実装 |
| `UpdateSigningTestKey` | 更新の配布物の署名検証テスト用の鍵と署名の生成 |
| `PortablePackageTestHelper` | ポータブル版のアプリフォルダー・zip・マニフェストの生成 |

## テストしやすくするための設計

- UI・外部環境に触れる処理はインターフェース (`IDialogService`・`IClipboardService`・`IWordToPdfConversionService` など) の後ろに置き、テストではモックに差し替える
- 静的クラスで差し替えが必要な処理は、`internal` のデリゲートプロパティ (`UserSettingsInitializer` の `IsUpgradeRequired` など) にしてテストから置き換える。本体の `InternalsVisibleTo("PdfConverter.Tests")` で `internal` メンバーを参照できる
