# 開発者向けドキュメント

PDF Converter の開発・ビルド・リリースに関する文書。機能と使い方は利用者向けドキュメント [`docs/user/`](../user/introduction.md) を参照する。

| ページ | 内容 |
| :--- | :--- |
| [開発環境とビルド](setup.md) | 必要なツール・ビルド・実行・更新ダウンロード用キー |
| [アーキテクチャ](architecture.md) | 起動の流れ・DI・MVVM と Coordinator・各サービスの責務・ファイルの置き場所 |
| [テスト](testing.md) | 単体テストの実行と書き方 |
| [インストーラーとリリース](release.md) | インストーラー作成・同梱ドキュメント・サードパーティ通知・リリース手順 |
| [開発の進め方](contributing.md) | ブランチ・コミット・コーディング規約・ドキュメント同期・依存パッケージの更新 |

## リポジトリ構成

```text
pdf-converter/
├─ src/
│  ├─ PdfConverter.slnx            # ソリューション
│  ├─ Directory.Build.props        # 全プロジェクト共通の MSBuild 設定 (プラットフォーム・コード解析)
│  ├─ PdfConverter/                # アプリ本体 (WPF / MVVM)
│  │  ├─ Assets/                   # アイコン
│  │  ├─ Commands/                 # ICommand 実装 (RelayCommand / AsyncRelayCommand)
│  │  ├─ Converters/               # XAML の値コンバーター
│  │  ├─ Infrastructure/           # ログ・例外処理・DI 構成・設定初期化・起動補助・CLI 解析
│  │  ├─ Models/                   # 列挙型・不変データ・ComboBox 表示用モデル
│  │  ├─ Properties/               # AssemblyInfo・リソース・ユーザー設定定義
│  │  ├─ Services/                 # PDF 描画・Word 変換・バッチ変換・更新確認・I/O
│  │  ├─ Themes/                   # ライト / ダークテーマと共通スタイル
│  │  ├─ ViewModels/               # ViewModel (Coordinators/ でプレビュー・保存フローを調停)
│  │  ├─ Views/                    # 画面 (Panels/ にメイン画面の各パネル、Behaviors/ に添付ビヘイビア)
│  │  └─ UpdateReleaseKey.local.props.example  # 更新ダウンロード用キーの設定例
│  └─ PdfConverter.Tests/          # xUnit v3 単体テスト (本体と同じフォルダー構成。Helpers/ にテスト補助)
├─ installer/                      # Inno Setup スクリプトとビルド用 PowerShell
│  └─ licenses/                    # NuGet パッケージに含まれないライセンス全文・ドキュメント素材の表示
├─ docs/
│  ├─ user/                        # 利用者向けドキュメント (mdBook の原稿。インストーラーに同梱)
│  └─ dev/                         # 開発者向けドキュメント (この文書)
├─ artifacts/                      # 利用者向けドキュメントのビルド出力 (Git 管理外)
├─ dist/                           # インストーラーの出力先 (Git 管理外)
├─ .claude/                        # Claude Code 向けの規約 (rules/)・スキル (skills/)・権限設定
├─ .editorconfig                   # コードスタイル・コード解析ルール
├─ CHANGELOG.md                    # 変更履歴
├─ CLAUDE.md                       # Claude Code 向けの開発ガイド
├─ LICENSE
└─ README.md
```
