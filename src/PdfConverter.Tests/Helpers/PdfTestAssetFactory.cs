using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// PDF 変換テスト用の一時ファイルを生成する
    /// </summary>
    internal static class PdfTestAssetFactory
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 指定ページ数の最小限の有効な PDF を一時ファイルとして書き出す
        /// </summary>
        public static string CreateTempPdf(int pageCount = 1)
        {
            if (pageCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pageCount), "ページ数は 1 以上である必要があります。");
            }

            var pageSizes = new (double Width, double Height)[pageCount];
            for (int i = 0; i < pageCount; i++)
            {
                pageSizes[i] = (200, 200);
            }

            return CreateTempPdfWithPageSizes(pageSizes);
        }

        /// <summary>
        /// ページごとに指定サイズ (pt) を持つ最小限の有効な PDF を一時ファイルとして書き出す
        /// </summary>
        public static string CreateTempPdfWithPageSizes(params (double Width, double Height)[] pageSizes)
        {
            return WriteTempPdf(pageSizes, paddingBytes: 0);
        }

        /// <summary>
        /// ヘッダー直後に指定バイト数以上のコメント行を挟んだ、200x200 pt のページを持つ有効な PDF を一時ファイルとして書き出す
        /// </summary>
        /// <remarks>メモリキャッシュの上限を超える大きな PDF の読み込みを検証するために使う</remarks>
        public static string CreateTempPdfWithPadding(long paddingBytes, int pageCount = 1)
        {
            var pageSizes = new (double Width, double Height)[pageCount];
            for (int i = 0; i < pageCount; i++)
            {
                pageSizes[i] = (200, 200);
            }

            return WriteTempPdf(pageSizes, paddingBytes);
        }

        /// <summary>
        /// PDF ヘッダーを持たない、指定サイズの 0 埋めファイルを一時パスへ書き出す
        /// </summary>
        public static string CreateTempInvalidPdfFileOfSize(long sizeBytes)
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                stream.SetLength(sizeBytes);
            }

            return path;
        }

        /// <summary>
        /// 有効な PDF の前に任意のバイト列を付加した PDF を一時ファイルとして書き出す
        /// </summary>
        public static string CreateTempPdfWithLeadingBytes(byte[] leadingBytes, int pageCount = 1)
        {
            string sourcePath = CreateTempPdf(pageCount);
            try
            {
                byte[] pdfBytes = File.ReadAllBytes(sourcePath);
                var combined = new byte[leadingBytes.Length + pdfBytes.Length];
                Buffer.BlockCopy(leadingBytes, 0, combined, 0, leadingBytes.Length);
                Buffer.BlockCopy(pdfBytes, 0, combined, leadingBytes.Length, pdfBytes.Length);

                string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
                File.WriteAllBytes(path, combined);
                return path;
            }
            finally
            {
                DeleteIfExists(sourcePath);
            }
        }

        /// <summary>
        /// 開くのにパスワードが必要な 1 ページの PDF を一時ファイルとして書き出す
        /// </summary>
        /// <param name="userPassword">PDF を開くパスワード</param>
        public static string CreateTempPasswordProtectedPdf(string userPassword)
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(path, BuildPasswordProtectedPdf(userPassword));
            return path;
        }

        /// <summary>
        /// 開くのにパスワードが必要な 1 ページの PDF をバイト列として組み立てる (標準セキュリティハンドラー、RC4 40 ビット)
        /// </summary>
        /// <remarks>ページ辞書に文字列・ストリームを含めないため、オブジェクトの暗号化は不要で、暗号化辞書だけを正しく作ればよい</remarks>
        /// <param name="userPassword">PDF を開くパスワード (ASCII)</param>
        public static byte[] BuildPasswordProtectedPdf(string userPassword)
        {
            byte[] padding =
            {
                0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
                0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
            };

            // O はユーザーパスワードの照合に鍵の材料としてのみ使われるため、任意の 32 バイトでよい
            byte[] ownerEntry = new byte[32];
            for (int i = 0; i < ownerEntry.Length; i++)
            {
                ownerEntry[i] = (byte)(i + 1);
            }

            byte[] documentId = new byte[16];
            for (int i = 0; i < documentId.Length; i++)
            {
                documentId[i] = (byte)(0xA0 + i);
            }

            // 権限フラグ -4 (32 ビットのリトルエンディアン)
            byte[] permissions = { 0xFC, 0xFF, 0xFF, 0xFF };

            byte[] paddedPassword = new byte[32];
            byte[] passwordBytes = Encoding.ASCII.GetBytes(userPassword);
            int copied = Math.Min(passwordBytes.Length, 32);
            Array.Copy(passwordBytes, paddedPassword, copied);
            Array.Copy(padding, 0, paddedPassword, copied, 32 - copied);

            byte[] keySource = new byte[paddedPassword.Length + ownerEntry.Length + permissions.Length + documentId.Length];
            int offset = 0;
            foreach (byte[] part in new[] { paddedPassword, ownerEntry, permissions, documentId })
            {
                Array.Copy(part, 0, keySource, offset, part.Length);
                offset += part.Length;
            }

            // PDF 標準セキュリティハンドラーの鍵導出が MD5 を規定しているため、テスト用の暗号化 PDF の生成でも MD5 を使う
#pragma warning disable CA5351
            byte[] key = MD5.HashData(keySource).AsSpan(0, 5).ToArray();
#pragma warning restore CA5351
            byte[] userEntry = Rc4(key, padding);

            string ownerHex = Convert.ToHexString(ownerEntry);
            string userHex = Convert.ToHexString(userEntry);
            string idHex = Convert.ToHexString(documentId);

            var objects = new[]
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>",
                $"<< /Filter /Standard /V 1 /R 2 /Length 40 /O <{ownerHex}> /U <{userHex}> /P -4 >>",
            };

            var pdf = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int>();
            for (int i = 0; i < objects.Length; i++)
            {
                offsets.Add(pdf.Length);
                pdf.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
            }

            int xrefStart = pdf.Length;
            pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
            foreach (int objectOffset in offsets)
            {
                pdf.Append(CultureInfo.InvariantCulture, $"{objectOffset:D10} 00000 n \n");
            }

            pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R /Encrypt 4 0 R /ID [<{idHex}> <{idHex}>] >>\nstartxref\n{xrefStart}\n%%EOF\n");
            return Encoding.ASCII.GetBytes(pdf.ToString());
        }

        /// <summary>
        /// PDF ヘッダーを持たない不正ファイルを一時パスへ書き出す
        /// </summary>
        public static string CreateTempInvalidPdfFile(string content = "NOT A PDF FILE")
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
            File.WriteAllText(path, content, Encoding.ASCII);
            return path;
        }

        /// <summary>
        /// 画像保存テスト用の一時出力ディレクトリを作成する
        /// </summary>
        public static string CreateTempOutputDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-out-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// 一時ファイルを削除する
        /// </summary>
        public static void DeleteIfExists(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// 一時ディレクトリを再帰的に削除する
        /// </summary>
        public static void DeleteDirectoryIfExists(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// RC4 でデータを変換する
        /// </summary>
        /// <param name="key">鍵</param>
        /// <param name="data">変換するデータ</param>
        /// <returns>変換後のデータ</returns>
        private static byte[] Rc4(byte[] key, byte[] data)
        {
            byte[] state = new byte[256];
            for (int i = 0; i < state.Length; i++)
            {
                state[i] = (byte)i;
            }

            int j = 0;
            for (int i = 0; i < state.Length; i++)
            {
                j = (j + state[i] + key[i % key.Length]) & 0xFF;
                (state[i], state[j]) = (state[j], state[i]);
            }

            byte[] result = new byte[data.Length];
            int a = 0;
            int b = 0;
            for (int n = 0; n < data.Length; n++)
            {
                a = (a + 1) & 0xFF;
                b = (b + state[a]) & 0xFF;
                (state[a], state[b]) = (state[b], state[a]);
                result[n] = (byte)(data[n] ^ state[(state[a] + state[b]) & 0xFF]);
            }

            return result;
        }

        /// <summary>
        /// ページごとに指定サイズ (pt) を持ち、ヘッダー直後に指定バイト数以上のコメント行を挟んだ PDF を一時ファイルとして書き出す
        /// </summary>
        private static string WriteTempPdf((double Width, double Height)[] pageSizes, long paddingBytes)
        {
            if (pageSizes == null || pageSizes.Length < 1)
            {
                throw new ArgumentException("ページサイズを 1 件以上指定する必要があります。", nameof(pageSizes));
            }

            int pageCount = pageSizes.Length;
            var bodyObjects = new List<string>
            {
                "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            };

            var kids = new StringBuilder();
            for (int i = 0; i < pageCount; i++)
            {
                if (i > 0)
                {
                    kids.Append(' ');
                }

                kids.Append(CultureInfo.InvariantCulture, $"{3 + i} 0 R");
            }

            bodyObjects.Add($"2 0 obj\n<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>\nendobj\n");

            for (int i = 0; i < pageCount; i++)
            {
                int objectNumber = 3 + i;
                bodyObjects.Add($"{objectNumber} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageSizes[i].Width} {pageSizes[i].Height}] >>\nendobj\n");
            }

            const string header = "%PDF-1.4\n";

            // コメント行は 1 KB ずつ書き出し、オブジェクトのオフセットはその分ずらす
            const int paddingLineLength = 1024;
            string paddingLine = "%" + new string('x', paddingLineLength - 2) + "\n";
            long paddingLineCount = (paddingBytes + paddingLineLength - 1) / paddingLineLength;
            long bodyStart = header.Length + paddingLineCount * paddingLineLength;

            var pdf = new StringBuilder();
            var offsets = new List<long> { 0 };
            foreach (string bodyObject in bodyObjects)
            {
                offsets.Add(bodyStart + pdf.Length);
                pdf.Append(bodyObject);
            }

            long xrefStart = bodyStart + pdf.Length;
            int totalObjects = bodyObjects.Count + 1;

            pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {totalObjects}\n");
            pdf.Append("0000000000 65535 f \n");
            for (int i = 1; i < totalObjects; i++)
            {
                pdf.Append(CultureInfo.InvariantCulture, $"{offsets[i]:D10} 00000 n \n");
            }

            pdf.Append("trailer\n");
            pdf.Append(CultureInfo.InvariantCulture, $"<< /Size {totalObjects} /Root 1 0 R >>\n");
            pdf.Append("startxref\n");
            pdf.Append(CultureInfo.InvariantCulture, $"{xrefStart}\n");
            pdf.Append("%%EOF\n");

            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
            using (var writer = new StreamWriter(path, append: false, Encoding.ASCII))
            {
                writer.Write(header);
                for (long i = 0; i < paddingLineCount; i++)
                {
                    writer.Write(paddingLine);
                }

                writer.Write(pdf.ToString());
            }

            return path;
        }
    }
}
