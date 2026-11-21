using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="UpdateSignatureVerifier"/> の署名検証を検証する
    /// </summary>
    public sealed class UpdateSignatureVerifierTests
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        private const string Tag = "v0.5.0";
        private const string AssetName = "PdfConverter-0.5.0-x64-setup.exe";
        private const long Size = 12345;
        private const string Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        // installer/update-signing.ps1 で生成した使い捨て鍵と署名 (ファイルは 18 バイトの "vector-setup-bytes")
        private const string ScriptPublicKey =
            "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEKQ48fv8HXsZj4YTAKj6fnlmpnZNhG3TCsUu7S+cNkQf56p5VNYLDq5Ek2qdvDPrEMfjdLXoMrNfeP35FwXlc7Q==";
        private const string ScriptSignature =
            "WCTeMAsnDEhmNI1Bx3p3XmLYOYB2dNI7o2NYxcmCkyKzJAQxoIqldCcLa9nZdvEHte7apW9oxJYo/E42f4QBGg==";
        private const string ScriptSha256 =
            "202c8e49e8ae3c819f65d408314b9b2010f1e6497a372346073583e3416a9a60";


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 正しい鍵で署名した内容は検証に成功することを検証する
        /// </summary>
        [Fact]
        public void TryVerify_ValidSignature_ReturnsTrue()
        {
            using (var key = new UpdateSigningTestKey())
            {
                string signature = key.Sign(Tag, AssetName, Size, Sha256);

                bool result = UpdateSignatureVerifier.TryVerify(
                    key.PublicKey, Tag, AssetName, Size, Sha256, signature, out string errorMessage);

                Assert.True(result);
                Assert.Null(errorMessage);
            }
        }

        /// <summary>
        /// 署名ファイル末尾の改行は無視して検証することを検証する
        /// </summary>
        [Fact]
        public void TryVerify_SignatureWithTrailingNewline_ReturnsTrue()
        {
            using (var key = new UpdateSigningTestKey())
            {
                string signature = key.Sign(Tag, AssetName, Size, Sha256) + "\r\n";

                bool result = UpdateSignatureVerifier.TryVerify(
                    key.PublicKey, Tag, AssetName, Size, Sha256, signature, out _);

                Assert.True(result);
            }
        }

        /// <summary>
        /// 署名後に版・アセット名・サイズ・ハッシュのいずれかが変わると検証に失敗することを検証する
        /// </summary>
        [Theory]
        [InlineData("v0.4.0", AssetName, Size, Sha256)]
        [InlineData(Tag, "PdfConverter-0.5.0-x86-setup.exe", Size, Sha256)]
        [InlineData(Tag, AssetName, Size + 1, Sha256)]
        [InlineData(Tag, AssetName, Size, "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210")]
        public void TryVerify_ChangedField_ReturnsFalse(string tag, string assetName, long size, string sha256)
        {
            using (var key = new UpdateSigningTestKey())
            {
                string signature = key.Sign(Tag, AssetName, Size, Sha256);

                bool result = UpdateSignatureVerifier.TryVerify(
                    key.PublicKey, tag, assetName, size, sha256, signature, out string errorMessage);

                Assert.False(result);
                Assert.Contains("署名が一致しません", errorMessage);
            }
        }

        /// <summary>
        /// 別の鍵で署名した内容は検証に失敗することを検証する
        /// </summary>
        [Fact]
        public void TryVerify_SignedByOtherKey_ReturnsFalse()
        {
            using (var trusted = new UpdateSigningTestKey())
            using (var attacker = new UpdateSigningTestKey())
            {
                string signature = attacker.Sign(Tag, AssetName, Size, Sha256);

                bool result = UpdateSignatureVerifier.TryVerify(
                    trusted.PublicKey, Tag, AssetName, Size, Sha256, signature, out string errorMessage);

                Assert.False(result);
                Assert.Contains("署名が一致しません", errorMessage);
            }
        }

        /// <summary>
        /// 署名ファイルが Base64 でない、または長さが違う場合は検証に失敗することを検証する
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("not base64!")]
        [InlineData("AAAA")]
        public void TryVerify_MalformedSignature_ReturnsFalse(string signature)
        {
            using (var key = new UpdateSigningTestKey())
            {
                bool result = UpdateSignatureVerifier.TryVerify(
                    key.PublicKey, Tag, AssetName, Size, Sha256, signature, out string errorMessage);

                Assert.False(result);
                Assert.Contains("形式が不正", errorMessage);
            }
        }

        /// <summary>
        /// 公開鍵が壊れている場合は検証に失敗することを検証する
        /// </summary>
        [Theory]
        [InlineData("not base64!")]
        [InlineData("AAAA")]
        public void TryVerify_InvalidPublicKey_ReturnsFalse(string publicKey)
        {
            using (var key = new UpdateSigningTestKey())
            {
                string signature = key.Sign(Tag, AssetName, Size, Sha256);

                bool result = UpdateSignatureVerifier.TryVerify(
                    publicKey, Tag, AssetName, Size, Sha256, signature, out string errorMessage);

                Assert.False(result);
                Assert.Contains("公開鍵が不正", errorMessage);
            }
        }

        /// <summary>
        /// 署名スクリプト (update-signing.ps1) が作った署名を検証できることを検証する
        /// </summary>
        [Fact]
        public void TryVerify_SignatureFromSigningScript_ReturnsTrue()
        {
            bool result = UpdateSignatureVerifier.TryVerify(
                ScriptPublicKey,
                "v9.9.9",
                "PdfConverter-9.9.9-x64-setup.exe",
                18,
                ScriptSha256,
                ScriptSignature,
                out string errorMessage);

            Assert.True(result, errorMessage);
        }
    }
}
