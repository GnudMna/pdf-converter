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
        /// null を渡した場合は空文字列を返すことを検証する
        /// </summary>
        [Fact]
        public void GetUserMessage_ReturnsEmptyForNull()
        {
            Assert.Equal(string.Empty, ExceptionFormattingHelper.GetUserMessage(null));
        }
    }
}
