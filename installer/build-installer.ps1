# PDF Converter インストーラーのビルドスクリプト
#
# 必要環境:
#   - .NET SDK 10 以降 (dotnet CLI)
#   - Inno Setup 6 — ISCC.exe
#   - mdBook (利用者向けドキュメントの生成。版は build-user-docs.ps1 の $UserDocsMdBookVersion に固定)
#
# アプリは .NET ランタイム同梱 (self-contained) で dotnet publish し、
# src\PdfConverter\bin\<Platform>\publish の内容をインストーラーへ格納します。
# 利用者向けドキュメント (docs/user) は mdBook で HTML 化し、publish 出力の docs\ に複製します。
#
# 環境変数 PDF_CONVERTER_UPDATE_SIGNING_KEY に署名用の秘密鍵 (PEM) のパスを設定している場合は、
# 作成したインストーラーごとに更新用の署名 (<ファイル名>.sig) も出力します (sign-update.ps1。PowerShell 7 以降が必要)。
#
# インストーラーは管理者権限を要求しないユーザー単位インストールです。
# インストール先: %LOCALAPPDATA%\Programs\PDF Converter
#
# 使用例:
#   .\build-installer.ps1                    # x64 のみ
#   .\build-installer.ps1 -Platform all        # x86 と x64 の両方
#   .\build-installer.ps1 -SkipBuild           # publish をスキップ (既存の publish 出力を使用)
#   .\build-installer.ps1 -NoPause             # 完了後のキー入力待ちを省略 (ターミナル向け)

[CmdletBinding()]
param(
    [ValidateSet('x64', 'x86', 'all')]
    [string] $Platform = 'x64',

    [switch] $SkipBuild,

    [switch] $NoPause
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'build-common.ps1')
. (Join-Path $PSScriptRoot 'build-user-docs.ps1')

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectPath = Join-Path $repoRoot 'src\PdfConverter\PdfConverter.csproj'
$issPath = Join-Path $PSScriptRoot 'PdfConverter.iss'
$distDir = Join-Path $repoRoot 'dist'
$assemblyInfoPath = Join-Path $repoRoot 'src\PdfConverter\Properties\AssemblyInfo.cs'

function Find-ISCC {
    $programFilesX86 = ${env:ProgramFiles(x86)}
    $programFiles = $env:ProgramFiles
    $localAppData = $env:LocalAppData

    $candidates = @(
        (Join-Path $programFilesX86 'Inno Setup 6\ISCC.exe'),
        (Join-Path $programFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $localAppData 'Programs\Inno Setup 6\ISCC.exe')
    )

    foreach ($path in $candidates) {
        if (Test-Path $path) { return $path }
    }

    $command = Get-Command iscc -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    throw "Inno Setup 6 (ISCC.exe) が見つかりません。winget install JRSoftware.InnoSetup でインストールできます。"
}

function Get-AppVersion {
    $content = Get-Content -LiteralPath $assemblyInfoPath -Raw
    if ($content -match 'AssemblyInformationalVersion\("([^"]+)"\)') {
        return $Matches[1]
    }

    throw "AssemblyInfo.cs からバージョンを取得できませんでした: $assemblyInfoPath"
}

function Get-PublishDir {
    param(
        [string] $TargetPlatform
    )

    return Join-Path $repoRoot "src\PdfConverter\bin\$TargetPlatform\publish"
}

function Publish-App {
    param(
        [string] $TargetPlatform
    )

    $dotnet = Find-DotNet
    $publishDir = Get-PublishDir -TargetPlatform $TargetPlatform

    # 前回の publish 出力が混ざらないよう作り直す
    if (Test-Path -LiteralPath $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }

    Write-Host "アプリを publish しています (Release / $TargetPlatform)..."
    & $dotnet publish $projectPath -c Release "-p:Platform=$TargetPlatform" -o $publishDir -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "publish に失敗しました (Platform=$TargetPlatform)"
    }
}

function Write-ThirdPartyNotices {
    param(
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
        [string] $TargetPlatform,
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

function Build-Installer {
    param(
        [string] $TargetPlatform,
        [string] $AppVersion,
        [string] $UserDocsDir
    )

    $sourceDir = Get-PublishDir -TargetPlatform $TargetPlatform
    if (-not (Test-Path (Join-Path $sourceDir 'PDF Converter.exe'))) {
        throw "publish 出力が見つかりません: $sourceDir`n先に publish を実行するか、-SkipBuild を外してください。"
    }

    Write-ThirdPartyNotices -TargetPlatform $TargetPlatform
    Copy-UserDocs -TargetPlatform $TargetPlatform -UserDocsDir $UserDocsDir

    $iscc = Find-ISCC
    Write-Host "Inno Setup: $iscc"
    Write-Host "インストーラーを作成しています ($TargetPlatform)..."

    & $iscc `
        "/DAppVersion=$AppVersion" `
        "/DPlatform=$TargetPlatform" `
        "/DSourceDir=$sourceDir" `
        $issPath

    if ($LASTEXITCODE -ne 0) {
        throw "インストーラーの作成に失敗しました (Platform=$TargetPlatform)"
    }
}

# 作成したインストーラーに更新用の署名 (.sig) を付ける。署名用の秘密鍵が無ければ警告だけ出して省く
function Invoke-UpdateSigning {
    param(
        [string] $AppVersion
    )

    if ([string]::IsNullOrWhiteSpace($env:PDF_CONVERTER_UPDATE_SIGNING_KEY)) {
        Write-Warning '環境変数 PDF_CONVERTER_UPDATE_SIGNING_KEY が未設定のため、署名 (.sig) を作成しません。署名の無いインストーラーは、公開しても更新を適用できません。'
        return
    }

    # 署名は PowerShell 7 以降の .NET API を使うため、実行中のシェルに依らず pwsh で行う
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if (-not $pwsh) {
        throw '署名には PowerShell 7 以降 (pwsh) が必要です。'
    }

    Write-Host 'インストーラーに署名しています...'
    $global:LASTEXITCODE = 0
    & $pwsh.Source -NoProfile -File (Join-Path $PSScriptRoot 'sign-update.ps1') -Version $AppVersion -NoPause
    if ($LASTEXITCODE -ne 0) {
        throw "署名に失敗しました (終了コード: $LASTEXITCODE)"
    }
}

$shouldPauseOnExit = -not $NoPause -and (Test-DoubleClickLaunch)
$exitCode = 0

try {
    $platformsToBuild = switch ($Platform) {
        'all' { @('x86', 'x64') }
        default { @($Platform) }
    }

    $appVersion = Get-AppVersion
    Write-Host "バージョン: $appVersion"

    New-Item -ItemType Directory -Path $distDir -Force | Out-Null

    # 利用者向けドキュメントはプラットフォームに依らないため 1 回だけ生成する
    $userDocsDir = Invoke-UserDocsBuild -RepoRoot $repoRoot

    # project.assets.json は最後に復元したプラットフォームの内容になるため、
    # publish → 通知生成 → インストーラー作成をプラットフォームごとに続けて行う
    foreach ($plat in $platformsToBuild) {
        if ($SkipBuild) {
            Restore-Packages -Path $projectPath -Platform $plat
        }
        else {
            Publish-App -TargetPlatform $plat
        }

        Build-Installer -TargetPlatform $plat -AppVersion $appVersion -UserDocsDir $userDocsDir
    }

    # dist\ にある同じ版のインストーラーすべてが対象 (今回作らなかったプラットフォームの分も含む)
    Invoke-UpdateSigning -AppVersion $appVersion

    Write-Host ''
    Write-Host '完了。出力先:'
    Get-ChildItem -LiteralPath $distDir -Filter "PdfConverter-$appVersion-*-setup.exe*" | ForEach-Object {
        Write-Host "  $($_.FullName)"
    }
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
