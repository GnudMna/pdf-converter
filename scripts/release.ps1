# PDF Converter — リリース手順の一括実行スクリプト
#
# バージョン更新 (AssemblyInfo.cs / CHANGELOG.md)・品質チェック・Git コミット・インストーラーとポータブル版の作成を順に行います。
# Git タグの付与・push・Release へのアップロードは行いません (完了時に手順を表示します)。
#
# 必要環境:
#   - Git
#   - build-and-test.ps1 / build-installer.ps1 / build-portable.ps1 と同じ環境 (.NET SDK 11 以降・Inno Setup 6・mdBook)
#   - 更新の署名用の秘密鍵 (環境変数 PDF_CONVERTER_UPDATE_SIGNING_KEY に PEM のパス。new-update-signing-key.ps1 で作成)
#   - PowerShell 7 以降 (pwsh)。インストーラー・ポータブル版の作成まで行うときは署名のために必要
#
# 前提:
#   - release/v{version} ブランチ上で実行する (ブランチ名と新バージョンが一致しないとエラー)
#   - CHANGELOG.md の [Unreleased] 節に変更項目を書いておく (空だとエラー)
#
# 使用例:
#   git fetch origin develop
#   git switch -c release/v0.6.0 origin/develop
#   .\release.ps1 -Version 0.6.0
#   .\release.ps1 -Bump Patch -DryRun          # 更新内容の確認のみ
#   .\release.ps1 -Bump Minor -Platform x64    # x64 のインストーラーだけ作成
#
# オプション:
#   -Version x.y.z           新バージョンを明示指定 (-Bump と排他)
#   -Bump Patch|Minor|Major  現在のバージョンから SemVer でインクリメント
#   -Date yyyy-MM-dd         リリース日 (既定: 本日)
#   -Platform x64|x86|all    品質チェック・インストーラー・ポータブル版の対象 (既定: all)
#   -DryRun                  更新予定を表示するだけでファイル・Git は変更しない
#   -SkipCheck               品質チェック (build-and-test.ps1) を省略
#   -SkipInstaller           インストーラー作成 (build-installer.ps1) を省略
#   -SkipPortable            ポータブル版の作成 (build-portable.ps1) を省略
#   -NoCommit                Git コミットを作成しない
#   -AllowDirty              未コミットの変更があっても続行
#   -NoPause                 完了後のキー入力待ちを省略 (ターミナル向け)

[CmdletBinding()]
param(
    [string] $Version,

    [ValidateSet('Patch', 'Minor', 'Major')]
    [string] $Bump,

    [string] $Date = (Get-Date -Format 'yyyy-MM-dd'),

    [ValidateSet('x64', 'x86', 'all')]
    [string] $Platform = 'all',

    [switch] $DryRun,

    [switch] $SkipCheck,

    [switch] $SkipInstaller,

    [switch] $SkipPortable,

    [switch] $NoCommit,

    [switch] $AllowDirty,

    [switch] $NoPause
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'build-common.ps1')
. (Join-Path $PSScriptRoot 'release-version.ps1')
. (Join-Path $PSScriptRoot 'update-signing.ps1')

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

# 子スクリプトを実行し、終了コードで成否を判定する
function Invoke-ChildScript {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name,

        [hashtable] $Arguments = @{}
    )

    $scriptPath = Join-Path $PSScriptRoot $Name
    $global:LASTEXITCODE = 0
    & $scriptPath @Arguments -NoPause
    if ($LASTEXITCODE -ne 0) {
        throw "$Name が失敗しました (終了コード: $LASTEXITCODE)"
    }
}

$shouldPauseOnExit = -not $NoPause -and (Test-DoubleClickLaunch)
$exitCode = 0

try {
    if ($Date -notmatch '^\d{4}-\d{2}-\d{2}$') {
        throw "-Date は yyyy-MM-dd 形式で指定してください: $Date"
    }

    if ($Version -and $Bump) {
        throw '-Version と -Bump は同時に指定できません'
    }

    if (-not $Version -and -not $Bump) {
        throw '-Version または -Bump を指定してください (例: -Bump Patch)'
    }

    $platforms = switch ($Platform) {
        'all' { @('x86', 'x64') }
        default { @($Platform) }
    }

    $currentVersion = Get-AssemblyInfoVersion -RepoRoot $repoRoot
    $newVersion = if ($Version) { $Version } else { Get-BumpedSemVer -Version $currentVersion -Bump $Bump }
    Assert-NewVersionIsGreater -CurrentVersion $currentVersion -NewVersion $newVersion

    $releaseBranch = "release/v$newVersion"

    Write-Host 'PDF Converter リリース準備'
    Write-Host "  現在のバージョン: $currentVersion"
    Write-Host "  新しいバージョン: $newVersion"
    Write-Host "  リリース日:       $Date"
    Write-Host "  リリースブランチ: $releaseBranch"
    Write-Host "  プラットフォーム: $($platforms -join ', ')"
    if ($DryRun) {
        Write-Host '  モード:           DryRun (ファイル・Git は変更しません)'
    }
    Write-Host ''

    Assert-GitReleaseBranch -RepoRoot $repoRoot -NewVersion $newVersion

    if (-not $DryRun -and -not $AllowDirty) {
        Assert-GitWorkingTreeClean -RepoRoot $repoRoot
    }

    # 署名無しで作ると配布物が更新を適用できないため、ファイルを変える前に確認する
    if (-not $SkipInstaller -or -not $SkipPortable) {
        Assert-UpdateSigningKeyConfigured -RepoRoot $repoRoot
    }

    Write-Host 'バージョン関連ファイルを更新しています...'
    Set-AssemblyInfoVersion -RepoRoot $repoRoot -NewVersion $newVersion -DryRun:$DryRun
    Update-ChangelogForRelease -RepoRoot $repoRoot -CurrentVersion $currentVersion -NewVersion $newVersion -ReleaseDate $Date -DryRun:$DryRun
    Write-Host ''

    if ($DryRun) {
        Write-Host 'DryRun のため、品質チェック・Git コミット・インストーラーとポータブル版の作成は行いません。'
        return
    }

    if ($SkipCheck) {
        Write-Host '品質チェックを省略しました (-SkipCheck)'
        Write-Host ''
    }
    else {
        foreach ($plat in $platforms) {
            Write-Host "品質チェックを実行しています (Release / $plat)..."
            Invoke-ChildScript -Name 'build-and-test.ps1' -Arguments @{ Configuration = 'Release'; Platform = $plat }
            Write-Host ''
        }
    }

    $releaseFiles = Get-ReleaseVersionFilePaths
    if ($NoCommit) {
        Write-Host 'Git コミットを省略しました (-NoCommit)'
        Write-Host "  手動でコミットする場合: git add $($releaseFiles -join ' ')"
        Write-Host "  コミットメッセージ: Release: v$newVersion"
        Write-Host ''
    }
    else {
        New-ReleaseGitCommit -RepoRoot $repoRoot -NewVersion $newVersion -FilePaths $releaseFiles
        Write-Host ''
    }

    if ($SkipInstaller) {
        Write-Host 'インストーラー作成を省略しました (-SkipInstaller)'
        Write-Host ''
    }
    else {
        Write-Host 'インストーラーを作成しています...'
        Invoke-ChildScript -Name 'build-installer.ps1' -Arguments @{ Platform = $Platform }
        Write-Host ''
    }

    if ($SkipPortable) {
        Write-Host 'ポータブル版の作成を省略しました (-SkipPortable)'
        Write-Host ''
    }
    else {
        # インストーラーを作った直後なら publish 出力をそのまま使う
        Write-Host 'ポータブル版を作成しています...'
        Invoke-ChildScript -Name 'build-portable.ps1' -Arguments @{ Platform = $Platform; SkipBuild = (-not $SkipInstaller) }
        Write-Host ''
    }

    Write-Host 'リリース手順が完了しました。'
    Write-Host ''
    Write-Host '次の作業 (手動):'
    Write-Host "  - リモートへ push: git push -u origin $releaseBranch"
    Write-Host "  - PR で $releaseBranch を develop と main へマージする"
    Write-Host "  - Gitea 上で v$newVersion タグを付与する"
    Write-Host '  - GitHub Release へインストーラーとポータブル版をアップロードする'
    Write-Host "      dist\PdfConverter-$newVersion-<platform>-setup.exe"
    Write-Host "      dist\PdfConverter-$newVersion-<platform>-setup.exe.sig (署名。これが無いと更新を適用できない)"
    Write-Host "      dist\PdfConverter-$newVersion-<platform>-portable.zip"
    Write-Host "      dist\PdfConverter-$newVersion-<platform>-portable.zip.sig (署名。これが無いとポータブル版を更新できない)"
}
catch {
    Write-Host ''
    Write-Host "エラー: $($_.Exception.Message)" -ForegroundColor Red
    $exitCode = 1
}
finally {
    if ($shouldPauseOnExit) {
        Wait-ForKeyPress
    }

    if ($exitCode -ne 0) {
        exit $exitCode
    }
}
