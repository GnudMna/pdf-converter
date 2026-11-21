using PdfConverter.Infrastructure;
using PdfConverter.Models;
using Xunit;

namespace PdfConverter.Tests.Infrastructure
{
    /// <summary>
    /// <see cref="BatchCliParser"/> のコマンドライン引数解析を検証する
    /// </summary>
    public class BatchCliParserTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// スイッチを含まない引数は GUI 起動として扱うことを検証する
        /// </summary>
        [Fact]
        public void TryParse_WithoutSwitches_ReturnsFalse()
        {
            bool isBatch = BatchCliParser.TryParse(
                new[] { @"C:\docs\sample.pdf" },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.False(isBatch);
            Assert.Null(options);
            Assert.Null(errorMessage);
        }

        /// <summary>
        /// スラッシュで始まるパスのみの引数は GUI 起動として扱うことを検証する
        /// </summary>
        [Theory]
        [InlineData("//server/share/a.pdf")]
        [InlineData(@"\\server\share\a.pdf")]
        [InlineData("/docs/a.pdf")]
        public void TryParse_WithSlashPathOnly_ReturnsFalse(string path)
        {
            bool isBatch = BatchCliParser.TryParse(
                new[] { path },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.False(isBatch);
            Assert.Null(options);
            Assert.Null(errorMessage);
        }

        /// <summary>
        /// スイッチと併用したスラッシュで始まるパスを入力ファイルとして扱うことを検証する
        /// </summary>
        [Fact]
        public void TryParse_WithSlashPathAndSwitches_TreatsPathAsInputFile()
        {
            bool isBatch = BatchCliParser.TryParse(
                new[] { "/o", @"C:\out", "//server/share/a.pdf" },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(errorMessage);
            Assert.Equal(@"C:\out", options.OutputPath);
            Assert.Single(options.InputFiles);
            Assert.Equal("//server/share/a.pdf", options.InputFiles[0]);
        }

        /// <summary>
        /// --help 指定時にバッチモードとして解析されることを検証する
        /// </summary>
        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        [InlineData("/?")]
        [InlineData("--?")]
        public void TryParse_WithHelp_ReturnsShowHelp(string helpSwitch)
        {
            bool isBatch = BatchCliParser.TryParse(
                new[] { helpSwitch },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.True(options.ShowHelp);
            Assert.Null(errorMessage);
        }

        /// <summary>
        /// 必須オプションが揃っている場合に解析できることを検証する
        /// </summary>
        [Fact]
        public void TryParse_WithRequiredOptions_ParsesValues()
        {
            bool isBatch = BatchCliParser.TryParse(
                new[]
                {
                    "--output", @"C:\out",
                    "-f", "png",
                    "-p", "1-2",
                    "--resolution-mode", "width",
                    "-r", "1920",
                    "--transparent",
                    "--overwrite",
                    "--quiet",
                    @"C:\docs\sample.pdf",
                },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(errorMessage);
            Assert.Equal(@"C:\out", options.OutputPath);
            Assert.Equal(OutputImageFormat.Png, options.Format);
            Assert.Equal("1-2", options.PageRange);
            Assert.Equal(ResolutionMode.Width, options.ResolutionMode);
            Assert.Equal("1920", options.ResolutionValue);
            Assert.True(options.PreserveTransparency);
            Assert.True(options.OverwriteExisting);
            Assert.True(options.Quiet);
            Assert.Single(options.InputFiles);
            Assert.Equal(@"C:\docs\sample.pdf", options.InputFiles[0]);
        }

        /// <summary>
        /// フラグ系スイッチに false を指定した場合に無効として扱うことを検証する
        /// </summary>
        [Fact]
        public void TryParse_FlagsWithFalseValue_AreDisabled()
        {
            bool isBatch = BatchCliParser.TryParse(
                new[]
                {
                    "-o", @"C:\out",
                    "--overwrite=false",
                    "--quiet=False",
                    "-q=false",
                    "--transparent=false",
                    "--export-pdf=false",
                    @"C:\docs\sample.pdf",
                },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(errorMessage);
            Assert.False(options.OverwriteExisting);
            Assert.False(options.Quiet);
            Assert.False(options.PreserveTransparency);
            Assert.False(options.ExportPdfOnly);
        }

        /// <summary>
        /// フラグ系スイッチに true を指定した場合に有効として扱うことを検証する
        /// </summary>
        [Fact]
        public void TryParse_FlagsWithTrueValue_AreEnabled()
        {
            bool isBatch = BatchCliParser.TryParse(
                new[]
                {
                    "-o", @"C:\out",
                    "--overwrite=true",
                    "--quiet=true",
                    "--no-transparent=false",
                    @"C:\docs\sample.pdf",
                },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(errorMessage);
            Assert.True(options.OverwriteExisting);
            Assert.True(options.Quiet);
            Assert.True(options.PreserveTransparency);
        }

        /// <summary>
        /// フラグ系スイッチに真偽値以外を指定した場合にエラーを返すことを検証する
        /// </summary>
        [Theory]
        [InlineData("--overwrite=abc")]
        [InlineData("--quiet=")]
        public void TryParse_FlagWithInvalidValue_ReturnsError(string flag)
        {
            bool isBatch = BatchCliParser.TryParse(
                new[] { "-o", @"C:\out", flag, @"C:\docs\sample.pdf" },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(options);
            Assert.Contains(flag, errorMessage);
        }

        /// <summary>
        /// 出力先未指定時にエラーを返すことを検証する
        /// </summary>
        [Fact]
        public void TryParse_WithoutOutput_ReturnsError()
        {
            bool isBatch = BatchCliParser.TryParse(
                new[] { "--batch", @"C:\docs\sample.pdf" },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(options);
            Assert.Contains("出力先", errorMessage);
        }

        /// <summary>
        /// 入力ファイル未指定時にエラーを返すことを検証する
        /// </summary>
        [Fact]
        public void TryParse_WithoutInputFiles_ReturnsError()
        {
            bool isBatch = BatchCliParser.TryParse(
                new[] { "-o", @"C:\out" },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(options);
            Assert.Contains("ファイル", errorMessage);
        }

        /// <summary>
        /// --export-pdf と --pages の併用を拒否することを検証する
        /// </summary>
        [Fact]
        public void TryParse_ExportPdfWithPages_ReturnsError()
        {
            bool isBatch = BatchCliParser.TryParse(
                new[]
                {
                    "--export-pdf",
                    "-o", @"C:\out",
                    "-p", "1",
                    @"C:\docs\sample.docx",
                },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(options);
            Assert.Contains("--export-pdf", errorMessage);
        }

        /// <summary>
        /// JPEG 形式の別名 jpg を受け付けることを検証する
        /// </summary>
        [Fact]
        public void TryParse_JpgAlias_ParsesAsJpeg()
        {
            bool isBatch = BatchCliParser.TryParse(
                new[]
                {
                    "-o", @"C:\out",
                    "-f", "jpg",
                    @"C:\docs\sample.pdf",
                },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(errorMessage);
            Assert.Equal(OutputImageFormat.Jpeg, options.Format);
        }

        /// <summary>
        /// -- 以降の引数をファイルパスとして扱うことを検証する
        /// </summary>
        [Fact]
        public void TryParse_EndOfOptionsMarker_ParsesRemainingAsFiles()
        {
            bool isBatch = BatchCliParser.TryParse(
                new[]
                {
                    "-o", @"C:\out",
                    "--",
                    @"-special-name.pdf",
                },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(errorMessage);
            Assert.Single(options.InputFiles);
            Assert.Equal(@"-special-name.pdf", options.InputFiles[0]);
        }

        /// <summary>
        /// ヘルプテキストに主要オプションが含まれることを検証する
        /// </summary>
        [Fact]
        public void GetHelpText_ContainsMainOptions()
        {
            string help = BatchCliParser.GetHelpText();

            Assert.Contains("--output", help);
            Assert.Contains("--export-pdf", help);
            Assert.Contains("--pages", help);
        }
    }
}
