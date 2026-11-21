# PDF Converter インストーラーのビルドスクリプト
#
# 必要環境:
#   - .NET SDK 10 以降 (dotnet CLI)
#   - Inno Setup 6 — ISCC.exe
#   - mdBook (利用者向けドキュメントの生成。版は build-user-docs.ps1 の $UserDocsMdBookVersion に固定)
#
# アプリは .NET ランタイム同梱 (self-contained) で dotnet publish し、
# src\PdfConverter\bin\<Platform>\publish の内容をインストーラーへ格納します。
# 利用者向けドキュメント (docs/user) は mdBook で HTML 化し、publish 出力の docs\ に複製します。
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

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectPath = Join-Path $repoRoot 'src\PdfConverter\PdfConverter.csproj'
$issPath = Join-Path $PSScriptRoot 'PdfConverter.iss'
$distDir = Join-Path $repoRoot 'dist'
$assemblyInfoPath = Join-Path $repoRoot 'src\PdfConverter\Properties\AssemblyInfo.cs'

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

function Get-AppVersion {
    $content = Get-Content -LiteralPath $assemblyInfoPath -Raw
    if ($content -match 'AssemblyInformationalVersion\("([^"]+)"\)') {
        return $Matches[1]
    }

    throw "AssemblyInfo.cs からバージョンを取得できませんでした: $assemblyInfoPath"
}

function Get-PublishDir {
    param(
        [string] $TargetPlatform
    )

    return Join-Path $repoRoot "src\PdfConverter\bin\$TargetPlatform\publish"
}

function Publish-App {
    param(
        [string] $TargetPlatform
    )

    $dotnet = Find-DotNet
    $publishDir = Get-PublishDir -TargetPlatform $TargetPlatform

    # 前回の publish 出力が混ざらないよう作り直す
    if (Test-Path -LiteralPath $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }

    Write-Host "アプリを publish しています (Release / $TargetPlatform)..."
    & $dotnet publish $projectPath -c Release "-p:Platform=$TargetPlatform" -o $publishDir -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "publish に失敗しました (Platform=$TargetPlatform)"
    }
}

function Write-ThirdPartyNotices {
    param(
        [string] $TargetPlatform
    )

    $sourceDir = Get-PublishDir -TargetPlatform $TargetPlatform
    $noticesPath = Join-Path $sourceDir 'THIRD-PARTY-NOTICES.txt'
    $generateScript = Join-Path $PSScriptRoot 'generate-third-party-notices.ps1'

    Write-Host "THIRD-PARTY-NOTICES を生成しています ($TargetPlatform)..."
    & $generateScript -OutputPath $noticesPath -Platform $TargetPlatform
}

function Copy-UserDocs {
    param(
        [string] $TargetPlatform,
        [string] $UserDocsDir
    )

    $docsDir = Join-Path (Get-PublishDir -TargetPlatform $TargetPlatform) 'docs'

    # -SkipBuild で前回の publish 出力を使う場合に、旧版のファイルが混ざらないよう作り直す
    if (Test-Path -LiteralPath $docsDir) {
        Remove-Item -LiteralPath $docsDir -Recurse -Force
    }

    Write-Host "利用者向けドキュメントを複製しています ($TargetPlatform)..."
    Copy-Item -LiteralPath $UserDocsDir -Destination $docsDir -Recurse -Force
    Assert-UserDocsPages -DocsDir $docsDir
}

function Build-Installer {
    param(
        [string] $TargetPlatform,
        [string] $AppVersion,
        [string] $UserDocsDir
    )

    $sourceDir = Get-PublishDir -TargetPlatform $TargetPlatform
    if (-not (Test-Path (Join-Path $sourceDir 'PDF Converter.exe'))) {
        throw "publish 出力が見つかりません: $sourceDir`n先に publish を実行するか、-SkipBuild を外してください。"
    }

    Write-ThirdPartyNotices -TargetPlatform $TargetPlatform
    Copy-UserDocs -TargetPlatform $TargetPlatform -UserDocsDir $UserDocsDir

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
    $platformsToBuild = switch ($Platform) {
        'all' { @('x86', 'x64') }
        default { @($Platform) }
    }

    $appVersion = Get-AppVersion
    Write-Host "バージョン: $appVersion"

    New-Item -ItemType Directory -Path $distDir -Force | Out-Null

    # 利用者向けドキュメントはプラットフォームに依らないため 1 回だけ生成する
    $userDocsDir = Invoke-UserDocsBuild -RepoRoot $repoRoot

    # project.assets.json は最後に復元したプラットフォームの内容になるため、
    # publish → 通知生成 → インストーラー作成をプラットフォームごとに続けて行う
    foreach ($plat in $platformsToBuild) {
        if ($SkipBuild) {
            Restore-Packages -Path $projectPath -Platform $plat
        }
        else {
            Publish-App -TargetPlatform $plat
        }

        Build-Installer -TargetPlatform $plat -AppVersion $appVersion -UserDocsDir $userDocsDir
    }

    Write-Host ''
    Write-Host '完了。出力先:'
    Get-ChildItem -LiteralPath $distDir -Filter "PdfConverter-$appVersion-*-setup.exe" | ForEach-Object {
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
