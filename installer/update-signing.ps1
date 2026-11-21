# 更新インストーラーの署名 (ECDSA P-256 / SHA-256) に使う共通関数
#
# sign-update.ps1 / new-update-signing-key.ps1 / release-version.ps1 から dot-source して使う。
# 単体では実行しない。
#
# 署名対象のメッセージは、アプリ側の UpdateSignatureVerifier.BuildMessage と同じ形式にそろえる。
#   PdfConverter-Update-v1 \n <タグ> \n <アセット名> \n <サイズ> \n <SHA-256 (小文字)>
#
# ECDsa の PEM 読み込み・IEEE P1363 形式の署名には .NET の API が必要なため、PowerShell 7 以降で動かす。

$script:UpdateSigningMessageDomain = 'PdfConverter-Update-v1'
$script:UpdateSigningKeyEnvName = 'PDF_CONVERTER_UPDATE_SIGNING_KEY'
$script:UpdateSigningKeySourceRelativePath = 'src\PdfConverter\Infrastructure\UpdateSigningKey.cs'

# PowerShell 7 以降で実行しているか検証する
function Assert-UpdateSigningPowerShell {
    if ($PSVersionTable.PSVersion.Major -lt 7) {
        throw "更新の署名には PowerShell 7 以降が必要です (現在: $($PSVersionTable.PSVersion))。pwsh で実行してください。"
    }
}

# 署名対象のメッセージ (UTF-8) を組み立てる
function Get-UpdateSignedMessage {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Tag,

        [Parameter(Mandatory = $true)]
        [string] $AssetName,

        [Parameter(Mandatory = $true)]
        [long] $Size,

        [Parameter(Mandatory = $true)]
        [string] $Sha256
    )

    $text = @(
        $script:UpdateSigningMessageDomain,
        $Tag,
        $AssetName,
        $Size.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        $Sha256.ToLowerInvariant()
    ) -join "`n"

    return [System.Text.UTF8Encoding]::new($false).GetBytes($text)
}

# 秘密鍵 (PKCS#8 の PEM) を読み込む
function Import-UpdateSigningKey {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "署名用の秘密鍵が見つかりません: $Path"
    }

    $ecdsa = [System.Security.Cryptography.ECDsa]::Create()
    try {
        $ecdsa.ImportFromPem((Get-Content -LiteralPath $Path -Raw))
    }
    catch {
        $ecdsa.Dispose()
        throw "署名用の秘密鍵を読み込めません (PKCS#8 の PEM が必要です): $Path"
    }

    if ($ecdsa.KeySize -ne 256) {
        $ecdsa.Dispose()
        throw "署名用の秘密鍵が P-256 ではありません: $Path"
    }

    return $ecdsa
}

# 公開鍵 (SubjectPublicKeyInfo の Base64) を返す
function Get-UpdateSigningPublicKey {
    param(
        [Parameter(Mandatory = $true)]
        [System.Security.Cryptography.ECDsa] $Key
    )

    return [Convert]::ToBase64String($Key.ExportSubjectPublicKeyInfo())
}

# アプリに埋め込まれた公開鍵 (UpdateSigningKey.cs の PublicKey) を返す。未設定なら空文字
function Get-EmbeddedUpdateSigningPublicKey {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot
    )

    $sourcePath = Join-Path $RepoRoot $script:UpdateSigningKeySourceRelativePath
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "公開鍵のソースが見つかりません: $sourcePath"
    }

    $content = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
    if ($content -notmatch 'const string PublicKey = "([^"]*)";') {
        throw "UpdateSigningKey.cs から PublicKey を読み取れません: $sourcePath"
    }

    return $Matches[1]
}

# 秘密鍵の環境変数が設定され、アプリに埋め込まれた公開鍵と対応していることを検証する
function Assert-UpdateSigningKeyConfigured {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot,

        [string] $KeyPath
    )

    Assert-UpdateSigningPowerShell

    if (-not $KeyPath) {
        $KeyPath = [Environment]::GetEnvironmentVariable($script:UpdateSigningKeyEnvName)
    }

    if ([string]::IsNullOrWhiteSpace($KeyPath)) {
        throw @"
更新の署名用の秘密鍵が指定されていません。
環境変数 $($script:UpdateSigningKeyEnvName) に秘密鍵 (PEM) のパスを設定してください。
鍵が無い場合は installer\new-update-signing-key.ps1 で作成します。
(署名の無いインストーラーは、公開後に更新を適用できません)
"@
    }

    $embedded = Get-EmbeddedUpdateSigningPublicKey -RepoRoot $RepoRoot
    if ([string]::IsNullOrEmpty($embedded)) {
        throw @"
アプリに署名検証用の公開鍵が埋め込まれていません。
installer\new-update-signing-key.ps1 が出力する公開鍵を $($script:UpdateSigningKeySourceRelativePath) の PublicKey に設定してください。
"@
    }

    $key = Import-UpdateSigningKey -Path $KeyPath
    try {
        if ((Get-UpdateSigningPublicKey -Key $key) -ne $embedded) {
            throw "秘密鍵が、アプリに埋め込まれた公開鍵と対応していません。この鍵で署名したインストーラーは、更新時に拒否されます。"
        }
    }
    finally {
        $key.Dispose()
    }
}

# ファイルのメッセージに署名し、Base64 (IEEE P1363 形式) を返す
function New-UpdateSignature {
    param(
        [Parameter(Mandatory = $true)]
        [System.Security.Cryptography.ECDsa] $Key,

        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string] $Tag
    )

    $file = Get-Item -LiteralPath $FilePath
    $sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $message = Get-UpdateSignedMessage -Tag $Tag -AssetName $file.Name -Size $file.Length -Sha256 $sha256

    $signature = $Key.SignData(
        $message,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.DSASignatureFormat]::IeeeP1363FixedFieldConcatenation)

    return [Convert]::ToBase64String($signature)
}
