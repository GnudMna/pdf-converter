using PdfConverter.Infrastructure;
using Xunit;

namespace PdfConverter.Tests.Infrastructure
{
    /// <summary>
    /// <see cref="PortableUpdateCommand"/> の起動引数の組み立てと解析を検証する
    /// </summary>
    public sealed class PortableUpdateCommandTests
    {
        /// <summary>
        /// 組み立てた起動引数を、空白を含むパスもそのまま解析できることを検証する
        /// </summary>
        [Fact]
        public void TryParse_BuiltArguments_RoundTrips()
        {
            string target = @"D:\Tools\PDF Converter";

            string[] args = [.. PortableUpdateCommand.BuildArguments(target, 1234)];
            bool parsed = PortableUpdateCommand.TryParse(args, out PortableUpdateOptions options);

            Assert.True(parsed);
            Assert.NotNull(options);
            Assert.Equal(target, options.TargetDirectory);
            Assert.Equal(1234, options.WaitProcessId);
        }

        /// <summary>
        /// 更新処理の指定ではない起動引数 (ファイルの指定・バッチ変換) は対象外とすることを検証する
        /// </summary>
        [Theory]
        [InlineData]
        [InlineData(@"C:\docs\report.pdf")]
        [InlineData("-o", @"C:\out", "report.pdf")]
        public void TryParse_OtherArguments_ReturnsFalse(params string[] args)
        {
            Assert.False(PortableUpdateCommand.TryParse(args, out PortableUpdateOptions options));
            Assert.Null(options);
        }

        /// <summary>
        /// 更新処理の指定で更新先が無い・相対パスのときは、対象としたうえで指定を返さないことを検証する
        /// </summary>
        [Theory]
        [InlineData("--apply-portable-update")]
        [InlineData("--apply-portable-update", "--target", @"relative\dir")]
        [InlineData("--apply-portable-update", "--wait-pid", "10")]
        public void TryParse_InvalidTarget_ReturnsTrueWithoutOptions(params string[] args)
        {
            Assert.True(PortableUpdateCommand.TryParse(args, out PortableUpdateOptions options));
            Assert.Null(options);
        }

        /// <summary>
        /// プロセス ID が数値でないときは、待たずに更新する指定として扱うことを検証する
        /// </summary>
        [Fact]
        public void TryParse_InvalidPid_IgnoresPid()
        {
            string[] args = ["--apply-portable-update", "--target", @"D:\Tools\PDF Converter", "--wait-pid", "abc"];

            Assert.True(PortableUpdateCommand.TryParse(args, out PortableUpdateOptions options));
            Assert.NotNull(options);
            Assert.Null(options.WaitProcessId);
        }
    }
}
