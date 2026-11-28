# PDF Converter — 利用者向けドキュメントのビルド関数
#
# docs/user を mdBook で HTML 化し、インストーラーへ同梱する出力 (artifacts\user-docs) を作ります。
# scripts/ 配下のスクリプトからドットソースして使用します。
#   . (Join-Path $PSScriptRoot 'build-user-docs.ps1')
#
# 必要環境:
#   - mdBook ($script:UserDocsMdBookVersion の版に固定)

# mdBook 0.5 以降は静的ファイル名にハッシュが付き、版で出力が変わるため固定する
# 版を変えるときは、出力に含まれる素材と licenses\user-docs-notices.txt の表示を確認して更新する
$script:UserDocsMdBookVersion = '0.5.4'

# 出力に必ず含まれるページ。docs/user/SUMMARY.md の章に対応する
$script:UserDocsRequiredPages = @(
    'index.html',
    'introduction.html',
    'usage.html',
    'cli.html',
    'troubleshooting.html',
    'changelog.html'
)

# BOM なしの UTF-8 で書き込む
function Write-Utf8NoBomFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string] $Value
    )

    [System.IO.File]::WriteAllText($Path, $Value, [System.Text.UTF8Encoding]::new($false))
}

# mdBook が所定の版でインストールされているか確認し、実行ファイルのパスを返す
function Find-MdBook {
    $installHint = @(
        '次のいずれかでインストールしてください:',
        "  - https://github.com/rust-lang/mdBook/releases/tag/v$($script:UserDocsMdBookVersion) の",
        "    mdbook-v$($script:UserDocsMdBookVersion)-x86_64-pc-windows-msvc.zip を展開し、mdbook.exe を PATH の通ったフォルダーに置く",
        "  - cargo install mdbook --locked --version $($script:UserDocsMdBookVersion)"
    ) -join [Environment]::NewLine

    $command = Get-Command mdbook -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "mdBook (mdbook) が見つかりません。`n$installHint"
    }

    $versionText = (& $command.Source --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "mdbook --version が終了コード $LASTEXITCODE で失敗しました"
    }
    if ($versionText -notmatch '(\d+\.\d+\.\d+)' -or $Matches[1] -ne $script:UserDocsMdBookVersion) {
        throw "mdBook の版が異なります (実際: $versionText, 期待: $($script:UserDocsMdBookVersion))。`n$installHint"
    }

    return $command.Source
}

# CHANGELOG.md を利用者向けの変更履歴ページに整える
# - 先頭の記載規約 (Keep a Changelog・節の分類表など。開発者向け) を除き、最初のバージョン見出しから始める
# - バージョン見出しの参照リンクを外す (見出し内のリンクで左側の目次が崩れるため)
# - リンクを本の中から辿れる形に書き換える
#   (docs/user/*.md → 同じ本のページ、本に含めない相対リンク → リンクを外して文字だけ残す)
# - 箇条書きの項目は、「。」の直後に文が続く箇所で行を折り返す
function ConvertTo-UserDocsChangelog {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Markdown
    )

    $title = [regex]::Match($Markdown, '(?m)^# [^\r\n]*').Value
    $firstRelease = [regex]::Match($Markdown, '(?m)^## \[')
    if (-not $title -or -not $firstRelease.Success) {
        throw 'CHANGELOG.md から見出しとバージョン節を読み取れませんでした'
    }
    $body = $title + [Environment]::NewLine + [Environment]::NewLine + $Markdown.Substring($firstRelease.Index)

    # バージョン見出しの参照リンク (`## [0.5.0]` → 比較 URL) は、見出しの中にリンクが入れ子になり、
    # 左側の目次にバージョンが出なくなる。ブラケットと、末尾の参照リンク定義を除く
    $body = [regex]::Replace($body, '(?m)^## \[([^\]]+)\]', '## $1')
    $body = [regex]::Replace($body, '(?m)^\[[^\]]+\]: \S+[ \t]*(\r?\n|$)', '')

    $pattern = '\[(?<text>[^\]]+)\]\((?<target>[^)\s]+)\)'
    $linked = [regex]::Replace($body, $pattern, {
            param($match)
            $text = $match.Groups['text'].Value
            $target = $match.Groups['target'].Value

            if ($target -match '^[a-z][a-z0-9+.-]*:' -or $target.StartsWith('#')) {
                return $match.Value
            }
            if ($target -match '^docs/user/(?<page>[^/]+\.md(#.*)?)$') {
                return "[$text]($($Matches['page']))"
            }
            if ($target -match '^CHANGELOG\.md(?<anchor>#.*)?$') {
                return "[$text](changelog.md$($Matches['anchor']))"
            }
            return $text
        })

    # 箇条書きの行だけ、「。」の直後に文が続く箇所 (閉じ括弧・引用符・行末を除く) で折り返す
    # (行末の `\` は Markdown の強制改行)
    $lines = $linked -split '\r?\n'
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^(?<indent>\s*)[-*] ') {
            $continuation = $Matches['indent'] + '  '
            $lines[$i] = [regex]::Replace($lines[$i], '。(?=[^\s)）」』])', "。\`n$continuation")
        }
    }
    return $lines -join [Environment]::NewLine
}

# 出力 HTML の相対リンク (href / src) のリンク先ファイルとアンカーが存在するか検証する
function Assert-UserDocsLinks {
    param(
        [Parameter(Mandatory = $true)]
        [string] $OutputDir
    )

    $idCache = @{}
    $getIds = {
        param([string] $Path)
        if (-not $idCache.ContainsKey($Path)) {
            $html = Get-Content -LiteralPath $Path -Raw -Encoding utf8
            $ids = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
            foreach ($m in [regex]::Matches($html, '\sid="([^"]+)"')) {
                $null = $ids.Add([System.Net.WebUtility]::HtmlDecode($m.Groups[1].Value))
            }
            $idCache[$Path] = $ids
        }
        return , $idCache[$Path]
    }

    $errors = New-Object 'System.Collections.Generic.List[string]'
    foreach ($page in Get-ChildItem -LiteralPath $OutputDir -Recurse -File -Filter '*.html') {
        $html = Get-Content -LiteralPath $page.FullName -Raw -Encoding utf8
        $relativePage = [System.IO.Path]::GetRelativePath($OutputDir, $page.FullName)

        foreach ($m in [regex]::Matches($html, '\s(?:href|src)="([^"]+)"')) {
            $link = [System.Net.WebUtility]::HtmlDecode($m.Groups[1].Value)
            if ($link -match '^[a-z][a-z0-9+.-]*:' -or $link.StartsWith('//')) {
                continue
            }

            $parts = $link.Split([char]'#', 2)
            $pathPart = [Uri]::UnescapeDataString($parts[0].Split([char]'?', 2)[0])
            $fragment = if ($parts.Count -gt 1) { [Uri]::UnescapeDataString($parts[1]) } else { '' }

            $target = if ($pathPart) {
                [System.IO.Path]::GetFullPath((Join-Path $page.DirectoryName $pathPart))
            }
            else {
                $page.FullName
            }

            if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
                $errors.Add("$relativePage -> $link (ファイルが無い)")
                continue
            }
            if ($fragment -and $target.EndsWith('.html') -and -not (& $getIds $target).Contains($fragment)) {
                $errors.Add("$relativePage -> $link (アンカーが無い)")
            }
        }
    }

    if ($errors.Count -gt 0) {
        $lines = @("利用者向けドキュメントにリンク切れが $($errors.Count) 件あります") +
            @($errors | ForEach-Object { "  $_" })
        throw ($lines -join [Environment]::NewLine)
    }
}

# 出力に同梱される第三者素材が、THIRD-PARTY-NOTICES.txt に追記する表示 (licenses\user-docs-notices.txt) と合っているか検証する
# - フォントは同梱しない (docs/user/theme/fonts/fonts.css で mdBook 既定のフォントを外している)
# - mdBook と、出力に含まれる highlight.js / clipboard.js / Font Awesome の版が表示に書かれている
function Assert-UserDocsAssets {
    param(
        [Parameter(Mandatory = $true)]
        [string] $OutputDir
    )

    $fonts = @(Get-ChildItem -LiteralPath $OutputDir -Recurse -File |
            Where-Object { $_.Extension -in @('.woff', '.woff2', '.ttf', '.otf') })
    if ($fonts.Count -gt 0) {
        throw "利用者向けドキュメントにフォントが含まれています ($($fonts[0].Name) など)。docs/user/theme/fonts/fonts.css を確認してください"
    }

    $noticesPath = Join-Path $PSScriptRoot 'licenses\user-docs-notices.txt'
    $notices = Get-Content -LiteralPath $noticesPath -Raw -Encoding utf8

    $expected = New-Object 'System.Collections.Generic.List[string]'
    $expected.Add("mdBook $($script:UserDocsMdBookVersion)")
    $patterns = @(
        @{ Pattern = 'Highlight\.js (\d+\.\d+\.\d+)'; Label = 'highlight.js' },
        @{ Pattern = 'clipboard\.js v(\d+\.\d+\.\d+)'; Label = 'clipboard.js' },
        @{ Pattern = 'Font Awesome Free (\d+\.\d+\.\d+)'; Label = 'Font Awesome Free' }
    )
    $contents = Get-ChildItem -LiteralPath $OutputDir -Recurse -File |
        Where-Object { $_.Extension -in @('.html', '.js', '.css', '.svg') } |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding utf8 }
    foreach ($entry in $patterns) {
        $versions = $contents | ForEach-Object { [regex]::Matches($_, $entry.Pattern) } |
            ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
        foreach ($version in $versions) {
            $expected.Add("$($entry.Label) $version")
        }
    }

    $missing = @($expected | Where-Object { -not $notices.Contains($_) })
    if ($missing.Count -gt 0) {
        $lines = @('利用者向けドキュメントの第三者素材の表示が出力と合っていません') +
            @($missing | ForEach-Object { "  $noticesPath に記載がない: $_" }) +
            @('mdBook の版を変えた場合は、同梱素材とライセンスを確認して表示を更新してください')
        throw ($lines -join [Environment]::NewLine)
    }
}

# 出力ディレクトリに必須ページが揃っているか検証する
function Assert-UserDocsPages {
    param(
        [Parameter(Mandatory = $true)]
        [string] $DocsDir
    )

    foreach ($page in $script:UserDocsRequiredPages) {
        if (-not (Test-Path -LiteralPath (Join-Path $DocsDir $page) -PathType Leaf)) {
            throw "利用者向けドキュメントに $page がありません ($DocsDir)"
        }
    }
}

# docs/user を mdBook でビルドし、出力ディレクトリ (artifacts\user-docs) を返す
# CHANGELOG.md のリンクを書き換えるため、原稿を artifacts\user-docs-src へ複製してからビルドする
function Invoke-UserDocsBuild {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot
    )

    $mdbook = Find-MdBook

    $sourceDir = Join-Path $RepoRoot 'docs\user'
    $stagingDir = Join-Path $RepoRoot 'artifacts\user-docs-src'
    $outputDir = Join-Path $RepoRoot 'artifacts\user-docs'

    Write-Host '利用者向けドキュメントを生成しています (mdBook)...'

    foreach ($dir in @($stagingDir, $outputDir)) {
        if (Test-Path -LiteralPath $dir) {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }
    $null = New-Item -ItemType Directory -Path $stagingDir -Force

    Copy-Item -Path (Join-Path $sourceDir '*') -Destination $stagingDir -Recurse -Force

    $changelog = Get-Content -LiteralPath (Join-Path $RepoRoot 'CHANGELOG.md') -Raw -Encoding utf8
    $changelogPage = ConvertTo-UserDocsChangelog -Markdown $changelog
    Write-Utf8NoBomFile -Path (Join-Path $stagingDir 'changelog.md') -Value $changelogPage

    & $mdbook build $stagingDir --dest-dir $outputDir | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "mdbook build が終了コード $LASTEXITCODE で失敗しました"
    }

    # src = "." のため、book.toml・theme/ (テーマの上書き)・custom.css (ハッシュ付きの複製を参照する) が
    # 原稿のまま出力へ複製される。404.html (<base href="/">) と .nojekyll は Web 公開用で、
    # ローカルのファイルとして開く配布物では使わない
    foreach ($name in @('book.toml', 'theme', 'custom.css', '404.html', '.nojekyll')) {
        $unused = Join-Path $outputDir $name
        if (Test-Path -LiteralPath $unused) {
            Remove-Item -LiteralPath $unused -Recurse -Force
        }
    }

    Assert-UserDocsPages -DocsDir $outputDir
    Assert-UserDocsLinks -OutputDir $outputDir
    Assert-UserDocsAssets -OutputDir $outputDir

    $fileCount = @(Get-ChildItem -LiteralPath $outputDir -Recurse -File).Count
    Write-Host "  出力: $outputDir ($fileCount ファイル)"

    return $outputDir
}
