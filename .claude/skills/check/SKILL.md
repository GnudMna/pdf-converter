---
name: check
description: ビルドと単体テストの一括実行 (installer/build-and-test.ps1) を行い、失敗した項目を直して通るまで繰り返す。作業の完了前・コミットや PR の前に品質チェックを通すとき、または「check して」「ビルドとテストを通して」と頼まれたときに使う
---

# 品質チェック

`./installer/build-and-test.ps1` で復元・ビルド・単体テストを実行し、失敗を直して全項目を通す。コード解析の警告もビルドエラーになる。

## 実行

- PowerShell ツール (PowerShell 7) で、リポジトリのルートから実行する。数分かかるため `timeout` は 600000 にする
- `-NoPause` を必ず付ける (付けないとキー入力待ちで止まることがある)
- 引数 (`-Configuration Release` / `-Platform x86` など) が渡されたときはそのまま付ける。渡されないときは既定 (Debug / x64) で実行する

```powershell
./installer/build-and-test.ps1 -NoPause
```

## 失敗したとき

スクリプトは最初の失敗で止まる。出力から失敗した項目を特定し、次のとおり直してから、その項目だけを再実行して確認する。通ったらスクリプト全体を再実行する。

| 失敗した項目 | 直し方 | 項目だけの確認 |
| :--- | :--- | :--- |
| パッケージ復元 | 直さずにユーザーへ報告する (ネットワーク・NuGet ソースの問題が多い) | `dotnet restore src/PdfConverter.slnx -p:Platform=x64` |
| ビルドエラー・コード解析の警告 | 原因をコードで直す | `dotnet build src/PdfConverter.slnx -c Debug -p:Platform=x64 --no-restore` |
| 単体テスト | 失敗の原因を直す | テスト実行ファイルに `-class` / `-method` を付けて絞る |

テスト実行ファイルは `src/PdfConverter.Tests/bin/<Platform>/<Configuration>/net10.0-windows/PdfConverter.Tests.exe`。

次はしない。

- `#pragma warning disable`・`.editorconfig` での抑止・`[SuppressMessage]`・`Skip` などで警告やテストを黙らせる。必要だと判断したら、作業を止めてユーザーに確認する
- テストの期待値を、原因を確かめないまま実装に合わせて書き換える
- 同じ失敗を 3 回直しても通らないまま続ける。その時点で状況を報告して判断を仰ぐ

## 報告

最後に次を簡潔に報告する。

- 結果 (成功 / 失敗した項目とエラー)
- 直した内容 (ファイルと理由)
- 実行した構成 (Configuration / Platform)
