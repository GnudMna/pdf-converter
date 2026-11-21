# PDF Converter インストーラーのビルドスクリプト
#
# 必要環境:
#   - Visual Studio 2019 以降（または Build Tools）— MSBuild
#   - NuGet CLI (nuget.exe) — Visual Studio に同梱、または PATH 上の nuget
#   - Inno Setup 6 — ISCC.exe
#
# インストーラーは管理者権限を要求しないユーザー単位インストールです。
# インストール先: %LOCALAPPDATA%\Programs\PDF Converter
#
# 使用例:
#   .\build-installer.ps1                    # x64 のみ
#   .\build-installer.ps1 -Platform all        # x86 と x64 の両方
#   .\build-installer.ps1 -SkipBuild           # アプリの再ビルドをスキップ
#   .\build-installer.ps1 -SkipRestore         # パッケージ復元をスキップ
#   .\build-installer.ps1 -NoPause             # 完了後のキー入力待ちを省略（ターミナル向け）

[CmdletBinding()]
param(
    [ValidateSet('x64', 'x86', 'all')]
    [string] $Platform = 'x64',

    [switch] $SkipBuild,

    [switch] $SkipRestore,

    [switch] $NoPause
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'build-common.ps1')

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$slnxPath = Join-Path $repoRoot 'src\PdfConverter.slnx'
$csprojPath = Join-Path $repoRoot 'src\PdfConverter\PdfConverter.csproj'
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

function Build-App {
    param([string[]] $Platforms)

    $msbuild = Find-MSBuild
    Write-Host "MSBuild: $msbuild"

    foreach ($plat in $Platforms) {
        Write-Host "アプリをビルドしています (Release / $plat)..."
        & $msbuild $csprojPath /p:Configuration=Release /p:Platform=$plat /t:Rebuild /v:minimal
        if ($LASTEXITCODE -ne 0) {
            throw "ビルドに失敗しました (Platform=$plat)"
        }
    }
}

function Write-ThirdPartyNotices {
    param(
        [string] $TargetPlatform
    )

    $sourceDir = Join-Path $repoRoot "src\PdfConverter\bin\$TargetPlatform\Release"
    $noticesPath = Join-Path $sourceDir 'THIRD-PARTY-NOTICES.txt'
    $generateScript = Join-Path $PSScriptRoot 'generate-third-party-notices.ps1'

    Write-Host "THIRD-PARTY-NOTICES を生成しています ($TargetPlatform)..."
    & $generateScript -OutputPath $noticesPath -Platform $TargetPlatform
}

function Build-Installer {
    param(
        [string] $TargetPlatform,
        [string] $AppVersion
    )

    $sourceDir = Join-Path $repoRoot "src\PdfConverter\bin\$TargetPlatform\Release"
    if (-not (Test-Path (Join-Path $sourceDir 'PDF Converter.exe'))) {
        throw "ビルド出力が見つかりません: $sourceDir`n先にビルドを実行するか、-SkipBuild を外してください。"
    }

    Write-ThirdPartyNotices -TargetPlatform $TargetPlatform

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

    if (-not $SkipRestore) {
        Restore-NuGetPackages -SolutionPath $slnxPath
    }

    $appVersion = Get-AppVersion
    Write-Host "バージョン: $appVersion"

    if (-not $SkipBuild) {
        Build-App -Platforms $platformsToBuild
    }

    New-Item -ItemType Directory -Path $distDir -Force | Out-Null

    foreach ($plat in $platformsToBuild) {
        Build-Installer -TargetPlatform $plat -AppVersion $appVersion
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
