# PDF Converter — ビルドと単体テストの一括実行スクリプト
#
# 必要環境:
#   - .NET SDK 11 以降 (dotnet CLI)
#
# 実行する項目:
#   1. NuGet パッケージの復元 (locked mode。packages.lock.json との食い違いをエラーにする)
#   2. 書式チェック (dotnet format whitespace。改行コード・インデント・文字コードなど .editorconfig の書式)
#   3. ソリューションのリビルド (コード解析の警告もエラー)
#   4. 単体テスト (-Coverage のときはカバレッジも計測する)
#
# 使用例:
#   .\build-and-test.ps1                  # Debug / x64 でビルドしてテスト実行
#   .\build-and-test.ps1 -Configuration Release
#   .\build-and-test.ps1 -Platform x86
#   .\build-and-test.ps1 -SkipRestore     # パッケージ復元をスキップ
#   .\build-and-test.ps1 -Coverage        # カバレッジを計測して artifacts\coverage\ にレポートを出す
#   .\build-and-test.ps1 -NoPause         # 完了後のキー入力待ちを省略

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [ValidateSet('x64', 'x86')]
    [string] $Platform = 'x64',

    [switch] $SkipRestore,

    [switch] $Coverage,

    [switch] $NoPause,

    [string] $SolutionPath
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'build-common.ps1')

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$solutionPath = Resolve-SolutionPath -RepoRoot $repoRoot -PreferredPath $SolutionPath
$testExePath = Join-Path $repoRoot "src\PdfConverter.Tests\bin\$Platform\$Configuration\PdfConverter.Tests.exe"
$coverageDir = Join-Path $repoRoot 'artifacts\coverage'
$coverageSettingsPath = Join-Path $PSScriptRoot 'coverage.settings.xml'

function Assert-TestResultPassed {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "テスト結果が見つかりません: $Path"
    }

    [xml] $result = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $failed = 0
    $errors = 0
    foreach ($assembly in $result.assemblies.assembly) {
        $failed += [int] $assembly.failed
        $errors += [int] $assembly.errors
    }

    if ($failed -ne 0 -or $errors -ne 0) {
        throw "テストに失敗しました (失敗: $failed 件、エラー: $errors 件)"
    }
}

$shouldPauseOnExit = -not $NoPause -and (Test-DoubleClickLaunch)
$exitCode = 0

try {
    $dotnet = Find-DotNet
    Write-Host "dotnet: $dotnet"

    if (-not $SkipRestore) {
        Restore-Packages -Path $solutionPath -Platform $Platform
    }

    Write-Host '書式をチェックしています...'
    Test-CodeFormat -RepoRoot $repoRoot

    Write-Host "ソリューションをビルドしています ($Configuration / $Platform)..."
    & $dotnet build $solutionPath -c $Configuration "-p:Platform=$Platform" --no-restore --no-incremental -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'ビルドに失敗しました'
    }

    if (-not (Test-Path -LiteralPath $testExePath)) {
        throw "テスト実行ファイルが見つかりません: $testExePath"
    }

    Write-Host ''
    if ($Coverage) {
        # 計測・レポート生成のツールはリポジトリのツールマニフェスト (dotnet-tools.json) で版を固定している
        Write-Host 'ローカルツールを復元しています...'
        Push-Location $repoRoot
        try {
            & $dotnet tool restore
            if ($LASTEXITCODE -ne 0) {
                throw 'ローカルツールの復元に失敗しました'
            }

            if (Test-Path -LiteralPath $coverageDir) {
                Remove-Item -LiteralPath $coverageDir -Recurse -Force
            }

            # dotnet-coverage の終了コードは計測対象の終了コードを引き継がない場合があるため、テストの成否は結果の XML で判定する
            $coberturaPath = Join-Path $coverageDir 'coverage.cobertura.xml'
            $testResultPath = Join-Path $coverageDir 'test-results.xml'
            Write-Host '単体テストを実行し、カバレッジを計測しています...'
            & $dotnet dotnet-coverage collect --nologo --settings $coverageSettingsPath -f cobertura -o $coberturaPath $testExePath -result-xml $testResultPath
            $collectExitCode = $LASTEXITCODE

            Assert-TestResultPassed -Path $testResultPath
            if ($collectExitCode -ne 0) {
                throw "カバレッジの計測に失敗しました (終了コード: $collectExitCode)"
            }

            $reportDir = Join-Path $coverageDir 'report'
            & $dotnet reportgenerator "-reports:$coberturaPath" "-targetdir:$reportDir" '-reporttypes:Html;TextSummary' '-verbosity:Warning'
            if ($LASTEXITCODE -ne 0) {
                throw 'カバレッジレポートの生成に失敗しました'
            }
        }
        finally {
            Pop-Location
        }

        Write-Host ''
        Get-Content -LiteralPath (Join-Path $reportDir 'Summary.txt') -Encoding UTF8 | Select-Object -First 16 | ForEach-Object { Write-Host $_ }
        Write-Host "カバレッジレポート: $(Join-Path $reportDir 'index.html')"
    }
    else {
        Write-Host '単体テストを実行しています...'
        & $testExePath
        if ($LASTEXITCODE -ne 0) {
            throw "テストに失敗しました (終了コード: $LASTEXITCODE)"
        }
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
