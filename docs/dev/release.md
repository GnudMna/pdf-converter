# インストーラーとリリース

## インストーラーの作成

`installer/build-installer.ps1` が、配布用の出力からセットアップ EXE を作る。[Inno Setup 6](https://jrsoftware.org/isinfo.php) と mdBook 0.5.4 が必要である ([開発環境とビルド](setup.md#必要なツール) を参照)。

```powershell
./installer/build-installer.ps1 -NoPause                  # x64 版 (既定)
./installer/build-installer.ps1 -NoPause -Platform all    # x86 と x64 の両方
./installer/build-installer.ps1 -NoPause -SkipBuild       # publish 済みの出力をそのまま使う
```

スクリプトは次を行う。

1. `docs/user/` を mdBook で HTML 化する (`build-user-docs.ps1`。プラットフォームに依らないため 1 回だけ)
2. プラットフォームごとに次を続けて行う
   1. `dotnet publish` (Release・self-contained) で `src/PdfConverter/bin/<Platform>/publish/` を作り直す
   2. `THIRD-PARTY-NOTICES.txt` を生成する (`generate-third-party-notices.ps1`)
   3. HTML 化したドキュメントを publish 出力の `docs\` に複製する
   4. `PdfConverter.iss` を ISCC でコンパイルし、`dist/PdfConverter-<版>-<Platform>-setup.exe` を出力する

バージョンは `src/PdfConverter/Properties/AssemblyInfo.cs` から取得し、ISCC に `/DAppVersion` として渡す。`AppVersion` を渡さずに `PdfConverter.iss` を直接コンパイルするとエラーになる。

`THIRD-PARTY-NOTICES.txt` は直前に復元したプラットフォームの `project.assets.json` から作るため、publish・通知の生成・インストーラーの作成はプラットフォームごとに続けて行う必要がある。

### インストーラーの仕様

| 項目 | 内容 |
| :--- | :--- |
| インストール方式 | 管理者権限を要求しないユーザー単位インストール (`PrivilegesRequired=lowest`) |
| インストール先 | `%LOCALAPPDATA%\Programs\PDF Converter` |
| スタートメニュー | 「PDF Converter」フォルダーにアプリとドキュメントのショートカットを作る |
| デスクトップショートカット | 任意 |
| ファイルの関連付け | 任意。`.pdf` / `.doc` / `.docx` の「プログラムから開く」に登録する (`Software\Classes` の `OpenWithProgids`) |
| ウィザードの言語 | 日本語 / 英語 |
| 使用許諾 | ウィザードで `LICENSE` を表示し、インストール先に `LICENSE.txt` として置く |

`AppId` は 0.6.0 以前と異なる。旧 `AppId` (`LegacyAppId`) で入っている版は、`[Code]` の `PrepareToInstall` でアンインストールしてから入れ直し、インストール先とタスクの選択を引き継ぐ。旧版の利用者が移行し終えたと判断できたら、`LegacyAppId` と移行処理は削除してよい。

## 同梱するドキュメント

`installer/build-user-docs.ps1` が `docs/user/` を HTML 化する。

- 原稿を `artifacts/user-docs-src/` に複製し、`CHANGELOG.md` から利用者向けの `changelog.md` を作ってから `artifacts/user-docs/` へビルドする。`CHANGELOG.md` は最初の `## [` 見出しより前 (書き方の規約) を除き、リンクを書き換える
- mdBook は版によって出力のファイル名と同梱素材が変わるため、`$script:UserDocsMdBookVersion` (0.5.4) に固定している。異なる版ではエラーにする
- ビルド後に、必要なページ (`$script:UserDocsRequiredPages`)・リンク切れ・同梱素材を検証する。`docs/user/` にページを足したら、`SUMMARY.md` とあわせて `$script:UserDocsRequiredPages` にも追加する

mdBook の版を上げるときは、出力に含まれる素材 (テーマ・highlight.js・clipboard.js・Font Awesome など) を確かめ、`installer/licenses/user-docs-notices.txt` の表示を更新する。

## サードパーティ通知

`installer/generate-third-party-notices.ps1` が、`obj/project.assets.json` と NuGet パッケージのメタデータから `THIRD-PARTY-NOTICES.txt` を作る。self-contained で同梱する .NET ランタイムのライセンスと、末尾に `installer/licenses/user-docs-notices.txt` (ドキュメントの素材の表示) を含める。

NuGet パッケージに含まれないライセンス全文は `installer/licenses/` に置く。PDFium のネイティブ DLL のライセンス (`PDFium.txt`) は PDFiumCore の版と一致している必要があり、不一致はエラーにする ([開発の進め方](contributing.md#依存パッケージの更新) を参照)。

## 更新の署名

アプリの更新は、インストーラーごとの署名 (`<インストーラー名>.sig`) を検証できたときだけ適用される。仕組みは [アーキテクチャ](architecture.md#更新-updateservice) を参照する。署名は PowerShell 7 以降 (`pwsh`) の .NET API で作る。

| ファイル | 役割 |
| :--- | :--- |
| `installer/new-update-signing-key.ps1` | キーペア (ECDSA P-256) を作る。秘密鍵を PEM で書き出し、アプリに埋め込む公開鍵を表示する |
| `installer/sign-update.ps1` | `dist/` の `PdfConverter-<版>-*-setup.exe` に署名し、隣へ `.sig` を出力する |
| `installer/update-signing.ps1` | 上の 2 つとリリース用スクリプトが使う共通関数。署名対象の組み立ては `UpdateSignatureVerifier.BuildMessage` と一致させる |

初回の準備は次のとおり。

1. `./installer/new-update-signing-key.ps1 -OutputPath <秘密鍵の保存先>` で鍵を作る
2. 表示された公開鍵を `src/PdfConverter/Infrastructure/UpdateSigningKey.cs` の `PublicKey` に設定してコミットする (公開鍵は秘密ではない)
3. 環境変数 `PDF_CONVERTER_UPDATE_SIGNING_KEY` に秘密鍵のパスを設定する

秘密鍵は Git に含めず、リリース作業をする端末の外へバックアップする。鍵を替えると、古い公開鍵を持つ版は新しい鍵の署名を検証できない。鍵を替える版だけは古い鍵で署名して配り、その次の版から新しい鍵で署名する。

`build-installer.ps1` は、環境変数が設定されていれば、インストーラーを作った後に `sign-update.ps1` を呼ぶ。未設定なら警告を出して署名を省く。署名は埋め込み済みの公開鍵と対応する秘密鍵でなければエラーにする。

## リリース

`installer/release.ps1` が、バージョン更新からインストーラー作成までを一括で行う。事前に `CHANGELOG.md` の `[Unreleased]` に変更項目がそろっていることを確かめ、更新ダウンロード用キー ([開発環境とビルド](setup.md#更新ダウンロード用キー)) と更新の署名用キー ([更新の署名](#更新の署名)) を設定しておく。

```powershell
git fetch origin develop
git switch -c release/v0.7.0 origin/develop

./installer/release.ps1 -Version 0.7.0 -NoPause          # バージョンを明示
./installer/release.ps1 -Bump Minor -NoPause             # 現在のバージョンから上げる
./installer/release.ps1 -Version 0.7.0 -DryRun -NoPause  # 更新内容の確認のみ (ファイル・Git は変更しない)
```

スクリプトは次を順に行う。

1. ブランチ名 (`release/v<版>`) と新バージョンの一致・作業ツリーがクリーンであること・更新ダウンロード用キーと更新の署名用キーの設定を確認する
2. `AssemblyInfo.cs` のバージョン 3 属性、`CHANGELOG.md` (`[Unreleased]` を `## [x.y.z] - 日付` へ移し、比較リンクを更新)、`README.md` のバージョンバッジを更新する
3. `build-and-test.ps1` で品質チェックを行う (Release 構成。対象プラットフォームごと)
4. `Release: vx.y.z` でコミットする
5. `build-installer.ps1` でインストーラーを作り、署名 (`.sig`) を付ける

| オプション | 説明 |
| :--- | :--- |
| `-Version x.y.z` | 新バージョンを明示する (`-Bump` と排他) |
| `-Bump Patch\|Minor\|Major` | 現在のバージョンから上げる |
| `-Date yyyy-MM-dd` | リリース日 (既定: 本日) |
| `-Platform x64\|x86\|all` | 品質チェックとインストーラーの対象 (既定: `all`) |
| `-DryRun` | 更新予定を表示するだけにする |
| `-SkipCheck` / `-SkipInstaller` / `-NoCommit` | 品質チェック / インストーラー作成 / コミットを省く |
| `-AllowDirty` | 未コミットの変更があっても続ける |
| `-NoPause` | 完了後のキー入力待ちを省く |

実行後は次を行う。

1. `release/v<版>` を push し、PR で `develop` と `main` へマージする
2. Gitea で `v<版>` タグを付ける
3. GitHub Release を作り、`dist\` のインストーラーと署名 (`.sig`) をアップロードする (アプリの更新確認は GitHub Release を参照する)
4. 更新サーバー (gh-release-bridge) が `.sig` を配信できるようにする。アセットを許可リストで絞っている場合は、`.sig` を通す設定をこのリポジトリの外で行う。配信されないと、アプリは更新の適用に失敗する

`README.md` のバージョンバッジは `release.ps1` が書き換えるため、バッジの形式 (`Version-x.y.z-blue`) を変えない。
