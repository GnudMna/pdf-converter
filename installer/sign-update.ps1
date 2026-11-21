# 更新インストーラーに署名するスクリプト
#
# インストーラー (PdfConverter-<版>-<Platform>-setup.exe) ごとに、隣へ <ファイル名>.sig を出力します。
# 署名はリリース時にオフラインで作り、GitHub Release へインストーラーと一緒にアップロードします。
# アプリは、この署名を埋め込み済みの公開鍵で検証できたインストーラーだけを起動します。
#
# 必要環境:
#   - PowerShell 7 以降
#   - 署名用の秘密鍵 (new-update-signing-key.ps1 で作成)。環境変数 PDF_CONVERTER_UPDATE_SIGNING_KEY か -KeyPath で指定する
#
# 使用例:
#   .\sign-update.ps1 -Version 0.7.0                       # dist\PdfConverter-0.7.0-*-setup.exe をすべて署名
#   .\sign-update.ps1 -Version 0.7.0 -KeyPath C:\keys\update-signing.pem
#
# オプション:
#   -Version x.y.z   署名するインストーラーの版 (リリースタグは v<版>)
#   -KeyPath         秘密鍵 (PEM) のパス (既定: 環境変数 PDF_CONVERTER_UPDATE_SIGNING_KEY)
#   -NoPause         完了後のキー入力待ちを省略 (ターミナル向け)

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $KeyPath,

    [switch] $NoPause
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'build-common.ps1')
. (Join-Path $PSScriptRoot 'update-signing.ps1')

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$distDir = Join-Path $repoRoot 'dist'

$shouldPauseOnExit = -not $NoPause -and (Test-DoubleClickLaunch)
$exitCode = 0

try {
    if (-not $KeyPath) {
        $KeyPath = [Environment]::GetEnvironmentVariable($script:UpdateSigningKeyEnvName)
    }

    # 埋め込み済みの公開鍵と対応しない鍵で署名すると、配布後に更新が拒否される
    Assert-UpdateSigningKeyConfigured -RepoRoot $repoRoot -KeyPath $KeyPath

    $installers = @(Get-ChildItem -LiteralPath $distDir -Filter "PdfConverter-$Version-*-setup.exe" -File -ErrorAction SilentlyContinue)
    if ($installers.Count -eq 0) {
        throw "署名するインストーラーが見つかりません: $distDir\PdfConverter-$Version-*-setup.exe"
    }

    $tag = "v$Version"
    $key = Import-UpdateSigningKey -Path $KeyPath
    try {
        foreach ($installer in $installers) {
            $signaturePath = "$($installer.FullName).sig"
            $signature = New-UpdateSignature -Key $key -FilePath $installer.FullName -Tag $tag
            [System.IO.File]::WriteAllText($signaturePath, $signature, [System.Text.UTF8Encoding]::new($false))
            Write-Host "署名しました: $signaturePath"
        }
    }
    finally {
        $key.Dispose()
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
