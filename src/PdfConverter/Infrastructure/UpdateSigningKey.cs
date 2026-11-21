namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// 更新インストーラーの署名検証に使う公開鍵 (対の秘密鍵はリポジトリに含めない)
    /// </summary>
    /// <remarks>
    /// 値は <c>installer/new-update-signing-key.ps1</c> が出力する SubjectPublicKeyInfo (DER) の Base64<br/>
    /// 空のままだと、更新の適用は署名を検証できないため失敗する<br/>
    /// <c>installer/sign-update.ps1</c> がこの値を読み取って秘密鍵との対応を確認するため、定数の書式を変えない
    /// </remarks>
    internal static class UpdateSigningKey
    {
        /// <summary>
        /// ECDSA P-256 公開鍵 (SubjectPublicKeyInfo の Base64)
        /// </summary>
        internal const string PublicKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEs96lWXNU53unWKISssIDi4Tm4KmSC42kgFrj3RycmS0Z10QOrqbjFvs3nyQYaO2HfRI9QQ7Ih1uaXcBFCu+OtQ==";
    }
}
