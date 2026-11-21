# PDF Converter — ビルドと単体テストの一括実行スクリプト
#
# 必要環境:
#   - Visual Studio 2022 17.10 以降（または MSBuild 17.10+）— .slnx 対応
#   - .NET SDK (dotnet CLI) — SDK 形式の PdfConverter.Tests の復元に必要
#   - NuGet CLI (nuget.exe) — Visual Studio に同梱、または PATH 上の nuget
#
# PdfConverter.Tests は SDK 形式のため、PdfConverter.slnx でのみビルドします。
# 互換用の PdfConverter.sln にはテストプロジェクトは含まれません。
#
# 使用例:
#   .\build-and-test.ps1                  # Debug / x64 でビルドしてテスト実行
#   .\build-and-test.ps1 -Configuration Release
#   .\build-and-test.ps1 -Platform x86
#   .\build-and-test.ps1 -SkipRestore     # パッケージ復元をスキップ
#   .\build-and-test.ps1 -NoPause         # 完了後のキー入力待ちを省略

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [ValidateSet('x64', 'x86')]
    [string] $Platform = 'x64',

    [switch] $SkipRestore,

    [switch] $NoPause,

    [string] $SolutionPath
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'build-common.ps1')

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$solutionPath = Resolve-SolutionPath -RepoRoot $repoRoot -PreferredPath $SolutionPath -RequireSlnx
$testExePath = Join-Path $repoRoot "src\PdfConverter.Tests\bin\$Platform\$Configuration\net481\PdfConverter.Tests.exe"

$shouldPauseOnExit = -not $NoPause -and (Test-DoubleClickLaunch)
$exitCode = 0

try {
    if (-not $SkipRestore) {
        Restore-NuGetPackages -SolutionPath $solutionPath -AllowSlnFallback:$false
    }

    $msbuild = Find-MSBuild
    Write-Host "MSBuild: $msbuild"
    Write-Host "ソリューションをビルドしています ($Configuration / $Platform)..."

    & $msbuild $solutionPath /p:Configuration=$Configuration /p:Platform=$Platform /t:Rebuild /v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'ビルドに失敗しました'
    }

    if (-not (Test-Path -LiteralPath $testExePath)) {
        throw "テスト実行ファイルが見つかりません: $testExePath"
    }

    Write-Host ''
    Write-Host '単体テストを実行しています...'
    & $testExePath
    if ($LASTEXITCODE -ne 0) {
        throw "テストに失敗しました (終了コード: $LASTEXITCODE)"
    }

    Write-Host ''
    Write-Host 'ビルドとテストが正常に完了しました。'
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
