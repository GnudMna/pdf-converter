using System;
using System.IO;
using PdfConverter.Infrastructure;
using Xunit;

namespace PdfConverter.Tests.Infrastructure
{
    /// <summary>
    /// <see cref="GlobalExceptionHandler"/> の動作を検証する
    /// </summary>
    public class GlobalExceptionHandlerTests : IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private readonly string _logDirectory;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        public GlobalExceptionHandlerTests()
        {
            _logDirectory = Path.Combine(Path.GetTempPath(), "PdfConverterTests", Guid.NewGuid().ToString("N"));
            GlobalExceptionHandler.LogDirectory = _logDirectory;
        }

        public void Dispose()
        {
            GlobalExceptionHandler.LogDirectory = null;
            GlobalExceptionHandler.Unregister();

            if (Directory.Exists(_logDirectory))
            {
                Directory.Delete(_logDirectory, recursive: true);
            }
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/

        /// <summary>
        /// キャンセル例外は報告されず、ログも出力されないことを検証する
        /// </summary>
        [Fact]
        public void Report_IgnoresCancellation()
        {
            GlobalExceptionHandler.Report(new OperationCanceledException(), "Test");

            Assert.False(Directory.Exists(_logDirectory));
        }

        /// <summary>
        /// 通常の例外はログファイルへ記録されることを検証する
        /// </summary>
        [Fact]
        public void Report_WritesExceptionToLogFile()
        {
            var exception = new InvalidOperationException("test failure");

            GlobalExceptionHandler.Report(exception, "UnitTest", ExceptionReportKind.Background);

            string logPath = Path.Combine(_logDirectory, "error.log");
            Assert.True(File.Exists(logPath));
            string content = File.ReadAllText(logPath);
            Assert.Contains("UnitTest", content);
            Assert.Contains("test failure", content);
            Assert.Contains(nameof(InvalidOperationException), content);
        }

        /// <summary>
        /// AggregateException は根本原因をログへ記録することを検証する
        /// </summary>
        [Fact]
        public void Report_FlattensAggregateException()
        {
            var inner = new ArgumentException("inner message");
            var aggregate = new AggregateException(inner);

            GlobalExceptionHandler.Report(aggregate, "AggregateTest");

            string content = File.ReadAllText(Path.Combine(_logDirectory, "error.log"));
            Assert.Contains("inner message", content);
            Assert.Contains(nameof(ArgumentException), content);
        }

        /// <summary>
        /// Register は重複登録しても例外にならないことを検証する
        /// </summary>
        [Fact]
        public void Register_IsIdempotent()
        {
            GlobalExceptionHandler.Register();
            GlobalExceptionHandler.Register();

            GlobalExceptionHandler.Unregister();
        }
    }
}
