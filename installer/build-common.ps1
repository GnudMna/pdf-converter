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
        [string] $SolutionPath
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
            throw 'NuGet パッケージの復元に失敗しました'
        }

        return
    }

    Write-Host 'NuGet CLI が見つからないため、MSBuild で復元します...'
    $msbuild = Find-MSBuild
    Write-Host "MSBuild: $msbuild"
    & $msbuild $SolutionPath /t:Restore /v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'MSBuild によるパッケージ復元に失敗しました'
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) {
        Write-Host 'SDK 形式プロジェクト向けに dotnet restore を実行しています...'
        & $dotnet.Source restore $SolutionPath
        if ($LASTEXITCODE -ne 0) {
            throw 'dotnet restore に失敗しました'
        }
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
