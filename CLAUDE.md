# PDF Converter

PDF / Word 文書のページを画像 (PNG / JPEG / BMP) へ変換する Windows デスクトップアプリ。詳細は [README.md](README.md) を参照。

## 技術スタック

- C# / .NET 10 (`net10.0-windows`, self-contained 配布) / WPF (MVVM)
- PDF レンダリング: PDFium (PDFiumCore。ネイティブ DLL は bblanchon.PDFium.Win32)
- DI: Microsoft.Extensions.DependencyInjection
- テスト: xUnit v3 + Moq (`net10.0-windows`)

## ビルド・テスト

- .NET SDK 10 以降 (dotnet CLI) でビルドする。ソリューションは `src/PdfConverter.slnx`
- プラットフォームは `x64` または `x86` を明示する (`-p:Platform=x64`)。Any CPU は非対応 (`pdfium.dll` がプラットフォーム固有のため)
- 復元・ビルド・テストの一括実行: `installer/build-and-test.ps1` (例: `-Configuration Release`, `-NoPause`)
- 手動でテストを実行する場合は `src/PdfConverter.Tests/bin/x64/Debug/net10.0-windows/PdfConverter.Tests.exe` を直接起動する
- 配布用出力は `dotnet publish` で `src/PdfConverter/bin/<Platform>/publish/` に作る (`installer/build-installer.ps1` が実行)
- PDFiumCore を更新するときは `installer/licenses/PDFium.txt` を同じバージョンの pdfium-binaries リリースのライセンスに差し替える (不一致は `generate-third-party-notices.ps1` がエラーにする)
- AssemblyInfo 属性は `Properties/AssemblyInfo.cs` で管理し、SDK 生成との重複は `src/Directory.Build.props` で無効化している

## ディレクトリ構成

- `src/PdfConverter/` アプリ本体
  - `Commands/` `Converters/` `Models/` `Themes/` `Views/` `ViewModels/` (`Coordinators/` でプレビュー・保存フローを調停)
  - `Services/` 変換・I/O・Word/LibreOffice 連携・バッチ変換
  - `Infrastructure/` ログ・例外処理・DI 構成・設定初期化・CLI 解析
- `src/PdfConverter.Tests/` 単体テスト (本体と同じフォルダ構成)
- `installer/` Inno Setup スクリプトとビルド用 PowerShell

## コーディング規約

- 詳細規則は `.claude/rules/` を参照 (対象ファイル編集時に自動で読み込まれる)
  - `cs-section-comments.md`: C# のセクション区切りコメント
  - `japanese-text-style.md`: 日本語コメント・UI 文言・README の括弧と半角スペース
- 本体 (`PdfConverter`) は Nullable 参照型が有効。null を取り得る値は `?` で明示し、Try 系メソッドの out 引数には `[NotNullWhen]` を付ける。テストプロジェクトは無効のまま
- 自動生成ファイル (`Settings.Designer.cs`, `Resources.Designer.cs`, `AssemblyInfo.cs`) は手で編集しない
- インデントは 4 スペース (`.editorconfig` に従う)
- コード解析 (`AnalysisLevel=10.0-recommended`) の警告はビルドエラーになる (`src/Directory.Build.props`)。ルールの抑止は理由を添えて `.editorconfig` に書き、コード中の `#pragma warning disable` は意図的な違反に限る

## Git 運用

- `develop` から `feature/<name>` / `fix/<name>` / `test/<name>` / `refactor/<name>` / `chore/<name>` を切り、PR で `develop` へマージする
- コミットメッセージは `Update: ...` / `Fix: ...` 形式で、本文は日本語
- 機能変更時は `CHANGELOG.md` の更新を検討する
