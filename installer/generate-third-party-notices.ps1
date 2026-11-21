# PDF Converter — THIRD-PARTY-NOTICES.txt 生成スクリプト
#
# packages.config と NuGet パッケージのメタデータから、
# サードパーティライセンス一覧を生成します。
#
# 使用例:
#   .\generate-third-party-notices.ps1 -OutputPath "..\src\PdfConverter\bin\x64\Release\THIRD-PARTY-NOTICES.txt"

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [ValidateSet('x64', 'x86')]
    [string] $Platform = 'x64'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$packagesConfigPath = Join-Path $repoRoot 'src\PdfConverter\packages.config'
$packagesDir = Join-Path $repoRoot 'src\packages'

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

function Get-NuGetPackageMetadata {
    param(
        [string] $PackageId,
        [string] $PackageVersion
    )

    $packageFolder = Join-Path $packagesDir "$PackageId.$PackageVersion"
    if (-not (Test-Path $packageFolder)) {
        throw "NuGet パッケージが見つかりません: $PackageId.$PackageVersion ($packageFolder)"
    }

    $nupkg = Get-ChildItem -LiteralPath $packageFolder -Filter '*.nupkg' | Select-Object -First 1
    if (-not $nupkg) {
        throw "nupkg が見つかりません: $packageFolder"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($nupkg.FullName)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -match '\.nuspec$' } | Select-Object -First 1
        if (-not $entry) {
            throw "nuspec が見つかりません: $($nupkg.FullName)"
        }

        $stream = $entry.Open()
        try {
            $content = (New-Object System.IO.StreamReader($stream)).ReadToEnd()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $zip.Dispose()
    }

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

if (-not (Test-Path $packagesConfigPath)) {
    throw "packages.config が見つかりません: $packagesConfigPath"
}

[xml] $packagesConfig = Get-Content -LiteralPath $packagesConfigPath
$packages = $packagesConfig.packages.package | Sort-Object { $_.id }

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

$pdfiumLicensePath = Join-Path $packagesDir "Docnet.Core.2.6.0\runtimes\win-$Platform\native\LICENSE"
if (-not (Test-Path $pdfiumLicensePath)) {
    throw "PDFium ライセンスが見つかりません: $pdfiumLicensePath"
}

$pdfiumLicense = Get-Content -LiteralPath $pdfiumLicensePath -Raw
$sections += New-Section -Title 'PDFium (native library, shipped via Docnet.Core)' -Body $pdfiumLicense

$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory -and -not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$content = ($sections -join [Environment]::NewLine).TrimEnd() + [Environment]::NewLine
[System.IO.File]::WriteAllText($OutputPath, $content, [System.Text.UTF8Encoding]::new($false))

Write-Host "THIRD-PARTY-NOTICES を生成しました: $OutputPath"
