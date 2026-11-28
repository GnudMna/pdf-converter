# PDF Converter — リリース用バージョン更新の共通関数
#
# scripts/release.ps1 からドットソースして使用します。
#   . (Join-Path $PSScriptRoot 'release-version.ps1')

# ======================================================================
# ファイル入出力
# ======================================================================

# UTF-8 で読み込む (改行コードはそのまま保つ)
function Read-ReleaseTextFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    return [System.IO.File]::ReadAllText($Path, [System.Text.UTF8Encoding]::new($false))
}

# BOM なしの UTF-8 で書き込む
function Write-ReleaseTextFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Value
    )

    [System.IO.File]::WriteAllText($Path, $Value, [System.Text.UTF8Encoding]::new($false))
}

# ======================================================================
# バージョン取得・検証
# ======================================================================

# AssemblyInfo.cs の AssemblyInformationalVersion から現在のバージョンを読み取る
function Get-AssemblyInfoVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot
    )

    $assemblyInfoPath = Join-Path $RepoRoot 'src\PdfConverter\Properties\AssemblyInfo.cs'
    $content = Read-ReleaseTextFile -Path $assemblyInfoPath
    if ($content -match 'AssemblyInformationalVersion\("([^"]+)"\)') {
        return $Matches[1]
    }

    throw "AssemblyInfo.cs からバージョンを取得できませんでした: $assemblyInfoPath"
}

# x.y.z 形式か検証する
function Test-SemVerVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Version
    )

    return $Version -match '^\d+\.\d+\.\d+$'
}

# 指定の種別で SemVer をインクリメントする
function Get-BumpedSemVer {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Version,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Patch', 'Minor', 'Major')]
        [string] $Bump
    )

    if (-not (Test-SemVerVersion -Version $Version)) {
        throw "現在のバージョンが x.y.z 形式ではありません: $Version"
    }

    $parts = $Version.Split('.') | ForEach-Object { [int]$_ }
    switch ($Bump) {
        'Major' { return '{0}.0.0' -f ($parts[0] + 1) }
        'Minor' { return '{0}.{1}.0' -f $parts[0], ($parts[1] + 1) }
        'Patch' { return '{0}.{1}.{2}' -f $parts[0], $parts[1], ($parts[2] + 1) }
    }
}

# 新バージョンが現在より大きいことを検証する
function Assert-NewVersionIsGreater {
    param(
        [Parameter(Mandatory = $true)]
        [string] $CurrentVersion,

        [Parameter(Mandatory = $true)]
        [string] $NewVersion
    )

    if (-not (Test-SemVerVersion -Version $NewVersion)) {
        throw "新バージョンが x.y.z 形式ではありません: $NewVersion"
    }

    if (-not (Test-SemVerVersion -Version $CurrentVersion)) {
        throw "現在のバージョンが x.y.z 形式ではありません: $CurrentVersion"
    }

    if ([version]$NewVersion -le [version]$CurrentVersion) {
        throw "新バージョン ($NewVersion) は現在のバージョン ($CurrentVersion) より大きくしてください"
    }
}

# ======================================================================
# ファイル更新
# ======================================================================

# AssemblyInfo.cs のバージョン 3 属性を更新する
function Set-AssemblyInfoVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot,

        [Parameter(Mandatory = $true)]
        [string] $NewVersion,

        [switch] $DryRun
    )

    $assemblyInfoPath = Join-Path $RepoRoot 'src\PdfConverter\Properties\AssemblyInfo.cs'
    $content = Read-ReleaseTextFile -Path $assemblyInfoPath
    $replacements = @(
        @{ Name = 'AssemblyVersion'; Value = "$NewVersion.0" },
        @{ Name = 'AssemblyFileVersion'; Value = "$NewVersion.0" },
        @{ Name = 'AssemblyInformationalVersion'; Value = $NewVersion }
    )

    $updated = $content
    foreach ($replacement in $replacements) {
        $pattern = '(\[assembly:\s*' + $replacement.Name + '\(")[^"]*("\)\])'
        if ([regex]::Matches($updated, $pattern).Count -ne 1) {
            throw "AssemblyInfo.cs の $($replacement.Name) が見つからないか、複数あります"
        }

        $updated = [regex]::Replace($updated, $pattern, '${1}' + $replacement.Value + '${2}')
    }

    if ($DryRun) {
        Write-Host "[DryRun] AssemblyInfo.cs -> $NewVersion.0 / $NewVersion.0 / $NewVersion"
        return
    }

    Write-ReleaseTextFile -Path $assemblyInfoPath -Value $updated
}

# CHANGELOG.md の [Unreleased] 節をリリース見出しへ移し、末尾の比較リンクを更新する
function Update-ChangelogForRelease {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot,

        [Parameter(Mandatory = $true)]
        [string] $CurrentVersion,

        [Parameter(Mandatory = $true)]
        [string] $NewVersion,

        [Parameter(Mandatory = $true)]
        [string] $ReleaseDate,

        [switch] $DryRun
    )

    $changelogPath = Join-Path $RepoRoot 'CHANGELOG.md'
    $content = Read-ReleaseTextFile -Path $changelogPath
    $newline = if ($content -match "`r`n") { "`r`n" } else { "`n" }

    $sectionPattern = '(?ms)^## \[Unreleased\]\r?\n(?<body>.*?)(?=^## \[)'
    $sectionMatch = [regex]::Match($content, $sectionPattern)
    if (-not $sectionMatch.Success) {
        throw 'CHANGELOG.md に ## [Unreleased] 節が見つかりません'
    }

    if ($sectionMatch.Groups['body'].Value -notmatch '(?m)^-\s') {
        throw 'CHANGELOG.md の [Unreleased] 節に変更項目がありません (- で始まる行が必要)'
    }

    if ($content -match ('(?m)^## \[' + [regex]::Escape($NewVersion) + '\]')) {
        throw "CHANGELOG.md に [$NewVersion] 節が既にあります"
    }

    # 節の本文はそのまま残し、[Unreleased] 見出しの直後に新バージョンの見出しを差し込む
    $header = "## [Unreleased]$newline$newline## [$NewVersion] - $ReleaseDate$newline$newline"
    $body = $sectionMatch.Groups['body'].Value.TrimStart("`r", "`n")
    $updated = $content.Substring(0, $sectionMatch.Index) + $header + $body + $content.Substring($sectionMatch.Index + $sectionMatch.Length)

    $linkPattern = '(?m)^\[Unreleased\]:\s*(?<base>\S+?)/compare/\S+?(?=\r?$)'
    $linkMatch = [regex]::Match($updated, $linkPattern)
    if (-not $linkMatch.Success) {
        throw 'CHANGELOG.md 末尾の [Unreleased] 比較リンクが見つかりません'
    }

    $compareBase = $linkMatch.Groups['base'].Value
    $links = "[Unreleased]: $compareBase/compare/v$NewVersion...develop$newline" +
        "[$NewVersion]: $compareBase/compare/v$CurrentVersion...v$NewVersion"
    $updated = $updated.Substring(0, $linkMatch.Index) + $links + $updated.Substring($linkMatch.Index + $linkMatch.Length)

    if ($DryRun) {
        Write-Host "[DryRun] CHANGELOG.md -> [Unreleased] を [$NewVersion] - $ReleaseDate へ移し、比較リンクを更新"
        return
    }

    Write-ReleaseTextFile -Path $changelogPath -Value $updated
}

# リリースコミットに含めるファイル (リポジトリルートからの相対パス)
function Get-ReleaseVersionFilePaths {
    return @(
        'src/PdfConverter/Properties/AssemblyInfo.cs',
        'CHANGELOG.md'
    )
}

# ======================================================================
# Git
# ======================================================================

# 作業ツリーに未コミットの変更が無いことを検証する
function Assert-GitWorkingTreeClean {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot
    )

    $status = git -C $RepoRoot status --porcelain
    if ($LASTEXITCODE -ne 0) {
        throw 'git status の実行に失敗しました'
    }

    if ($status) {
        throw @"
作業ツリーに未コミットの変更があります。
コミットしてから実行するか、意図的に続行する場合は -AllowDirty を指定してください。

$($status -join [Environment]::NewLine)
"@
    }
}

# 現在のブランチが release/v{version} であることを検証する
function Assert-GitReleaseBranch {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot,

        [Parameter(Mandatory = $true)]
        [string] $NewVersion
    )

    $expectedBranch = "release/v$NewVersion"
    $currentBranch = git -C $RepoRoot branch --show-current
    if ($LASTEXITCODE -ne 0) {
        throw 'git branch --show-current の実行に失敗しました'
    }

    if ($currentBranch -ne $expectedBranch) {
        throw @"
リリースブランチが一致しません。
  現在のブランチ: $currentBranch
  必要なブランチ: $expectedBranch

git fetch origin develop のうえ git switch -c $expectedBranch origin/develop で作成してから実行してください。
"@
    }
}

# バージョン更新を Release: v{version} でコミットする
function New-ReleaseGitCommit {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot,

        [Parameter(Mandatory = $true)]
        [string] $NewVersion,

        [Parameter(Mandatory = $true)]
        [string[]] $FilePaths
    )

    git -C $RepoRoot add -- $FilePaths
    if ($LASTEXITCODE -ne 0) {
        throw 'git add に失敗しました'
    }

    $commitMessage = "Release: v$NewVersion"
    git -C $RepoRoot commit -m $commitMessage
    if ($LASTEXITCODE -ne 0) {
        throw 'git commit に失敗しました'
    }

    Write-Host "Git コミットを作成しました: $commitMessage"
}
