using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// PDF 変換テスト用の一時ファイルを生成する
    /// </summary>
    internal static class PdfTestAssetFactory
    {
        /// <summary>
        /// 指定ページ数の最小限の有効な PDF を一時ファイルとして書き出す
        /// </summary>
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
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
