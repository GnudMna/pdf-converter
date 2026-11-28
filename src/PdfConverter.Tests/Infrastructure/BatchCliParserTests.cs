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
        /// --password の値を、別引数と --password=value のどちらの形式でも読み取ることを検証する
        /// </summary>
        [Fact]
        public void TryParse_PasswordOption_ParsesValue()
        {
            bool isBatch = BatchCliParser.TryParse(
                new[] { "-o", @"C:\out", "--password", "secret", "a.pdf" },
                out BatchCliOptions separated,
                out string separatedError);
            bool isBatchInline = BatchCliParser.TryParse(
                new[] { "-o", @"C:\out", "--password=-a=b", "a.pdf" },
                out BatchCliOptions inline,
                out string inlineError);

            Assert.True(isBatch);
            Assert.Null(separatedError);
            Assert.Equal("secret", separated.Password);
            Assert.True(isBatchInline);
            Assert.Null(inlineError);
            Assert.Equal("-a=b", inline.Password);
        }

        /// <summary>
        /// --password の値の前後の空白と二重引用符を取り除かないことを検証する
        /// </summary>
        [Theory]
        [InlineData(" secret ")]
        [InlineData("\"secret\"")]
        [InlineData("  ")]
        public void TryParse_PasswordWithSpacesOrQuotes_KeepsValue(string password)
        {
            bool isBatch = BatchCliParser.TryParse(
                new[] { "-o", @"C:\out", "--password", password, "a.pdf" },
                out BatchCliOptions separated,
                out string separatedError);
            bool isBatchInline = BatchCliParser.TryParse(
                new[] { "-o", @"C:\out", "--password=" + password, "a.pdf" },
                out BatchCliOptions inline,
                out string inlineError);

            Assert.True(isBatch);
            Assert.Null(separatedError);
            Assert.Equal(password, separated.Password);
            Assert.True(isBatchInline);
            Assert.Null(inlineError);
            Assert.Equal(password, inline.Password);
        }

        /// <summary>
        /// --password の次の引数が - や / で始まっていても、パスワードとして読み取ることを検証する
        /// </summary>
        [Theory]
        [InlineData("-abc")]
        [InlineData("/secret")]
        public void TryParse_PasswordStartingWithSwitchChar_ParsesAsValue(string password)
        {
            bool isBatch = BatchCliParser.TryParse(
                new[] { "--password", password, "-o", @"C:\out", "a.pdf" },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(errorMessage);
            Assert.Equal(password, options.Password);
            Assert.Equal(@"C:\out", options.OutputPath);
            Assert.Single(options.InputFiles);
        }

        /// <summary>
        /// --password に空の値を指定した場合はエラーになることを検証する
        /// </summary>
        [Fact]
        public void TryParse_PasswordEmptyValue_ReturnsError()
        {
            bool isBatch = BatchCliParser.TryParse(
                new[] { "-o", @"C:\out", "--password=", "a.pdf" },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(options);
            Assert.Contains("--password", errorMessage);
        }

        /// <summary>
        /// --password-file / --password-env / --password-stdin の指定を読み取ることを検証する
        /// </summary>
        [Fact]
        public void TryParse_PasswordSourceOptions_ParsesEachSource()
        {
            bool isFile = BatchCliParser.TryParse(
                new[] { "-o", @"C:\out", "--password-file", @"C:\pw.txt", "a.pdf" },
                out BatchCliOptions file,
                out string fileError);
            bool isEnv = BatchCliParser.TryParse(
                new[] { "-o", @"C:\out", "--password-env=PDF_PW", "a.pdf" },
                out BatchCliOptions env,
                out string envError);
            bool isStdin = BatchCliParser.TryParse(
                new[] { "-o", @"C:\out", "--password-stdin", "a.pdf" },
                out BatchCliOptions stdin,
                out string stdinError);

            Assert.True(isFile);
            Assert.Null(fileError);
            Assert.Equal(@"C:\pw.txt", file.PasswordFilePath);
            Assert.True(isEnv);
            Assert.Null(envError);
            Assert.Equal("PDF_PW", env.PasswordEnvironmentVariable);
            Assert.True(isStdin);
            Assert.Null(stdinError);
            Assert.True(stdin.PasswordFromStandardInput);
            Assert.Null(stdin.Password);
        }

        /// <summary>
        /// パスワードの指定方法を複数同時に指定した場合はエラーになることを検証する
        /// </summary>
        [Theory]
        [InlineData("--password", "secret", "--password-stdin")]
        [InlineData("--password-file", "pw.txt", "--password-env=PDF_PW")]
        public void TryParse_MultiplePasswordSources_ReturnsError(string first, string second, string third)
        {
            bool isBatch = BatchCliParser.TryParse(
                new[] { "-o", @"C:\out", first, second, third, "a.pdf" },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(options);
            Assert.Contains("--password-file", errorMessage);
        }

        /// <summary>
        /// --password-file に値がない場合はエラーになることを検証する
        /// </summary>
        [Fact]
        public void TryParse_PasswordFileWithoutValue_ReturnsError()
        {
            bool isBatch = BatchCliParser.TryParse(
                new[] { "-o", @"C:\out", "a.pdf", "--password-file" },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(options);
            Assert.Contains("--password-file", errorMessage);
        }

        /// <summary>
        /// --password に値がない場合はエラーになることを検証する
        /// </summary>
        [Fact]
        public void TryParse_PasswordWithoutValue_ReturnsError()
        {
            bool isBatch = BatchCliParser.TryParse(
                new[] { "-o", @"C:\out", "a.pdf", "--password" },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(options);
            Assert.Contains("--password", errorMessage);
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

        /// <summary>
        /// ヘルプテキストでコンソール用の起動プログラム pdfconv.exe を案内することを検証する
        /// </summary>
        [Fact]
        public void GetHelpText_MentionsConsoleLauncher()
        {
            string help = BatchCliParser.GetHelpText();

            Assert.Contains("pdfconv.exe", help);
        }

        /// <summary>
        /// --jpeg-quality の値を別引数と --jpeg-quality=value のどちらの形式でも読み取ることを検証する
        /// </summary>
        [Theory]
        [InlineData("--jpeg-quality", "70")]
        [InlineData("--jpeg-quality=70", null)]
        public void TryParse_JpegQuality_ParsesValue(string first, string second)
        {
            string[] args = second == null
                ? new[] { "-o", @"C:\out", "-f", "jpeg", first, @"C:\docs\sample.pdf" }
                : new[] { "-o", @"C:\out", "-f", "jpeg", first, second, @"C:\docs\sample.pdf" };

            bool isBatch = BatchCliParser.TryParse(args, out BatchCliOptions options, out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(errorMessage);
            Assert.Equal(70, options.JpegQuality);
        }

        /// <summary>
        /// 範囲外・整数でない --jpeg-quality はエラーになることを検証する
        /// </summary>
        [Theory]
        [InlineData("0")]
        [InlineData("101")]
        [InlineData("abc")]
        public void TryParse_InvalidJpegQuality_ReturnsError(string quality)
        {
            bool isBatch = BatchCliParser.TryParse(
                new[] { "-o", @"C:\out", "--jpeg-quality", quality, @"C:\docs\sample.pdf" },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(options);
            Assert.NotNull(errorMessage);
        }

        /// <summary>
        /// JPEG 以外の形式を明示した --jpeg-quality はエラーになることを検証する
        /// </summary>
        [Fact]
        public void TryParse_JpegQualityWithPng_ReturnsError()
        {
            bool isBatch = BatchCliParser.TryParse(
                new[] { "-o", @"C:\out", "-f", "png", "--jpeg-quality", "80", @"C:\docs\sample.pdf" },
                out BatchCliOptions options,
                out string errorMessage);

            Assert.True(isBatch);
            Assert.Null(options);
            Assert.NotNull(errorMessage);
        }
    }
}
