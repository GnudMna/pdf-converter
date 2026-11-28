# PDF Converter ポータブル版 (zip) のビルドスクリプト
#
# 必要環境:
#   - .NET SDK 11 以降 (dotnet CLI)
#   - mdBook (利用者向けドキュメントの生成。版は build-user-docs.ps1 の $UserDocsMdBookVersion に固定)
#
# インストーラーと同じ publish 出力 (src\PdfConverter\bin\<Platform>\publish。THIRD-PARTY-NOTICES.txt と docs\ を含む) に、
# 次を加えて dist\PdfConverter-<版>-<Platform>-portable.zip を作ります。
#   - LICENSE.txt             本アプリの使用許諾
#   - portable.txt            ポータブル版であることを示すマーカー (scripts\portable\portable.txt)。アプリはこれがあると設定・ログを UserData\ に保存する
#   - portable-manifest.txt   zip に含むファイルの一覧。アプリ内の更新で、旧版にしか無いファイルを消すために使う
#
# 環境変数 PDF_CONVERTER_UPDATE_SIGNING_KEY に署名用の秘密鍵 (PEM) のパスを設定している場合は、
# 作成した zip ごとに更新用の署名 (<ファイル名>.sig) も出力します (sign-update.ps1。PowerShell 7 以降が必要)。
#
# 使用例:
#   .\build-portable.ps1                    # x64 のみ
#   .\build-portable.ps1 -Platform all        # x86 と x64 の両方
#   .\build-portable.ps1 -SkipBuild           # publish をスキップ (既存の publish 出力を使用)
#   .\build-portable.ps1 -NoPause             # 完了後のキー入力待ちを省略 (ターミナル向け)

[CmdletBinding()]
param(
    [ValidateSet('x64', 'x86', 'all')]
    [string] $Platform = 'x64',

    [switch] $SkipBuild,

    [switch] $NoPause
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'build-common.ps1')
. (Join-Path $PSScriptRoot 'build-user-docs.ps1')
. (Join-Path $PSScriptRoot 'publish-common.ps1')

$distDir = $script:PublishDistDir
$stagingRoot = Join-Path $script:PublishRepoRoot 'artifacts\portable'
$markerTemplatePath = Join-Path $PSScriptRoot 'portable\portable.txt'

# アプリ側 (PortableMode) の定数と一致させる
$markerFileName = 'portable.txt'
$manifestFileName = 'portable-manifest.txt'
$dataDirectoryName = 'UserData'

function Build-PortableZip {
    param(
        [string] $TargetPlatform,
        [string] $AppVersion,
        [string] $UserDocsDir
    )

    $publishDir = Get-PublishDir -TargetPlatform $TargetPlatform
    Complete-PublishOutput -TargetPlatform $TargetPlatform -UserDocsDir $UserDocsDir

    # publish 出力を汚さないよう、別のフォルダーで zip の中身をそろえる
    $stagingDir = Join-Path $stagingRoot $TargetPlatform
    if (Test-Path -LiteralPath $stagingDir) {
        Remove-Item -LiteralPath $stagingDir -Recurse -Force
    }

    Write-Host "ポータブル版の中身をそろえています ($TargetPlatform)..."
    New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null
    Copy-Item -Path (Join-Path $publishDir '*') -Destination $stagingDir -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $script:PublishRepoRoot 'LICENSE') -Destination (Join-Path $stagingDir 'LICENSE.txt') -Force
    Copy-Item -LiteralPath $markerTemplatePath -Destination (Join-Path $stagingDir $markerFileName) -Force

    # publish 出力から起動したときの利用者のデータが紛れ込まないようにする
    $dataDir = Join-Path $stagingDir $dataDirectoryName
    if (Test-Path -LiteralPath $dataDir) {
        Remove-Item -LiteralPath $dataDir -Recurse -Force
    }

    $stagingFullPath = (Resolve-Path -LiteralPath $stagingDir).Path.TrimEnd('\') + '\'
    $files = @(Get-ChildItem -LiteralPath $stagingDir -Recurse -File | ForEach-Object {
            $_.FullName.Substring($stagingFullPath.Length)
        })
    $files += $manifestFileName
    $manifest = ($files | Sort-Object -Unique) -join "`r`n"
    [System.IO.File]::WriteAllText((Join-Path $stagingDir $manifestFileName), $manifest + "`r`n", [System.Text.UTF8Encoding]::new($false))

    $zipPath = Join-Path $distDir "PdfConverter-$AppVersion-$TargetPlatform-portable.zip"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Write-Host "zip を作成しています ($TargetPlatform)..."
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $stagingDir,
        $zipPath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)
}

$shouldPauseOnExit = -not $NoPause -and (Test-DoubleClickLaunch)
$exitCode = 0

try {
    $platformsToBuild = Get-TargetPlatforms -Platform $Platform

    $appVersion = Get-AppVersion
    Write-Host "バージョン: $appVersion"

    New-Item -ItemType Directory -Path $distDir -Force | Out-Null

    # 利用者向けドキュメントはプラットフォームに依らないため 1 回だけ生成する
    $userDocsDir = Invoke-UserDocsBuild -RepoRoot $script:PublishRepoRoot

    # project.assets.json は最後に復元したプラットフォームの内容になるため、
    # publish → 通知生成 → zip 作成をプラットフォームごとに続けて行う
    foreach ($plat in $platformsToBuild) {
        Invoke-PublishOrRestore -TargetPlatform $plat -SkipBuild:$SkipBuild
        Build-PortableZip -TargetPlatform $plat -AppVersion $appVersion -UserDocsDir $userDocsDir
    }

    # dist\ にある同じ版の配布物すべてが対象 (今回作らなかったプラットフォームの分・インストーラーも含む)
    Invoke-UpdateSigning -AppVersion $appVersion

    Write-Host ''
    Write-Host '完了。出力先:'
    Get-ChildItem -LiteralPath $distDir -Filter "PdfConverter-$appVersion-*-portable.zip*" | ForEach-Object {
        Write-Host "  $($_.FullName)"
    }
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
