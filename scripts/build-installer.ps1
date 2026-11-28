# PDF Converter インストーラーのビルドスクリプト
#
# 必要環境:
#   - .NET SDK 11 以降 (dotnet CLI)
#   - Inno Setup 6 — ISCC.exe
#   - mdBook (利用者向けドキュメントの生成。版は build-user-docs.ps1 の $UserDocsMdBookVersion に固定)
#
# アプリは .NET ランタイム同梱 (self-contained) で dotnet publish し、
# src\PdfConverter\bin\<Platform>\publish の内容をインストーラーへ格納します。
# 利用者向けドキュメント (docs/user) は mdBook で HTML 化し、publish 出力の docs\ に複製します。
#
# 環境変数 PDF_CONVERTER_UPDATE_SIGNING_KEY に署名用の秘密鍵 (PEM) のパスを設定している場合は、
# 作成したインストーラーごとに更新用の署名 (<ファイル名>.sig) も出力します (sign-update.ps1。PowerShell 7 以降が必要)。
#
# インストーラーは管理者権限を要求しないユーザー単位インストールです。
# インストール先: %LOCALAPPDATA%\Programs\PDF Converter
#
# 使用例:
#   .\build-installer.ps1                    # x64 のみ
#   .\build-installer.ps1 -Platform all        # x86 と x64 の両方
#   .\build-installer.ps1 -SkipBuild           # publish をスキップ (既存の publish 出力を使用)
#   .\build-installer.ps1 -NoPause             # 完了後のキー入力待ちを省略 (ターミナル向け)

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

$issPath = Join-Path $PSScriptRoot 'PdfConverter.iss'
$distDir = $script:PublishDistDir

function Find-ISCC {
    $programFilesX86 = ${env:ProgramFiles(x86)}
    $programFiles = $env:ProgramFiles
    $localAppData = $env:LocalAppData

    $candidates = @(
        (Join-Path $programFilesX86 'Inno Setup 6\ISCC.exe'),
        (Join-Path $programFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $localAppData 'Programs\Inno Setup 6\ISCC.exe')
    )

    foreach ($path in $candidates) {
        if (Test-Path $path) { return $path }
    }

    $command = Get-Command iscc -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    throw "Inno Setup 6 (ISCC.exe) が見つかりません。winget install JRSoftware.InnoSetup でインストールできます。"
}

function Build-Installer {
    param(
        [string] $TargetPlatform,
        [string] $AppVersion,
        [string] $UserDocsDir
    )

    $sourceDir = Get-PublishDir -TargetPlatform $TargetPlatform
    Complete-PublishOutput -TargetPlatform $TargetPlatform -UserDocsDir $UserDocsDir

    $iscc = Find-ISCC
    Write-Host "Inno Setup: $iscc"
    Write-Host "インストーラーを作成しています ($TargetPlatform)..."

    & $iscc `
        "/DAppVersion=$AppVersion" `
        "/DPlatform=$TargetPlatform" `
        "/DSourceDir=$sourceDir" `
        $issPath

    if ($LASTEXITCODE -ne 0) {
        throw "インストーラーの作成に失敗しました (Platform=$TargetPlatform)"
    }
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
    # publish → 通知生成 → インストーラー作成をプラットフォームごとに続けて行う
    foreach ($plat in $platformsToBuild) {
        Invoke-PublishOrRestore -TargetPlatform $plat -SkipBuild:$SkipBuild
        Build-Installer -TargetPlatform $plat -AppVersion $appVersion -UserDocsDir $userDocsDir
    }

    # dist\ にある同じ版の配布物すべてが対象 (今回作らなかったプラットフォームの分・ポータブル版も含む)
    Invoke-UpdateSigning -AppVersion $appVersion

    Write-Host ''
    Write-Host '完了。出力先:'
    Get-ChildItem -LiteralPath $distDir -Filter "PdfConverter-$appVersion-*-setup.exe*" | ForEach-Object {
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
