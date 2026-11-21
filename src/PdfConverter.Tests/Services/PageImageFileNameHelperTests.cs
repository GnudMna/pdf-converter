using System;
using PdfConverter.Models;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="PageImageFileNameHelper"/> の出力ファイル名生成を検証する
    /// </summary>
    public class PageImageFileNameHelperTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// ページ番号が総ページ数の桁数までゼロ埋めされることを検証する
        /// </summary>
        [Theory]
        [InlineData(0, 1, "page_1.png")]
        [InlineData(8, 9, "page_9.png")]
        [InlineData(0, 10, "page_01.png")]
        [InlineData(9, 10, "page_10.png")]
        [InlineData(4, 150, "page_005.png")]
        [InlineData(149, 150, "page_150.png")]
        [InlineData(0, 1000, "page_0001.png")]
        public void GetFileName_PadsPageNumberToPageCountDigits(int pageIndex, int pageCount, string expected)
        {
            Assert.Equal(expected, PageImageFileNameHelper.GetFileName(pageIndex, pageCount, OutputImageFormat.Png));
        }

        /// <summary>
        /// 出力形式に応じた拡張子が付くことを検証する
        /// </summary>
        [Theory]
        [InlineData(OutputImageFormat.Png, "page_2.png")]
        [InlineData(OutputImageFormat.Jpeg, "page_2.jpg")]
        [InlineData(OutputImageFormat.Bmp, "page_2.bmp")]
        public void GetFileName_AppendsFormatExtension(OutputImageFormat format, string expected)
        {
            Assert.Equal(expected, PageImageFileNameHelper.GetFileName(1, 5, format));
        }

        /// <summary>
        /// 範囲外のページインデックスで ArgumentOutOfRangeException になることを検証する
        /// </summary>
        [Theory]
        [InlineData(-1, 5)]
        [InlineData(5, 5)]
        [InlineData(0, 0)]
        public void GetFileName_OutOfRange_ThrowsArgumentOutOfRangeException(int pageIndex, int pageCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PageImageFileNameHelper.GetFileName(pageIndex, pageCount, OutputImageFormat.Png));
        }
    }
}
