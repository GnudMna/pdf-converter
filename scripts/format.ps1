# PDF Converter — 書式の一括修正スクリプト
#
# src\ 配下の C# ファイルを .editorconfig の書式 (改行コード・インデント・文字コード・末尾の空白など) に直す。
# build-and-test.ps1 の書式チェックで違反が出たときに使う。
#
# 必要環境:
#   - .NET SDK 11 以降 (dotnet CLI)
#
# 使用例:
#   .\format.ps1
#   .\format.ps1 -NoPause         # 完了後のキー入力待ちを省略

[CmdletBinding()]
param(
    [switch] $NoPause
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'build-common.ps1')

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

$shouldPauseOnExit = -not $NoPause -and (Test-DoubleClickLaunch)
$exitCode = 0

try {
    Write-Host '書式を直しています...'
    Test-CodeFormat -RepoRoot $repoRoot -Fix
    Write-Host '完了しました。'
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
