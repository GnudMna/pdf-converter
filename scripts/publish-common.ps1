# PDF Converter — 配布物 (インストーラー・ポータブル版) の作成に使う共通関数
#
# build-installer.ps1 / build-portable.ps1 からドットソースして使用します。単体では実行しません。
# build-common.ps1 を先にドットソースしておく必要があります。
#   . (Join-Path $PSScriptRoot 'build-common.ps1')
#   . (Join-Path $PSScriptRoot 'publish-common.ps1')
#
# 配布物はどちらも、dotnet publish (Release・self-contained) の出力 src\PdfConverter\bin\<Platform>\publish に
# THIRD-PARTY-NOTICES.txt と利用者向けドキュメント (docs\) を加えたものから作ります。

$script:PublishRepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$script:PublishProjectPath = Join-Path $script:PublishRepoRoot 'src\PdfConverter\PdfConverter.csproj'
$script:PublishAssemblyInfoPath = Join-Path $script:PublishRepoRoot 'src\PdfConverter\Properties\AssemblyInfo.cs'
$script:PublishDistDir = Join-Path $script:PublishRepoRoot 'dist'

# AssemblyInfo.cs の AssemblyInformationalVersion を返す
function Get-AppVersion {
    $content = Get-Content -LiteralPath $script:PublishAssemblyInfoPath -Raw
    if ($content -match 'AssemblyInformationalVersion\("([^"]+)"\)') {
        return $Matches[1]
    }

    throw "AssemblyInfo.cs からバージョンを取得できませんでした: $($script:PublishAssemblyInfoPath)"
}

# -Platform の値 (x64 / x86 / all) を、作成するプラットフォームの一覧に展開する
function Get-TargetPlatforms {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Platform
    )

    switch ($Platform) {
        'all' { return @('x86', 'x64') }
        default { return @($Platform) }
    }
}

function Get-PublishDir {
    param(
        [Parameter(Mandatory = $true)]
        [string] $TargetPlatform
    )

    return Join-Path $script:PublishRepoRoot "src\PdfConverter\bin\$TargetPlatform\publish"
}

function Publish-App {
    param(
        [Parameter(Mandatory = $true)]
        [string] $TargetPlatform
    )

    $dotnet = Find-DotNet
    $publishDir = Get-PublishDir -TargetPlatform $TargetPlatform

    # 前回の publish 出力が混ざらないよう作り直す
    if (Test-Path -LiteralPath $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }

    Write-Host "アプリを publish しています (Release / $TargetPlatform)..."
    & $dotnet publish $script:PublishProjectPath -c Release "-p:Platform=$TargetPlatform" -o $publishDir -v:minimal -p:RestoreLockedMode=true
    if ($LASTEXITCODE -ne 0) {
        throw "publish に失敗しました (Platform=$TargetPlatform)"
    }
}

# publish する。-SkipBuild のときは publish せず、通知の生成に使う project.assets.json を復元だけする
function Invoke-PublishOrRestore {
    param(
        [Parameter(Mandatory = $true)]
        [string] $TargetPlatform,

        [switch] $SkipBuild
    )

    if ($SkipBuild) {
        Restore-Packages -Path $script:PublishProjectPath -Platform $TargetPlatform
    }
    else {
        Publish-App -TargetPlatform $TargetPlatform
    }
}

function Write-ThirdPartyNotices {
    param(
        [Parameter(Mandatory = $true)]
        [string] $TargetPlatform
    )

    $sourceDir = Get-PublishDir -TargetPlatform $TargetPlatform
    $noticesPath = Join-Path $sourceDir 'THIRD-PARTY-NOTICES.txt'
    $generateScript = Join-Path $PSScriptRoot 'generate-third-party-notices.ps1'

    Write-Host "THIRD-PARTY-NOTICES を生成しています ($TargetPlatform)..."
    & $generateScript -OutputPath $noticesPath -Platform $TargetPlatform
}

function Copy-UserDocs {
    param(
        [Parameter(Mandatory = $true)]
        [string] $TargetPlatform,

        [Parameter(Mandatory = $true)]
        [string] $UserDocsDir
    )

    $docsDir = Join-Path (Get-PublishDir -TargetPlatform $TargetPlatform) 'docs'

    # -SkipBuild で前回の publish 出力を使う場合に、旧版のファイルが混ざらないよう作り直す
    if (Test-Path -LiteralPath $docsDir) {
        Remove-Item -LiteralPath $docsDir -Recurse -Force
    }

    Write-Host "利用者向けドキュメントを複製しています ($TargetPlatform)..."
    Copy-Item -LiteralPath $UserDocsDir -Destination $docsDir -Recurse -Force
    Assert-UserDocsPages -DocsDir $docsDir
}

# publish 出力があることを確かめ、THIRD-PARTY-NOTICES.txt と利用者向けドキュメントを加える
function Complete-PublishOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string] $TargetPlatform,

        [Parameter(Mandatory = $true)]
        [string] $UserDocsDir
    )

    $sourceDir = Get-PublishDir -TargetPlatform $TargetPlatform
    if (-not (Test-Path (Join-Path $sourceDir 'PDF Converter.exe'))) {
        throw "publish 出力が見つかりません: $sourceDir`n先に publish を実行するか、-SkipBuild を外してください。"
    }

    Write-ThirdPartyNotices -TargetPlatform $TargetPlatform
    Copy-UserDocs -TargetPlatform $TargetPlatform -UserDocsDir $UserDocsDir
}

# dist\ の配布物に更新用の署名 (.sig) を付ける。署名用の秘密鍵が無ければ警告だけ出して省く
function Invoke-UpdateSigning {
    param(
        [Parameter(Mandatory = $true)]
        [string] $AppVersion
    )

    if ([string]::IsNullOrWhiteSpace($env:PDF_CONVERTER_UPDATE_SIGNING_KEY)) {
        Write-Warning '環境変数 PDF_CONVERTER_UPDATE_SIGNING_KEY が未設定のため、署名 (.sig) を作成しません。署名の無い配布物は、公開しても更新を適用できません。'
        return
    }

    # 署名は PowerShell 7 以降の .NET API を使うため、実行中のシェルに依らず pwsh で行う
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if (-not $pwsh) {
        throw '署名には PowerShell 7 以降 (pwsh) が必要です。'
    }

    Write-Host '配布物に署名しています...'
    $global:LASTEXITCODE = 0
    & $pwsh.Source -NoProfile -File (Join-Path $PSScriptRoot 'sign-update.ps1') -Version $AppVersion -NoPause
    if ($LASTEXITCODE -ne 0) {
        throw "署名に失敗しました (終了コード: $LASTEXITCODE)"
    }
}
