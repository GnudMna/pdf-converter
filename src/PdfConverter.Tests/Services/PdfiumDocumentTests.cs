using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="PdfiumDocument"/> による PDF の読み込みと描画を検証する
    /// </summary>
    public class PdfiumDocumentTests
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>32 バイトのダミー値 (暗号化辞書の /O・/U、文書 ID に使用)</summary>
        private const string Dummy32BytesHex = "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F";


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// ページ数とページのピクセルサイズを取得できることを検証する
        /// </summary>
        [Fact]
        public void PageCountAndPixelSize_ReturnsValuesForScale()
        {
            byte[] pdf = BuildPdf(new[] { (200, 100), (50, 80) });

            using (var document = new PdfiumDocument(pdf))
            {
                Assert.Equal(2, document.PageCount);
                Assert.Equal((200, 100), document.GetPagePixelSize(0, 1.0));
                Assert.Equal((400, 200), document.GetPagePixelSize(0, 2.0));
                Assert.Equal((25, 40), document.GetPagePixelSize(1, 0.5));
            }
        }

        /// <summary>
        /// 描画結果が BGRA32 で、塗りつぶした領域は不透明・何も描かない領域は透明になることを検証する
        /// </summary>
        [Fact]
        public void RenderPage_ReturnsBgraBitmapWithTransparentBackground()
        {
            // 左半分 (PDF 座標の x: 0-100) を赤で塗りつぶす
            byte[] pdf = BuildPdf(new[] { (200, 100) }, "1 0 0 rg 0 0 100 100 re f");

            using (var document = new PdfiumDocument(pdf))
            {
                BitmapSource bitmap = document.RenderPage(0, 1.0);

                Assert.Equal(200, bitmap.PixelWidth);
                Assert.Equal(100, bitmap.PixelHeight);
                Assert.Equal(PixelFormats.Bgra32, bitmap.Format);
                Assert.True(bitmap.IsFrozen);
                Assert.Equal(new byte[] { 0, 0, 255, 255 }, ReadPixel(bitmap, 50, 50));
                Assert.Equal(0, ReadPixel(bitmap, 150, 50)[3]);
            }
        }

        /// <summary>
        /// 外観ストリームを持つ注釈 (矩形注釈) が描画されることを検証する
        /// </summary>
        [Fact]
        public void RenderPage_Annotation_IsRendered()
        {
            // オブジェクト 5: 右半分を覆う矩形注釈、オブジェクト 6: 青で塗りつぶす外観ストリーム
            string[] extraObjects =
            {
                "<< /Type /Annot /Subtype /Square /Rect [100 0 200 100] /C [0 0 1] /AP << /N 6 0 R >> >>",
                FormXObject("0 0 1 rg 0 0 100 100 re f"),
            };
            byte[] pdf = BuildPdf(new[] { (200, 100) }, pageExtras: "/Annots [5 0 R]", extraObjects: extraObjects);

            using (var document = new PdfiumDocument(pdf))
            {
                BitmapSource bitmap = document.RenderPage(0, 1.0);

                Assert.Equal(new byte[] { 255, 0, 0, 255 }, ReadPixel(bitmap, 150, 50));
                Assert.Equal(0, ReadPixel(bitmap, 50, 50)[3]);
            }
        }

        /// <summary>
        /// フォームフィールド (ウィジェット注釈) の外観が描画され、入力欄のハイライトは描画されないことを検証する
        /// </summary>
        [Fact]
        public void RenderPage_FormField_IsRenderedWithoutHighlight()
        {
            // オブジェクト 5: 右半分のテキストフィールド (外観は中央のみ緑)、オブジェクト 6: 外観ストリーム
            string[] extraObjects =
            {
                "<< /Type /Annot /Subtype /Widget /FT /Tx /T (field1) /Rect [100 0 200 100] /F 4 /AP << /N 6 0 R >> >>",
                FormXObject("0 1 0 rg 25 25 50 50 re f"),
            };
            byte[] pdf = BuildPdf(
                new[] { (200, 100) },
                pageExtras: "/Annots [5 0 R]",
                catalogExtras: "/AcroForm << /Fields [5 0 R] >>",
                extraObjects: extraObjects);

            using (var document = new PdfiumDocument(pdf))
            {
                BitmapSource bitmap = document.RenderPage(0, 1.0);

                Assert.Equal(new byte[] { 0, 255, 0, 255 }, ReadPixel(bitmap, 150, 50));
                Assert.Equal(0, ReadPixel(bitmap, 105, 5)[3]);
            }
        }

        /// <summary>
        /// 描画倍率に応じたサイズでビットマップが生成されることを検証する
        /// </summary>
        [Fact]
        public void RenderPage_Scale_ChangesBitmapSize()
        {
            byte[] pdf = BuildPdf(new[] { (200, 100) });

            using (var document = new PdfiumDocument(pdf))
            {
                BitmapSource bitmap = document.RenderPage(0, 1.5);

                Assert.Equal(300, bitmap.PixelWidth);
                Assert.Equal(150, bitmap.PixelHeight);
            }
        }

        /// <summary>
        /// 範囲外のページインデックスで ArgumentOutOfRangeException になることを検証する
        /// </summary>
        [Theory]
        [InlineData(-1)]
        [InlineData(1)]
        public void PageAccess_OutOfRange_ThrowsArgumentOutOfRangeException(int pageIndex)
        {
            using (var document = new PdfiumDocument(BuildPdf(new[] { (100, 100) })))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => document.RenderPage(pageIndex, 1.0));
                Assert.Throws<ArgumentOutOfRangeException>(() => document.GetPagePixelSize(pageIndex, 1.0));
                Assert.Throws<ArgumentOutOfRangeException>(() => document.GetRenderSize(pageIndex, 1.0));
            }
        }

        /// <summary>
        /// 描画サイズが 0 になる倍率で InvalidDataException になることを検証する
        /// </summary>
        [Fact]
        public void RenderPage_ZeroPixelSize_ThrowsInvalidDataException()
        {
            using (var document = new PdfiumDocument(BuildPdf(new[] { (100, 100) })))
            {
                Assert.Throws<InvalidDataException>(() => document.RenderPage(0, 0.001));
            }
        }

        /// <summary>
        /// 上限以内の描画サイズを切り捨てたピクセル数で返すことを検証する
        /// </summary>
        [Fact]
        public void GetRenderSize_WithinLimit_ReturnsFlooredPixelSize()
        {
            using (var document = new PdfiumDocument(BuildPdf(new[] { (200, 100) })))
            {
                Assert.Equal((300, 150), document.GetRenderSize(0, 1.5));
                Assert.Equal((200, 100), document.GetRenderSize(0, 1.004));
            }
        }

        /// <summary>
        /// 描画サイズが上限を超える場合、描画前に InvalidOperationException になることを検証する
        /// </summary>
        [Fact]
        public void RenderPageAndGetRenderSize_ExceedingLimit_ThrowsInvalidOperationException()
        {
            // 14400pt 四方 (PDF の最大ページサイズ) を 2 倍で描画すると 28800x28800 px (約 8.3 億画素) になる
            using (var document = new PdfiumDocument(BuildPdf(new[] { (14400, 14400) })))
            {
                var renderException = Assert.Throws<InvalidOperationException>(() => document.RenderPage(0, 2.0));
                Assert.Contains("上限", renderException.Message);
                Assert.Throws<InvalidOperationException>(() => document.GetRenderSize(0, 2.0));
            }
        }

        /// <summary>
        /// 画素数が上限ちょうどなら許容し、1 画素でも超えれば InvalidOperationException になることを検証する
        /// </summary>
        [Fact]
        public void ValidateRenderSize_Boundary_AllowsExactLimitAndRejectsOver()
        {
            Assert.Equal((1000, 500), PdfiumDocument.ValidateRenderSize(0, 1000.9, 500.9, 500_000));

            var exception = Assert.Throws<InvalidOperationException>(
                () => PdfiumDocument.ValidateRenderSize(2, 1001, 500, 500_000));
            Assert.Contains("ページ 3", exception.Message);
            Assert.Contains("1001x500", exception.Message);
        }

        /// <summary>
        /// int に収まらない巨大な描画サイズでもオーバーフローせず InvalidOperationException になることを検証する
        /// </summary>
        [Theory]
        [InlineData(1e10, 10)]
        [InlineData(10, 1e10)]
        [InlineData(double.PositiveInfinity, 10)]
        public void ValidateRenderSize_HugeSize_ThrowsInvalidOperationException(double width, double height)
        {
            Assert.Throws<InvalidOperationException>(
                () => PdfiumDocument.ValidateRenderSize(0, width, height, PdfiumDocument.MaxRenderPixelsX64));
        }

        /// <summary>
        /// 1 ピクセル未満または数値でない描画サイズで InvalidDataException になることを検証する
        /// </summary>
        [Theory]
        [InlineData(0.9, 10)]
        [InlineData(10, 0)]
        [InlineData(-5, 10)]
        [InlineData(double.NaN, 10)]
        public void ValidateRenderSize_InvalidSize_ThrowsInvalidDataException(double width, double height)
        {
            Assert.Throws<InvalidDataException>(
                () => PdfiumDocument.ValidateRenderSize(0, width, height, PdfiumDocument.MaxRenderPixelsX64));
        }

        /// <summary>
        /// 1 ページあたりの最大画素数が、プロセスのビット数に応じた値になることを検証する
        /// </summary>
        [Fact]
        public void MaxRenderPixels_MatchesProcessBitness()
        {
            long expected = Environment.Is64BitProcess ? PdfiumDocument.MaxRenderPixelsX64 : PdfiumDocument.MaxRenderPixelsX86;

            Assert.Equal(expected, PdfiumDocument.MaxRenderPixels);
        }

        /// <summary>
        /// 空のデータで ArgumentException になることを検証する
        /// </summary>
        [Fact]
        public void Constructor_EmptyBytes_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new PdfiumDocument(Array.Empty<byte>()));
        }

        /// <summary>
        /// PDF ヘッダーはあるが中身が壊れているデータで InvalidDataException になることを検証する
        /// </summary>
        [Fact]
        public void Constructor_CorruptedPdf_ThrowsInvalidDataException()
        {
            byte[] corrupted = Encoding.ASCII.GetBytes("%PDF-1.4\nthis is not a valid pdf body\n%%EOF\n");

            var exception = Assert.Throws<InvalidDataException>(() => new PdfiumDocument(corrupted));
            Assert.Contains("破損", exception.Message);
        }

        /// <summary>
        /// パスワードで保護された PDF で、原因が分かるメッセージの InvalidDataException になることを検証する
        /// </summary>
        [Fact]
        public void Constructor_PasswordProtectedPdf_ThrowsInvalidDataExceptionWithPasswordMessage()
        {
            // 空パスワードで照合できない /U を持つ標準セキュリティハンドラーの暗号化辞書
            string encryptObject = $"<< /Filter /Standard /V 1 /R 2 /Length 40 /O <{Dummy32BytesHex}> /U <{Dummy32BytesHex}> /P -4 >>";
            string trailerExtras = $"/ID [<{Dummy32BytesHex}> <{Dummy32BytesHex}>]";
            byte[] pdf = BuildPdf(new[] { (100, 100) }, encryptObject: encryptObject, trailerExtras: trailerExtras);

            var exception = Assert.Throws<InvalidDataException>(() => new PdfiumDocument(pdf));
            Assert.Contains("パスワード", exception.Message);
        }

        /// <summary>
        /// 破棄後の操作で ObjectDisposedException になり、二重破棄では例外にならないことを検証する
        /// </summary>
        [Fact]
        public void Dispose_ThenAccess_ThrowsObjectDisposedException()
        {
            var document = new PdfiumDocument(BuildPdf(new[] { (100, 100) }));

            document.Dispose();
            document.Dispose();

            Assert.Throws<ObjectDisposedException>(() => document.RenderPage(0, 1.0));
            Assert.Throws<ObjectDisposedException>(() => document.GetPagePixelSize(0, 1.0));
            Assert.Throws<ObjectDisposedException>(() => document.GetRenderSize(0, 1.0));
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// テスト用の最小限の PDF をバイト列として組み立てる
        /// </summary>
        /// <param name="pageSizes">ページごとのサイズ (pt)</param>
        /// <param name="contentStream">全ページ共通のコンテンツストリーム (<c>null</c> の場合は描画なし)</param>
        /// <param name="encryptObject">暗号化辞書 (<c>null</c> の場合は暗号化なし)</param>
        /// <param name="trailerExtras">trailer 辞書に追加するエントリー</param>
        /// <param name="pageExtras">全ページの辞書に追加するエントリー</param>
        /// <param name="catalogExtras">カタログ辞書に追加するエントリー</param>
        /// <param name="extraObjects">コンテンツストリームの直後に追加するオブジェクト (番号は 4 + ページ数から順に振られる)</param>
        /// <returns>PDF のバイト列</returns>
        private static byte[] BuildPdf(
            (int Width, int Height)[] pageSizes,
            string contentStream = null,
            string encryptObject = null,
            string trailerExtras = "",
            string pageExtras = "",
            string catalogExtras = "",
            string[] extraObjects = null)
        {
            extraObjects = extraObjects ?? Array.Empty<string>();
            int firstPageObject = 3;
            int contentObject = firstPageObject + pageSizes.Length;
            int encryptObjectNumber = contentObject + extraObjects.Length + 1;

            var objects = new List<string>
            {
                $"<< /Type /Catalog /Pages 2 0 R {catalogExtras} >>",
            };

            var kids = new StringBuilder();
            for (int i = 0; i < pageSizes.Length; i++)
            {
                kids.Append(CultureInfo.InvariantCulture, $"{firstPageObject + i} 0 R ");
            }

            objects.Add($"<< /Type /Pages /Kids [{kids}] /Count {pageSizes.Length} >>");

            string contentsEntry = contentStream == null ? string.Empty : $" /Contents {contentObject} 0 R";
            foreach ((int width, int height) in pageSizes)
            {
                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}]{contentsEntry} {pageExtras} >>");
            }

            objects.Add(contentStream == null
                ? "null"
                : $"<< /Length {contentStream.Length} >>\nstream\n{contentStream}\nendstream");
            objects.AddRange(extraObjects);

            if (encryptObject != null)
            {
                objects.Add(encryptObject);
                trailerExtras = $"/Encrypt {encryptObjectNumber} 0 R {trailerExtras}";
            }

            var pdf = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int>();
            for (int i = 0; i < objects.Count; i++)
            {
                offsets.Add(pdf.Length);
                pdf.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
            }

            int xrefStart = pdf.Length;
            pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
            foreach (int offset in offsets)
            {
                pdf.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
            }

            pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R {trailerExtras} >>\nstartxref\n{xrefStart}\n%%EOF\n");
            return Encoding.ASCII.GetBytes(pdf.ToString());
        }

        /// <summary>
        /// 100x100 の外観ストリーム (フォーム XObject) を組み立てる
        /// </summary>
        /// <param name="content">描画内容</param>
        /// <returns>オブジェクト本体</returns>
        private static string FormXObject(string content)
        {
            return $"<< /Type /XObject /Subtype /Form /BBox [0 0 100 100] /Length {content.Length} >>\nstream\n{content}\nendstream";
        }

        /// <summary>
        /// BGRA32 ビットマップの指定位置のピクセル (B, G, R, A) を読み取る
        /// </summary>
        /// <param name="bitmap">BGRA32 ビットマップ</param>
        /// <param name="x">X 座標</param>
        /// <param name="y">Y 座標</param>
        /// <returns>B, G, R, A の 4 バイト</returns>
        private static byte[] ReadPixel(BitmapSource bitmap, int x, int y)
        {
            var pixel = new byte[4];
            bitmap.CopyPixels(new System.Windows.Int32Rect(x, y, 1, 1), pixel, 4, 0);
            return pixel;
        }
    }
}
