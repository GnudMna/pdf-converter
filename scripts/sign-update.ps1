# 更新用の配布物 (インストーラー・ポータブル版の zip) に署名するスクリプト
#
# 配布物 (PdfConverter-<版>-<Platform>-setup.exe / -portable.zip) ごとに、隣へ <ファイル名>.sig を出力します。
# 署名はリリース時にオフラインで作り、GitHub Release へ配布物と一緒にアップロードします。
# アプリは、この署名を埋め込み済みの公開鍵で検証できた配布物だけを更新に使います。
#
# 必要環境:
#   - PowerShell 7 以降
#   - 署名用の秘密鍵 (new-update-signing-key.ps1 で作成)。環境変数 PDF_CONVERTER_UPDATE_SIGNING_KEY か -KeyPath で指定する
#
# 使用例:
#   .\sign-update.ps1 -Version 0.7.0                       # dist\PdfConverter-0.7.0-*-setup.exe / -portable.zip をすべて署名
#   .\sign-update.ps1 -Version 0.7.0 -KeyPath C:\keys\update-signing.pem
#
# オプション:
#   -Version x.y.z   署名する配布物の版 (リリースタグは v<版>)
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

    $packages = @(
        foreach ($filter in @("PdfConverter-$Version-*-setup.exe", "PdfConverter-$Version-*-portable.zip")) {
            Get-ChildItem -LiteralPath $distDir -Filter $filter -File -ErrorAction SilentlyContinue
        }
    )
    if ($packages.Count -eq 0) {
        throw "署名する配布物が見つかりません: $distDir\PdfConverter-$Version-*-setup.exe / -portable.zip"
    }

    $tag = "v$Version"
    $key = Import-UpdateSigningKey -Path $KeyPath
    try {
        foreach ($package in $packages) {
            $signaturePath = "$($package.FullName).sig"
            $signature = New-UpdateSignature -Key $key -FilePath $package.FullName -Tag $tag
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
