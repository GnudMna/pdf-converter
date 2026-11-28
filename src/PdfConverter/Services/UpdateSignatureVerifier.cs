using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PdfConverter.Services
{
    /// <summary>
    /// 更新インストーラーのオフライン署名 (ECDSA P-256 / SHA-256) を検証する
    /// </summary>
    /// <remarks>
    /// 署名対象はインストーラー本体ではなく、版・アセット名・サイズ・SHA-256 をまとめた文字列 (<see cref="BuildMessage"/>)<br/>
    /// 更新サーバーが返す値とは独立に、リリース時の秘密鍵で署名することで、サーバー侵害時の差し替えと
    /// 古い署名済みインストーラーへの巻き戻しを防ぐ<br/>
    /// 署名は <c>scripts/sign-update.ps1</c> が作る (メッセージの組み立ては <c>scripts/update-signing.ps1</c> と一致させる)
    /// </remarks>
    public static class UpdateSignatureVerifier
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>署名対象メッセージの先頭に付ける識別子 (別用途の署名との取り違えを防ぐ)</summary>
        public const string MessageDomain = "PdfConverter-Update-v1";

        /// <summary>署名 (IEEE P1363 形式: r と s を各 32 バイトで連結) のバイト数</summary>
        public const int SignatureLength = 64;

        /// <summary>署名ファイルの上限バイト数 (Base64 の 64 バイトは 88 文字)</summary>
        public const int MaxSignatureFileBytes = 1024;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 署名対象のメッセージ (UTF-8) を組み立てる
        /// </summary>
        /// <param name="tag">リリースタグ</param>
        /// <param name="assetName">インストーラーのアセット名</param>
        /// <param name="size">インストーラーのバイト数</param>
        /// <param name="sha256">インストーラーの SHA-256 (小文字 64 桁)</param>
        /// <returns>改行区切りの UTF-8 バイト列</returns>
        public static byte[] BuildMessage(string tag, string assetName, long size, string sha256)
        {
            string message = string.Join(
                "\n",
                MessageDomain,
                tag,
                assetName,
                size.ToString(CultureInfo.InvariantCulture),
                sha256);
            return Encoding.UTF8.GetBytes(message);
        }

        /// <summary>
        /// インストーラーの署名を、埋め込み済みの公開鍵で検証する
        /// </summary>
        /// <param name="publicKeyBase64">SubjectPublicKeyInfo (DER) の Base64</param>
        /// <param name="tag">リリースタグ</param>
        /// <param name="assetName">インストーラーのアセット名</param>
        /// <param name="size">インストーラーのバイト数</param>
        /// <param name="sha256">ダウンロードしたファイルから計算した SHA-256 (小文字 64 桁)</param>
        /// <param name="signatureBase64">署名ファイルの内容 (IEEE P1363 形式の Base64)</param>
        /// <param name="errorMessage">失敗時のメッセージ</param>
        /// <returns>署名が正しければ <c>true</c></returns>
        public static bool TryVerify(
            string publicKeyBase64,
            string tag,
            string assetName,
            long size,
            string sha256,
            string signatureBase64,
            [NotNullWhen(false)] out string? errorMessage)
        {
            errorMessage = null;

            byte[] signature;
            try
            {
                signature = Convert.FromBase64String(signatureBase64.Trim());
            }
            catch (FormatException)
            {
                errorMessage = "署名ファイルの形式が不正です。";
                return false;
            }

            if (signature.Length != SignatureLength)
            {
                errorMessage = "署名ファイルの形式が不正です。";
                return false;
            }

            bool valid;
            try
            {
                using (ECDsa ecdsa = ECDsa.Create())
                {
                    ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
                    valid = ecdsa.VerifyData(
                        BuildMessage(tag, assetName, size, sha256),
                        signature,
                        HashAlgorithmName.SHA256,
                        DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
                }
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                errorMessage = "更新の署名検証用公開鍵が不正です。";
                return false;
            }

            if (!valid)
            {
                errorMessage = "インストーラーの署名が一致しません。";
                return false;
            }

            return true;
        }
    }
}
