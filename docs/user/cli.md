# コマンドライン

## ファイルを指定して起動する

起動時に PDF / Word のパスを渡すと、起動後に自動で読み込む。
複数の引数を渡した場合は、先頭から順に調べ、最初に開けるパスを使う。

```powershell
# 絶対パスで指定
& ".\PDF Converter.exe" "C:\docs\report.pdf"

# 相対パスでも指定できる (起動時の作業フォルダーが基準)
& ".\PDF Converter.exe" ".\sample.docx"
```

エクスプローラーで PDF / Word をアプリのアイコンへドラッグしたり、「プログラムから開く」に登録して使うこともできる。
インストール時に **ファイルの関連付け** を選ぶと、`.pdf` / `.doc` / `.docx` の「プログラムから開く」に表示される。

## バッチ変換 (ウィンドウを表示しない)

`--output` などのオプションを 1 つでも指定すると、ウィンドウを表示せずに変換だけを行って終了する。
スクリプトやタスク スケジューラからの自動化に使える。
進捗とエラーは標準出力・標準エラーに表示し、詳細は画面で使う場合と同じログファイルにも記録する ([トラブルシューティング](troubleshooting.md#ログ) を参照)。

```powershell
# PDF の全ページを PNG 画像として出力
& ".\PDF Converter.exe" --output "C:\out" "C:\docs\report.pdf"

# ページ範囲・形式・解像度を指定 (-o=value の形式も可)
& ".\PDF Converter.exe" -o "C:\out" -f png -p "1-3,5" --resolution-mode width -r 1920 "report.pdf"

# 複数ファイルを一括変換 (ファイルごとに <出力先>\<ファイル名>\page_1.png の形式)
& ".\PDF Converter.exe" -o "C:\out" "a.pdf" "b.pdf"

# Word を PDF として保存 (出力先に .pdf を指定すると、そのパスへ直接保存)
& ".\PDF Converter.exe" --export-pdf -o "C:\out\proposal.pdf" "proposal.docx"

# パスワード付き PDF を変換 (指定したパスワードをすべての入力ファイルに使う)
& ".\PDF Converter.exe" -o "C:\out" --password "secret" "locked.pdf"

# 既存ファイルを上書き
& ".\PDF Converter.exe" -o "C:\out" --overwrite "report.pdf"

# 先頭が - のパスを渡す場合は -- で区切る
& ".\PDF Converter.exe" -o "C:\out" -- "-report.pdf"

# ヘルプを表示
& ".\PDF Converter.exe" --help
```

### 出力先

| 入力 | 画像の出力先 |
| :--- | :--- |
| 1 ファイル | `<出力先>\page_1.png`、`<出力先>\page_2.png`、… |
| 複数ファイル | `<出力先>\<ファイル名>\page_1.png`、… |

別フォルダーの同名ファイル (`a\x.docx` と `b\x.docx` など) のように出力先が重なる場合は、2 件目以降の名前に `_2`、`_3` を付けて別の出力先にする (`<出力先>\x\`、`<出力先>\x_2\`、…)。
`--export-pdf` でも同様に、`x.pdf`、`x_2.pdf`、… の名前で保存する。変更した名前は進捗メッセージに表示する。

ページ番号は、総ページ数の桁数までゼロ埋めする (例: 12 ページの PDF なら `page_01.png` ～ `page_12.png`)。

### オプション

| オプション | 説明 |
| :--- | :--- |
| `--output`, `-o <path>` | 画像の出力先フォルダー、または PDF の出力先 (必須) |
| `--pages`, `-p <範囲>` | 出力するページ (例: `1-3,5`)。省略時は全ページ |
| `--format`, `-f <形式>` | `png` / `jpeg` (`jpg` も可) / `bmp`。省略時は画面で保存した設定 |
| `--resolution-mode <方式>` | `default` / `width` / `height` / `dpi`。省略時は画面で保存した設定 |
| `--resolution`, `-r <数値>` | 解像度の数値。省略時は画面で保存した設定 |
| `--transparent` | 透明度を保持する (PNG のみ) |
| `--no-transparent` | 白背景で合成する |
| `--password <文字列>` | パスワード付き PDF を開くパスワード。すべての入力ファイルに使う。値は前後の空白や `"` も含めてそのまま使い、`-` や `/` で始まる値も指定できる。次の引数は常にパスワードとして扱う |
| `--overwrite` | 既存ファイルを確認せずに上書きする |
| `--export-pdf` | Word を PDF として保存する (画像には変換しない) |
| `--batch`, `-b` | バッチ変換であることを明示する。ほかのオプションと同様にウィンドウを表示しない動作になるだけで、指定しなくても結果は変わらない |
| `--quiet`, `-q` | 進捗メッセージを表示しない |
| `--help`, `-h`, `/?`, `--?` | ヘルプを表示する |

オプションは `-o=value` / `--output=value` の形式でも指定できる。
値を取らないオプションも `--overwrite=false` のように `true` / `false` を指定できる (指定なしは `true`)。
`--export-pdf` と `--pages` は同時に指定できない。
`--password` に指定したパスワードは、実行中のプロセスのコマンドラインとして他のプログラムから見える。共有環境では、スクリプトにパスワードを直接書かないよう注意する。
`/` で始まる引数のうち、`//server/share/a.pdf` のように `/` や `\` を含むものはオプションではなくファイルパスとして扱う。

### 終了コード

| コード | 意味 |
| :--- | :--- |
| `0` | 成功 |
| `1` | 変換エラー |
| `2` | 引数エラー |

変換中に Ctrl+C を押すと、実行中の Word / LibreOffice を終了し、一時ファイルを削除してから終了コード `1` で終了する。
Word が応答しない場合は、終了までに 15 秒ほどかかることがある。

本体は Windows アプリ (GUI サブシステム) のため、cmd や PowerShell は変換の終了を待たずに次のコマンドへ進む。
そのまま実行すると `%ERRORLEVEL%` / `$LASTEXITCODE` に終了コードは入らないので、終了を待つ起動方法で実行する。

```powershell
# PowerShell: Start-Process -Wait -PassThru で待ち、ExitCode を確認する
$p = Start-Process -FilePath ".\PDF Converter.exe" -ArgumentList '-o "C:\out" "report.pdf"' -Wait -PassThru
$p.ExitCode
```

```bat
rem cmd: start /wait で待つと、%ERRORLEVEL% に終了コードが入る
start /wait "" "PDF Converter.exe" -o "C:\out" "report.pdf"
echo %ERRORLEVEL%
```