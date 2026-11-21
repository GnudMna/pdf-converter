# PDF Converter — ビルドスクリプト共通関数
#
# installer/ 配下のスクリプトからドットソースして使用します。
#   . (Join-Path $PSScriptRoot 'build-common.ps1')

function Find-NuGet {
    $command = Get-Command nuget -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $programFilesX86 = ${env:ProgramFiles(x86)}
    $programFiles = $env:ProgramFiles

    $candidates = @(
        (Join-Path $programFilesX86 'NuGet\nuget.exe'),
        (Join-Path $programFiles 'NuGet\nuget.exe')
    )

    $vswhere = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $vsNuGet = & $vswhere `
            -version '[16.0,99.0)' `
            -latest `
            -find 'Common7\IDE\CommonExtensions\Microsoft\NuGet\NuGet.exe' `
            | Select-Object -First 1
        if ($vsNuGet) {
            $candidates = @($vsNuGet) + $candidates
        }
    }

    foreach ($path in $candidates) {
        if ($path -and (Test-Path $path)) {
            return $path
        }
    }

    throw @'
NuGet CLI (nuget.exe) が見つかりません。
Visual Studio 2019 以降をインストールするか、https://www.nuget.org/downloads から nuget.exe を取得し PATH に追加してください。
'@
}

function Test-SlnxSupported {
    param(
        [string] $MsBuildPath
    )

    if (-not $MsBuildPath) {
        try {
            $MsBuildPath = Find-MSBuild
        }
        catch {
            return $false
        }
    }

    if (-not (Test-Path -LiteralPath $MsBuildPath)) {
        return $false
    }

    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($MsBuildPath)
    $fileVersion = $versionInfo.FileVersion
    if (-not $fileVersion) {
        return $false
    }

    $parts = $fileVersion.Split('.')
    if ($parts.Count -lt 2) {
        return $false
    }

    $major = [int]$parts[0]
    $minor = [int]$parts[1]

    # .slnx は MSBuild 17.10 (Visual Studio 2022 17.10) 以降でサポート
    return ($major -gt 17) -or ($major -eq 17 -and $minor -ge 10)
}

function Resolve-SolutionPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot,

        [string] $PreferredPath,

        [switch] $RequireSlnx
    )

    $slnxPath = Join-Path $RepoRoot 'src\PdfConverter.slnx'
    $slnPath = Join-Path $RepoRoot 'src\PdfConverter.sln'

    if ($PreferredPath) {
        if (-not (Test-Path -LiteralPath $PreferredPath)) {
            throw "指定されたソリューションが見つかりません: $PreferredPath"
        }

        if ($RequireSlnx -and $PreferredPath -notlike '*.slnx') {
            throw 'テスト実行には PdfConverter.slnx を指定してください。PdfConverter.sln にはテストプロジェクトが含まれていません。'
        }

        return (Resolve-Path -LiteralPath $PreferredPath).Path
    }

    $slnxExists = Test-Path -LiteralPath $slnxPath
    $slnExists = Test-Path -LiteralPath $slnPath

    if ($RequireSlnx) {
        if (-not $slnxExists) {
            throw "テスト実行には PdfConverter.slnx が必要です: $slnxPath"
        }

        if (-not (Test-SlnxSupported)) {
            throw @'
テスト実行には MSBuild 17.10 以降（.slnx 対応）が必要です。
SDK 形式の PdfConverter.Tests は PdfConverter.slnx でのみビルドできます。
Visual Studio 2022 17.10 以降、または Build Tools の MSBuild を更新してください。
'@
        }

        Write-Host 'ソリューション: PdfConverter.slnx（テストプロジェクト含む）'
        return (Resolve-Path -LiteralPath $slnxPath).Path
    }

    if (-not $slnxExists -and -not $slnExists) {
        throw "ソリューションファイルが見つかりません (PdfConverter.slnx / PdfConverter.sln): $(Join-Path $RepoRoot 'src')"
    }

    if ($slnxExists -and (Test-SlnxSupported)) {
        Write-Host 'ソリューション: PdfConverter.slnx'
        return (Resolve-Path -LiteralPath $slnxPath).Path
    }

    if ($slnExists) {
        if ($slnxExists) {
            Write-Host 'PdfConverter.slnx はこの環境の MSBuild では未対応のため、PdfConverter.sln（アプリ本体のみ）を使用します。'
        }
        else {
            Write-Host 'ソリューション: PdfConverter.sln'
        }

        return (Resolve-Path -LiteralPath $slnPath).Path
    }

    throw @'
PdfConverter.slnx はこの環境の MSBuild では開けません。
Visual Studio 2022 17.10 以降、または Build Tools の MSBuild 17.10 以降が必要です。
互換用の PdfConverter.sln を用意するか、MSBuild を更新してください。
'@
}

function Invoke-DotNetRestoreForSlnx {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SolutionPath
    )

    if ($SolutionPath -notlike '*.slnx') {
        return
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw @'
SDK 形式の PdfConverter.Tests を復元するには .NET SDK (dotnet CLI) が必要です。
https://dotnet.microsoft.com/download からインストールしてください。
'@
    }

    Write-Host 'SDK 形式プロジェクト向けに dotnet restore を実行しています...'
    & $dotnet.Source restore $SolutionPath
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet restore に失敗しました'
    }
}

function Find-MSBuild {
    $programFilesX86 = ${env:ProgramFiles(x86)}
    $programFiles = $env:ProgramFiles
    $vswhere = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'

    if (Test-Path $vswhere) {
        $path = & $vswhere `
            -version '[16.0,99.0)' `
            -latest `
            -requires Microsoft.Component.MSBuild `
            -find 'MSBuild\**\Bin\MSBuild.exe' `
            | Select-Object -First 1
        if ($path) { return $path }

        $path = & $vswhere `
            -version '[16.0,99.0)' `
            -latest `
            -products * `
            -find 'MSBuild\**\Bin\MSBuild.exe' `
            | Select-Object -First 1
        if ($path) { return $path }
    }

    $editions = @('Community', 'Professional', 'Enterprise', 'BuildTools')
    $years = @('2019', '2022')
    $roots = @($programFilesX86, $programFiles)

    foreach ($root in $roots) {
        foreach ($year in $years) {
            foreach ($edition in $editions) {
                $path = Join-Path $root "Microsoft Visual Studio\$year\$edition\MSBuild\Current\Bin\MSBuild.exe"
                if (Test-Path $path) { return $path }
            }
        }
    }

    throw 'MSBuild が見つかりません。Visual Studio 2019 以降（または Build Tools）をインストールしてください。'
}

function Restore-NuGetPackages {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SolutionPath,

        [bool] $AllowSlnFallback = $true
    )

    if (-not (Test-Path -LiteralPath $SolutionPath)) {
        throw "ソリューションが見つかりません: $SolutionPath"
    }

    Write-Host 'NuGet パッケージを復元しています...'

    $nugetPath = $null
    $nugetCommand = Get-Command nuget -ErrorAction SilentlyContinue
    if ($nugetCommand) {
        $nugetPath = $nugetCommand.Source
    }
    else {
        try {
            $nugetPath = Find-NuGet
        }
        catch {
            $nugetPath = $null
        }
    }

    if ($nugetPath) {
        Write-Host "NuGet: $nugetPath"
        & $nugetPath restore $SolutionPath
        if ($LASTEXITCODE -ne 0) {
            $fallbackSln = $SolutionPath -replace '\.slnx$', '.sln'
            if ($AllowSlnFallback -and $SolutionPath -like '*.slnx' -and (Test-Path -LiteralPath $fallbackSln)) {
                Write-Host 'NuGet が .slnx に対応していないため、PdfConverter.sln（アプリ本体のみ）で復元を再試行します...'
                & $nugetPath restore $fallbackSln
                if ($LASTEXITCODE -ne 0) {
                    throw 'NuGet パッケージの復元に失敗しました'
                }

                Invoke-DotNetRestoreForSlnx -SolutionPath $SolutionPath
                return
            }

            throw 'NuGet パッケージの復元に失敗しました'
        }

        Invoke-DotNetRestoreForSlnx -SolutionPath $SolutionPath
        return
    }

    Write-Host 'NuGet CLI が見つからないため、MSBuild で復元します...'
    $msbuild = Find-MSBuild
    Write-Host "MSBuild: $msbuild"
    & $msbuild $SolutionPath /t:Restore /v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'MSBuild によるパッケージ復元に失敗しました'
    }

    Invoke-DotNetRestoreForSlnx -SolutionPath $SolutionPath
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
