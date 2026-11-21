using System;
using System.IO;
using PdfConverter.Infrastructure;
using Xunit;

namespace PdfConverter.Tests.Infrastructure
{
    /// <summary>
    /// <see cref="ExceptionFormattingHelper"/> の動作を検証する
    /// </summary>
    public class ExceptionFormattingHelperTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/

        /// <summary>
        /// 単一例外はそのメッセージを返すことを検証する
        /// </summary>
        [Fact]
        public void GetUserMessage_ReturnsRootMessage()
        {
            string message = ExceptionFormattingHelper.GetUserMessage(new InvalidOperationException("root failure"));

            Assert.Equal("root failure", message);
        }

        /// <summary>
        /// 内部例外のメッセージが異なる場合は括弧付きで付加することを検証する
        /// </summary>
        [Fact]
        public void GetUserMessage_AppendsDistinctInnerMessage()
        {
            var exception = new InvalidOperationException(
                "conversion failed",
                new IOException("disk full"));

            string message = ExceptionFormattingHelper.GetUserMessage(exception);

            Assert.Equal("conversion failed (disk full)", message);
        }

        /// <summary>
        /// AggregateException は根本原因のメッセージを返すことを検証する
        /// </summary>
        [Fact]
        public void GetUserMessage_FlattensAggregateException()
        {
            var aggregate = new AggregateException(new ArgumentException("inner reason"));

            string message = ExceptionFormattingHelper.GetUserMessage(aggregate);

            Assert.Equal("inner reason", message);
        }

        /// <summary>
        /// ArgumentException が自動付加する引数名を取り除くことを検証する
        /// </summary>
        [Fact]
        public void GetUserMessage_RemovesParameterNameFromArgumentException()
        {
            var exception = new ArgumentException("パスが不正です。", "path");

            string message = ExceptionFormattingHelper.GetUserMessage(exception);

            Assert.Equal("パスが不正です。", message);
        }

        /// <summary>
        /// ArgumentOutOfRangeException が付加する引数名と実際の値を取り除くことを検証する
        /// </summary>
        [Fact]
        public void GetUserMessage_RemovesParameterNameAndActualValueFromArgumentOutOfRangeException()
        {
            var exception = new ArgumentOutOfRangeException("pageIndex", 5, "ページインデックスが範囲外です。");

            string message = ExceptionFormattingHelper.GetUserMessage(exception);

            Assert.Equal("ページインデックスが範囲外です。", message);
        }

        /// <summary>
        /// 内部例外の引数名も取り除いて付加することを検証する
        /// </summary>
        [Fact]
        public void GetUserMessage_RemovesParameterNameFromInnerException()
        {
            var exception = new InvalidOperationException(
                "変換に失敗しました。",
                new ArgumentException("入力が空です。", "input"));

            string message = ExceptionFormattingHelper.GetUserMessage(exception);

            Assert.Equal("変換に失敗しました。 (入力が空です。)", message);
        }

        /// <summary>
        /// null を渡した場合は空文字列を返すことを検証する
        /// </summary>
        [Fact]
        public void GetUserMessage_ReturnsEmptyForNull()
        {
            Assert.Equal(string.Empty, ExceptionFormattingHelper.GetUserMessage(null));
        }
    }
}
