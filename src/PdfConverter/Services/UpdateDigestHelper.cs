using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PdfConverter.Services
{
    /// <summary>
    /// 更新アセットの digest / サイズ検証ヘルパー
    /// </summary>
    public static class UpdateDigestHelper
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>SHA-256 の 16 進文字数</summary>
        public const int Sha256HexLength = 64;

        /// <summary>更新アセットダウンロードの絶対サイズ上限（512 MiB）</summary>
        public const long MaxUpdateDownloadBytes = 512L * 1024L * 1024L;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// <c>sha256:&lt;hex&gt;</c> または素の 16 進を正規化する
        /// </summary>
        /// <param name="raw">生文字列</param>
        /// <param name="sha256">小文字 64 桁</param>
        /// <param name="errorMessage">失敗時のメッセージ</param>
        /// <returns>成功すれば <c>true</c></returns>
        public static bool TryParseDigest(string raw, out string sha256, out string errorMessage)
        {
            sha256 = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                errorMessage = "アセットの digest が空です。";
                return false;
            }

            string trimmed = raw.Trim();
            string hex = trimmed;
            const string prefix = "sha256:";
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                hex = trimmed.Substring(prefix.Length).Trim();
            }

            if (hex.Length != Sha256HexLength || !IsHex(hex))
            {
                errorMessage = $"アセットの digest が不正です: {trimmed}";
                return false;
            }

            sha256 = hex.ToLowerInvariant();
            return true;
        }

        /// <summary>
        /// 宣言サイズがダウンロード可能な範囲か検証する
        /// </summary>
        /// <param name="expectedSize">version API が報告するバイト数</param>
        /// <param name="errorMessage">失敗時のメッセージ</param>
        /// <returns>許容範囲なら <c>true</c></returns>
        public static bool TryValidateDeclaredSize(long expectedSize, out string errorMessage)
        {
            errorMessage = null;
            if (expectedSize <= 0)
            {
                errorMessage = "アセットのサイズが 0 です。";
                return false;
            }

            if (expectedSize > MaxUpdateDownloadBytes)
            {
                errorMessage =
                    $"アセットサイズが上限（{MaxUpdateDownloadBytes} バイト）を超えています。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// ファイルの SHA-256（小文字 16 進）を計算する
        /// </summary>
        /// <param name="filePath">ファイルパス</param>
        /// <returns>小文字 64 桁のハッシュ</returns>
        public static string ComputeSha256Hex(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var builder = new StringBuilder(Sha256HexLength);
                foreach (byte b in hash)
                {
                    builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        private static bool IsHex(string value)
        {
            foreach (char c in value)
            {
                bool isDigit = c >= '0' && c <= '9';
                bool isLower = c >= 'a' && c <= 'f';
                bool isUpper = c >= 'A' && c <= 'F';
                if (!isDigit && !isLower && !isUpper)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
