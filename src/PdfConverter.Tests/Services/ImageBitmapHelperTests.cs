using System;
using System.IO;
using FluentAssertions;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="ImageBitmapHelper"/> の拡張子取得・透明度処理・ファイル保存を検証する
    /// </summary>
    public class ImageBitmapHelperTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 各出力形式に対応するファイル拡張子が正しく返されることを検証する
        /// </summary>
        [Theory]
        [InlineData(OutputImageFormat.Png, ".png")]
        [InlineData(OutputImageFormat.Jpeg, ".jpg")]
        [InlineData(OutputImageFormat.Bmp, ".bmp")]
        public void GetFileExtension_ReturnsExpectedExtension(OutputImageFormat format, string expected)
        {
            ImageBitmapHelper.GetFileExtension(format).Should().Be(expected);
        }

        /// <summary>
        /// 未対応の出力形式で ArgumentOutOfRangeException がスローされることを検証する
        /// </summary>
        [Fact]
        public void GetFileExtension_UnsupportedFormat_Throws()
        {
            Action act = () => ImageBitmapHelper.GetFileExtension((OutputImageFormat)999);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        /// <summary>
        /// JPEG のみ透明度非対応、PNG/BMP は透明度対応であることを検証する
        /// </summary>
        [Theory]
        [InlineData(OutputImageFormat.Png, true)]
        [InlineData(OutputImageFormat.Bmp, true)]
        [InlineData(OutputImageFormat.Jpeg, false)]
        public void SupportsTransparency_ReturnsExpectedValue(OutputImageFormat format, bool expected)
        {
            ImageBitmapHelper.SupportsTransparency(format).Should().Be(expected);
        }

        /// <summary>
        /// 透明度保持時に元の BitmapSource インスタンスがそのまま返されることを検証する
        /// </summary>
        [Fact]
        public void ApplyTransparency_WhenPreserved_ReturnsSameInstance()
        {
            StaTestHelper.Run(() =>
            {
                var source = BitmapTestHelper.CreateBitmap();

                var result = ImageBitmapHelper.ApplyTransparency(source, preserveTransparency: true);

                result.Should().BeSameAs(source);
            });
        }

        /// <summary>
        /// 透明度を保持しない場合に白背景へ合成された別インスタンスの Freeze 済みビットマップが返されることを検証する
        /// </summary>
        [Fact]
        public void ApplyTransparency_WhenFlattened_ReturnsDifferentFrozenBitmap()
        {
            StaTestHelper.Run(() =>
            {
                var source = BitmapTestHelper.CreateBitmap();

                var result = ImageBitmapHelper.ApplyTransparency(source, preserveTransparency: false);

                result.Should().NotBeSameAs(source);
                result.IsFrozen.Should().BeTrue();
                result.PixelWidth.Should().Be(source.PixelWidth);
            });
        }

        /// <summary>
        /// null の BitmapSource を渡した場合に ArgumentNullException がスローされることを検証する
        /// </summary>
        [Fact]
        public void ApplyTransparency_NullSource_ThrowsArgumentNullException()
        {
            StaTestHelper.Run(() =>
            {
                Action act = () => ImageBitmapHelper.ApplyTransparency(null, true);

                act.Should().Throw<ArgumentNullException>();
            });
        }

        /// <summary>
        /// ビットマップが指定パスに画像ファイルとして書き込まれることを検証する
        /// </summary>
        [Fact]
        public void SaveToFile_WritesImageFile()
        {
            StaTestHelper.Run(() =>
            {
                var source = BitmapTestHelper.CreateBitmap();
                string outputPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.png");

                try
                {
                    ImageBitmapHelper.SaveToFile(source, outputPath, OutputImageFormat.Png);

                    File.Exists(outputPath).Should().BeTrue();
                    new FileInfo(outputPath).Length.Should().BeGreaterThan(0);
                }
                finally
                {
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }
                }
            });
        }

        /// <summary>
        /// 出力パスが空の場合に ArgumentException がスローされることを検証する
        /// </summary>
        [Fact]
        public void SaveToFile_EmptyPath_ThrowsArgumentException()
        {
            StaTestHelper.Run(() =>
            {
                var source = BitmapTestHelper.CreateBitmap();

                Action act = () => ImageBitmapHelper.SaveToFile(source, "", OutputImageFormat.Png);

                act.Should().Throw<ArgumentException>();
            });
        }
    }
}
