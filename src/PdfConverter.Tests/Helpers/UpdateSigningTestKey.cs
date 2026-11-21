using System;
using System.Security.Cryptography;
using PdfConverter.Services;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// 更新の署名検証テスト用に、使い捨ての ECDSA P-256 鍵で署名する
    /// </summary>
    internal sealed class UpdateSigningTestKey : IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>公開鍵 (SubjectPublicKeyInfo の Base64)</summary>
        public string PublicKey => Convert.ToBase64String(_ecdsa.ExportSubjectPublicKeyInfo());


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 更新情報に対する署名 (IEEE P1363 形式の Base64) を作る
        /// </summary>
        public string Sign(string tag, string assetName, long size, string sha256) =>
            Convert.ToBase64String(_ecdsa.SignData(
                UpdateSignatureVerifier.BuildMessage(tag, assetName, size, sha256),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

        /// <inheritdoc/>
        public void Dispose()
        {
            _ecdsa.Dispose();
        }
    }
}
