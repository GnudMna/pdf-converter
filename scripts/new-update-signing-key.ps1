# 更新インストーラーの署名用キーペア (ECDSA P-256) を作成するスクリプト
#
# 秘密鍵を PKCS#8 の PEM としてファイルへ書き出し、アプリに埋め込む公開鍵を表示します。
# 秘密鍵はリポジトリへ含めず、リリース作業をする端末の外へバックアップしてください。
# 紛失すると、すでに配布した版からの更新を署名できなくなります。
#
# 必要環境:
#   - PowerShell 7 以降
#
# 使用例:
#   .\new-update-signing-key.ps1 -OutputPath "$env:USERPROFILE\.pdf-converter\update-signing.pem"
#
# 作成後:
#   1. 表示された公開鍵を src\PdfConverter\Infrastructure\UpdateSigningKey.cs の PublicKey に設定する
#   2. 環境変数 PDF_CONVERTER_UPDATE_SIGNING_KEY に秘密鍵のパスを設定する

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [switch] $NoPause
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'build-common.ps1')
. (Join-Path $PSScriptRoot 'update-signing.ps1')

$shouldPauseOnExit = -not $NoPause -and (Test-DoubleClickLaunch)
$exitCode = 0

try {
    Assert-UpdateSigningPowerShell

    if (Test-Path -LiteralPath $OutputPath) {
        throw "出力先に既にファイルがあります。上書きしません: $OutputPath"
    }

    $directory = Split-Path -Parent ([System.IO.Path]::GetFullPath($OutputPath))
    New-Item -ItemType Directory -Path $directory -Force | Out-Null

    $key = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve]::CreateFromFriendlyName('nistP256'))
    try {
        $body = [Convert]::ToBase64String($key.ExportPkcs8PrivateKey(), [System.Base64FormattingOptions]::InsertLineBreaks)
        $pem = "-----BEGIN PRIVATE KEY-----`n$body`n-----END PRIVATE KEY-----`n"
        [System.IO.File]::WriteAllText($OutputPath, $pem, [System.Text.UTF8Encoding]::new($false))

        $publicKey = Get-UpdateSigningPublicKey -Key $key
    }
    finally {
        $key.Dispose()
    }

    # 秘密鍵は現在のユーザーだけが読めるようにする (失敗しても続行)
    & icacls $OutputPath /inheritance:r /grant:r "$($env:USERNAME):(R,W)" | Out-Null

    Write-Host "秘密鍵を作成しました: $OutputPath"
    Write-Host ''
    Write-Host '公開鍵 (src\PdfConverter\Infrastructure\UpdateSigningKey.cs の PublicKey に設定):'
    Write-Host "  $publicKey"
    Write-Host ''
    Write-Host '次の作業:'
    Write-Host "  - 環境変数 $($script:UpdateSigningKeyEnvName) に秘密鍵のパスを設定する"
    Write-Host '  - 秘密鍵を端末の外へバックアップする (Git には含めない)'
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
