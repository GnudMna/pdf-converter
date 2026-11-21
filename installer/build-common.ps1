# PDF Converter — ビルドスクリプト共通関数
#
# installer/ 配下のスクリプトからドットソースして使用します。
#   . (Join-Path $PSScriptRoot 'build-common.ps1')

$script:RequiredDotNetSdkMajor = 10

function Find-DotNet {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $command) {
        throw @"
.NET SDK (dotnet CLI) が見つかりません。
.NET SDK $script:RequiredDotNetSdkMajor 以降を https://dotnet.microsoft.com/download からインストールしてください。
"@
    }

    $sdks = & $command.Source --list-sdks 2>$null
    $hasRequiredSdk = $sdks | Where-Object {
        $_ -match '^(\d+)\.' -and [int]$Matches[1] -ge $script:RequiredDotNetSdkMajor
    }
    if (-not $hasRequiredSdk) {
        throw ".NET SDK $script:RequiredDotNetSdkMajor 以降が必要です。インストール済み: $($sdks -join ', ')"
    }

    return $command.Source
}

function Resolve-SolutionPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot,

        [string] $PreferredPath
    )

    if ($PreferredPath) {
        if (-not (Test-Path -LiteralPath $PreferredPath)) {
            throw "指定されたソリューションが見つかりません: $PreferredPath"
        }

        return (Resolve-Path -LiteralPath $PreferredPath).Path
    }

    $slnxPath = Join-Path $RepoRoot 'src\PdfConverter.slnx'
    if (-not (Test-Path -LiteralPath $slnxPath)) {
        throw "ソリューションファイルが見つかりません: $slnxPath"
    }

    return (Resolve-Path -LiteralPath $slnxPath).Path
}

function Restore-Packages {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Platform
    )

    $dotnet = Find-DotNet
    Write-Host "NuGet パッケージを復元しています ($Platform)..."
    & $dotnet restore $Path "-p:Platform=$Platform"
    if ($LASTEXITCODE -ne 0) {
        throw 'NuGet パッケージの復元に失敗しました'
    }
}

function Test-DoubleClickLaunch {
    try {
        $parentId = (Get-CimInstance Win32_Process -Filter "ProcessId=$PID").ParentProcessId
        $parentName = (Get-CimInstance Win32_Process -Filter "ProcessId=$parentId").Name
        return $parentName -in @('explorer.exe', 'OpenWith.exe')
    }
    catch {
        return $false
    }
}

function Wait-ForKeyPress {
    Write-Host ''
    Write-Host '終了するには何かキーを押してください...'
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
}
