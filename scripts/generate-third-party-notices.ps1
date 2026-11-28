# PDF Converter — THIRD-PARTY-NOTICES.txt 生成スクリプト
#
# obj/project.assets.json (dotnet restore の結果) と NuGet パッケージのメタデータから、
# サードパーティライセンス一覧を生成します。self-contained 配布のため
# 同梱される .NET ランタイムのライセンスも含めます。
# 末尾に利用者向けドキュメント (mdBook の HTML) に含まれる素材の表示 (licenses\user-docs-notices.txt) を追記します。
#
# 事前に対象プラットフォームで dotnet restore / publish を実行しておく必要があります。
#
# 使用例:
#   .\generate-third-party-notices.ps1 -OutputPath "..\src\PdfConverter\bin\x64\publish\THIRD-PARTY-NOTICES.txt"

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [ValidateSet('x64', 'x86')]
    [string] $Platform = 'x64'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$assetsPath = Join-Path $repoRoot 'src\PdfConverter\obj\project.assets.json'
$licensesFolder = Join-Path $PSScriptRoot 'licenses'
$runtimeIdentifier = "win-$Platform"

# 同梱される .NET ランタイムパック (WPF アプリの self-contained 配布で含まれるもの)
$runtimePackIds = @(
    'Microsoft.NETCore.App.Runtime',
    'Microsoft.WindowsDesktop.App.Runtime'
)

function Get-MitLicenseText {
    param([string] $CopyrightLine)

    @"
MIT License

$CopyrightLine

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
"@
}

function Get-Bsd3ClauseLicenseText {
    param([string] $CopyrightLine)

    @"
BSD 3-Clause License

$CopyrightLine

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its
   contributors may be used to endorse or promote products derived from
   this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
"@
}

function Get-PackageFolder {
    param(
        [string] $PackageId,
        [string] $PackageVersion
    )

    # グローバルパッケージフォルダーは小文字の <id>\<version> 構成
    foreach ($root in $packageRoots) {
        $folder = Join-Path $root "$($PackageId.ToLowerInvariant())\$($PackageVersion.ToLowerInvariant())"
        if (Test-Path -LiteralPath $folder) {
            return $folder
        }
    }

    throw "NuGet パッケージが見つかりません: $PackageId $PackageVersion (検索先: $($packageRoots -join ', '))"
}

function Get-NuGetPackageMetadata {
    param(
        [string] $PackageId,
        [string] $PackageVersion
    )

    $packageFolder = Get-PackageFolder -PackageId $PackageId -PackageVersion $PackageVersion
    $nuspec = Get-ChildItem -LiteralPath $packageFolder -Filter '*.nuspec' | Select-Object -First 1
    if (-not $nuspec) {
        throw "nuspec が見つかりません: $packageFolder"
    }

    $content = Get-Content -LiteralPath $nuspec.FullName -Raw

    $metadata = [ordered]@{
        Id       = $PackageId
        Version  = $PackageVersion
        License  = $null
        Authors  = $null
        Copyright = $null
    }

    if ($content -match '<id>([^<]+)</id>') { $metadata.Id = $Matches[1].Trim() }
    if ($content -match '<version>([^<]+)</version>') { $metadata.Version = $Matches[1].Trim() }
    if ($content -match '<license[^>]*>([^<]+)</license>') { $metadata.License = $Matches[1].Trim() }
    if ($content -match '<authors>([^<]+)</authors>') { $metadata.Authors = $Matches[1].Trim() }
    if ($content -match '<copyright>([^<]+)</copyright>') { $metadata.Copyright = $Matches[1].Trim() }

    if (-not $metadata.License) {
        throw "ライセンス情報を取得できませんでした: $PackageId $PackageVersion"
    }

    return $metadata
}

function Get-LicenseTextForPackage {
    param($Metadata)

    $copyrightLine = if ($Metadata.Copyright -and $Metadata.Copyright -ne 'MIT') {
        $Metadata.Copyright
    }
    elseif ($Metadata.Authors) {
        "Copyright (c) $($Metadata.Authors)"
    }
    else {
        "Copyright (c) $($Metadata.Id) contributors"
    }

    switch ($Metadata.License) {
        'MIT' { return Get-MitLicenseText -CopyrightLine $copyrightLine }
        'BSD-3-Clause' { return Get-Bsd3ClauseLicenseText -CopyrightLine $copyrightLine }
        'Apache-2.0' {
            $apacheLicense = Get-Content -LiteralPath (Join-Path $licensesFolder 'Apache-2.0.txt') -Raw
            return "$copyrightLine`r`n`r`n$apacheLicense"
        }
        default { throw "未対応のライセンス式です: $($Metadata.License) ($($Metadata.Id))" }
    }
}

function New-Section {
    param(
        [string] $Title,
        [string] $Body
    )

    $separator = ('-' * 78)
    return @(
        ''
        $separator
        $Title
        $separator
        ''
        $Body.TrimEnd()
        ''
    )
}

if (-not (Test-Path $assetsPath)) {
    throw "project.assets.json が見つかりません。先に dotnet restore を実行してください: $assetsPath"
}

$assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
$packageRoots = @($assets.packageFolders.PSObject.Properties.Name)

$target = $assets.targets.PSObject.Properties | Where-Object { $_.Name -like "*/$runtimeIdentifier" } | Select-Object -First 1
if (-not $target) {
    throw "project.assets.json に $runtimeIdentifier 向けの復元結果がありません。-p:Platform=$Platform で dotnet restore を実行してください。"
}

# 対象 RID で配布物にファイルを含めないパッケージ (例: PDFiumCore が依存する Linux / macOS 向け PDFium) は除外する
function Test-PackageHasAssets {
    param($PackageEntry)

    foreach ($kind in @('runtime', 'native')) {
        $assetGroup = $PackageEntry.$kind
        if ($assetGroup) {
            $files = @($assetGroup.PSObject.Properties.Name | Where-Object { $_ -notlike '*/_._' })
            if ($files.Count -gt 0) {
                return $true
            }
        }
    }

    return $false
}

$packages = $target.Value.PSObject.Properties |
    Where-Object { $_.Value.type -eq 'package' -and (Test-PackageHasAssets -PackageEntry $_.Value) } |
    ForEach-Object {
        $id, $version = $_.Name -split '/', 2
        [pscustomobject]@{ id = $id; version = $version }
    } |
    Sort-Object id

$frameworkInfo = $assets.project.frameworks.PSObject.Properties | Select-Object -First 1
$runtimePacks = $frameworkInfo.Value.downloadDependencies |
    Where-Object { $_.name -in ($runtimePackIds | ForEach-Object { "$_.$runtimeIdentifier" }) } |
    ForEach-Object {
        [pscustomobject]@{
            id      = $_.name
            version = ($_.version -replace '^\[([^,\]]+).*$', '$1').Trim()
        }
    } |
    Sort-Object id

if (@($runtimePacks).Count -ne $runtimePackIds.Count) {
    throw "project.assets.json から .NET ランタイムパックを特定できませんでした ($runtimeIdentifier)"
}

$sections = @(
    'THIRD-PARTY SOFTWARE NOTICES AND INFORMATION'
    '=============================================='
    ''
    'This file contains the licenses and notices for third-party software'
    'components included in PDF Converter.'
    ''
    'This file is generated automatically during installer build.'
    'Do not edit manually.'
)

foreach ($package in $packages) {
    $metadata = Get-NuGetPackageMetadata -PackageId $package.id -PackageVersion $package.version
    $licenseText = Get-LicenseTextForPackage -Metadata $metadata
    $title = "$($metadata.Id) $($metadata.Version) (SPDX: $($metadata.License))"
    $sections += New-Section -Title $title -Body $licenseText
}

# PDFium のネイティブ DLL は NuGet パッケージにライセンスが含まれないため、
# pdfium-binaries のリリースから取得したライセンスを scripts\licenses\PDFium.txt に同梱している
$pdfiumPackage = $packages | Where-Object { $_.id -eq 'bblanchon.PDFium.Win32' } | Select-Object -First 1
if (-not $pdfiumPackage) {
    throw 'bblanchon.PDFium.Win32 パッケージが復元結果に見つかりません'
}

$pdfiumLicensePath = Join-Path $licensesFolder 'PDFium.txt'
if (-not (Test-Path $pdfiumLicensePath)) {
    throw "PDFium ライセンスが見つかりません: $pdfiumLicensePath"
}

$pdfiumLicense = Get-Content -LiteralPath $pdfiumLicensePath -Raw
if (-not $pdfiumLicense.StartsWith("PDFium $($pdfiumPackage.version) ")) {
    throw "scripts\licenses\PDFium.txt が PDFium $($pdfiumPackage.version) 向けではありません。pdfium-binaries の同じバージョンのリリースからライセンスを更新してください。"
}

$sections += New-Section -Title "PDFium $($pdfiumPackage.version) (native library, shipped via bblanchon.PDFium.Win32)" -Body $pdfiumLicense

# .NET ランタイム (self-contained で同梱) のライセンスと、ランタイム自身のサードパーティ通知
foreach ($runtimePack in $runtimePacks) {
    $packFolder = Get-PackageFolder -PackageId $runtimePack.id -PackageVersion $runtimePack.version
    $licenseFile = Get-ChildItem -LiteralPath $packFolder -File |
        Where-Object { $_.Name -match '^LICENSE(\.txt)?$' } |
        Select-Object -First 1
    if (-not $licenseFile) {
        throw "ランタイムパックのライセンスが見つかりません: $packFolder"
    }

    $title = "$($runtimePack.id) $($runtimePack.version) (.NET runtime, self-contained)"
    $sections += New-Section -Title $title -Body (Get-Content -LiteralPath $licenseFile.FullName -Raw)

    $noticesFile = Get-ChildItem -LiteralPath $packFolder -File |
        Where-Object { $_.Name -match '^THIRD-PARTY-NOTICES\.txt$' } |
        Select-Object -First 1
    if ($noticesFile) {
        $sections += New-Section -Title "$($runtimePack.id) third-party notices" -Body (Get-Content -LiteralPath $noticesFile.FullName -Raw)
    }
}

# 利用者向けドキュメント (docs\。mdBook の HTML) に含まれる素材は NuGet の依存ではないため、
# scripts\licenses\user-docs-notices.txt の表示を末尾に追記する (版の一致は build-user-docs.ps1 が検証する)
$userDocsNoticesPath = Join-Path $licensesFolder 'user-docs-notices.txt'
if (-not (Test-Path $userDocsNoticesPath)) {
    throw "利用者向けドキュメントの素材表示が見つかりません: $userDocsNoticesPath"
}

$sections += ''
$sections += (Get-Content -LiteralPath $userDocsNoticesPath -Raw -Encoding utf8).TrimEnd()

$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory -and -not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$content = ($sections -join [Environment]::NewLine).TrimEnd() + [Environment]::NewLine
[System.IO.File]::WriteAllText($OutputPath, $content, [System.Text.UTF8Encoding]::new($false))

Write-Host "THIRD-PARTY-NOTICES を生成しました: $OutputPath"
