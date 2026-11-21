# PDF Converter — ビルドと単体テストの一括実行スクリプト
#
# 必要環境:
#   - .NET SDK 10 以降 (dotnet CLI)
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
$solutionPath = Resolve-SolutionPath -RepoRoot $repoRoot -PreferredPath $SolutionPath
$testExePath = Join-Path $repoRoot "src\PdfConverter.Tests\bin\$Platform\$Configuration\net10.0-windows\PdfConverter.Tests.exe"

$shouldPauseOnExit = -not $NoPause -and (Test-DoubleClickLaunch)
$exitCode = 0

try {
    $dotnet = Find-DotNet
    Write-Host "dotnet: $dotnet"

    if (-not $SkipRestore) {
        Restore-Packages -Path $solutionPath -Platform $Platform
    }

    Write-Host "ソリューションをビルドしています ($Configuration / $Platform)..."
    & $dotnet build $solutionPath -c $Configuration "-p:Platform=$Platform" --no-restore --no-incremental -v:minimal
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
