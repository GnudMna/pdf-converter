using System;
using System.IO;
using System.Text;
using PdfConverter.Infrastructure;
using Xunit;

namespace PdfConverter.Tests.Infrastructure
{
    /// <summary>
    /// <see cref="BatchPasswordResolver"/> のパスワード読み取りを検証する
    /// </summary>
    public class BatchPasswordResolverTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// パスワードの指定先がなければ何もしないことを検証する
        /// </summary>
        [Fact]
        public void TryResolve_NoSource_KeepsPassword()
        {
            var options = new BatchCliOptions { Password = "direct" };

            bool result = BatchPasswordResolver.TryResolve(options, NoStandardInput, NoEnvironment, out string errorMessage);

            Assert.True(result);
            Assert.Null(errorMessage);
            Assert.Equal("direct", options.Password);
        }

        /// <summary>
        /// ファイルの 1 行目を、末尾の改行を除いて前後の空白を残したまま読み取ることを検証する
        /// </summary>
        [Theory]
        [InlineData("secret\r\n", "secret")]
        [InlineData("secret\n", "secret")]
        [InlineData(" se cret \r\nnext line", " se cret ")]
        [InlineData("日本語\"pw\"", "日本語\"pw\"")]
        public void TryResolve_PasswordFile_ReadsFirstLine(string content, string expected)
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(path, content, new UTF8Encoding(false));
            try
            {
                var options = new BatchCliOptions { PasswordFilePath = path };

                bool result = BatchPasswordResolver.TryResolve(options, NoStandardInput, NoEnvironment, out string errorMessage);

                Assert.True(result);
                Assert.Null(errorMessage);
                Assert.Equal(expected, options.Password);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// パスワードファイルが存在しない・空の場合はエラーになることを検証する
        /// </summary>
        [Fact]
        public void TryResolve_PasswordFileMissingOrEmpty_ReturnsError()
        {
            string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
            string empty = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(empty, string.Empty);
            try
            {
                bool missingResult = BatchPasswordResolver.TryResolve(
                    new BatchCliOptions { PasswordFilePath = missing }, NoStandardInput, NoEnvironment, out string missingError);
                bool emptyResult = BatchPasswordResolver.TryResolve(
                    new BatchCliOptions { PasswordFilePath = empty }, NoStandardInput, NoEnvironment, out string emptyError);

                Assert.False(missingResult);
                Assert.Contains(missing, missingError);
                Assert.False(emptyResult);
                Assert.Contains("パスワードがありません", emptyError);
            }
            finally
            {
                File.Delete(empty);
            }
        }

        /// <summary>
        /// 環境変数の値をそのままパスワードとして読み取り、未設定・空はエラーになることを検証する
        /// </summary>
        [Fact]
        public void TryResolve_EnvironmentVariable_ReadsValueOrReturnsError()
        {
            var options = new BatchCliOptions { PasswordEnvironmentVariable = "PDF_PW" };
            var unset = new BatchCliOptions { PasswordEnvironmentVariable = "OTHER" };
            var emptyValue = new BatchCliOptions { PasswordEnvironmentVariable = "EMPTY" };
            Func<string, string> getter = name => name switch
            {
                "PDF_PW" => " secret ",
                "EMPTY" => string.Empty,
                _ => null,
            };

            bool result = BatchPasswordResolver.TryResolve(options, NoStandardInput, getter, out string errorMessage);
            bool unsetResult = BatchPasswordResolver.TryResolve(unset, NoStandardInput, getter, out string unsetError);
            bool emptyResult = BatchPasswordResolver.TryResolve(emptyValue, NoStandardInput, getter, out string emptyError);

            Assert.True(result);
            Assert.Null(errorMessage);
            Assert.Equal(" secret ", options.Password);
            Assert.False(unsetResult);
            Assert.Contains("OTHER", unsetError);
            Assert.False(emptyResult);
            Assert.Contains("EMPTY", emptyError);
        }

        /// <summary>
        /// 標準入力の 1 行目をパスワードとして読み取り、入力がなければエラーになることを検証する
        /// </summary>
        [Fact]
        public void TryResolve_StandardInput_ReadsFirstLineOrReturnsError()
        {
            var options = new BatchCliOptions { PasswordFromStandardInput = true };
            var noInput = new BatchCliOptions { PasswordFromStandardInput = true };

            bool result = BatchPasswordResolver.TryResolve(
                options, () => new MemoryStream(Encoding.UTF8.GetBytes("secret\r\nignored\r\n")), NoEnvironment, out string errorMessage);
            bool noInputResult = BatchPasswordResolver.TryResolve(
                noInput, () => Stream.Null, NoEnvironment, out string noInputError);

            Assert.True(result);
            Assert.Null(errorMessage);
            Assert.Equal("secret", options.Password);
            Assert.False(noInputResult);
            Assert.Contains("標準入力", noInputError);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>呼ばれてはならない標準入力の取得処理</summary>
        private static Stream NoStandardInput()
        {
            throw new InvalidOperationException("標準入力を開いてはならない");
        }

        /// <summary>呼ばれてはならない環境変数の取得処理</summary>
        private static string NoEnvironment(string name)
        {
            throw new InvalidOperationException("環境変数を読んではならない");
        }
    }
}
