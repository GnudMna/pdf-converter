using System;
using System.IO;
using FluentAssertions;
using PdfConverter.Infrastructure;
using Xunit;

namespace PdfConverter.Tests.Infrastructure
{
    /// <summary>
    /// <see cref="GlobalExceptionHandler"/> の動作を検証する
    /// </summary>
    public class GlobalExceptionHandlerTests : IDisposable
    {
        private readonly string _logDirectory;

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

        /// <summary>
        /// キャンセル例外は報告されず、ログも出力されないことを検証する
        /// </summary>
        [Fact]
        public void Report_IgnoresCancellation()
        {
            GlobalExceptionHandler.Report(new OperationCanceledException(), "Test");

            Directory.Exists(_logDirectory).Should().BeFalse();
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
            File.Exists(logPath).Should().BeTrue();
            string content = File.ReadAllText(logPath);
            content.Should().Contain("UnitTest");
            content.Should().Contain("test failure");
            content.Should().Contain(nameof(InvalidOperationException));
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
            content.Should().Contain("inner message");
            content.Should().Contain(nameof(ArgumentException));
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
