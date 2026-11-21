using System;
using System.IO;
using System.Runtime.InteropServices;
using Moq;
using PdfConverter.Infrastructure;
using PdfConverter.Models;
using PdfConverter.Tests.Helpers;
using PdfConverter.ViewModels;
using Xunit;

namespace PdfConverter.Tests.Infrastructure
{
    /// <summary>
    /// <see cref="GlobalExceptionHandler"/> の動作を検証する
    /// </summary>
    [Collection(GlobalStateTestCollection.Name)]
    public sealed class GlobalExceptionHandlerTests : IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private readonly string _logDirectory;
        private readonly Action _originalShutdownAfterFatalException;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        public GlobalExceptionHandlerTests()
        {
            _logDirectory = Path.Combine(Path.GetTempPath(), "PdfConverterTests", Guid.NewGuid().ToString("N"));
            ApplicationLogger.LogDirectory = _logDirectory;
            _originalShutdownAfterFatalException = GlobalExceptionHandler.ShutdownAfterFatalException;
        }

        public void Dispose()
        {
            GlobalExceptionHandler.ShutdownAfterFatalException = _originalShutdownAfterFatalException;
            ApplicationLogger.LogDirectory = null;
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
            Assert.Contains("[Error]", content);
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

        /// <summary>
        /// メモリ不足・ネイティブ障害・明らかな状態破壊は致命的と判定することを検証する
        /// </summary>
        /// <param name="exception">判定対象の例外</param>
        [Theory]
        [MemberData(nameof(FatalExceptionCases))]
        public void IsFatalException_ReturnsTrueForFatalTypes(Exception exception)
        {
            Assert.True(GlobalExceptionHandler.IsFatalException(exception));
        }

        /// <summary>
        /// 一般的な運用時例外は継続可能と判定することを検証する
        /// </summary>
        /// <param name="exception">判定対象の例外</param>
        [Theory]
        [MemberData(nameof(RecoverableExceptionCases))]
        public void IsFatalException_ReturnsFalseForRecoverableTypes(Exception exception)
        {
            Assert.False(GlobalExceptionHandler.IsFatalException(exception));
        }

        /// <summary>
        /// AggregateException は内側の根本原因で致命判定することを検証する
        /// </summary>
        [Fact]
        public void IsFatalException_UsesInnerExceptionOfAggregate()
        {
            var aggregate = new AggregateException(new NullReferenceException("broken"));

            Assert.True(GlobalExceptionHandler.IsFatalException(aggregate));
        }

        /// <summary>
        /// null は致命的ではないと判定することを検証する
        /// </summary>
        [Fact]
        public void IsFatalException_ReturnsFalseForNull()
        {
            Assert.False(GlobalExceptionHandler.IsFatalException(null));
        }

        /// <summary>
        /// 処理中の状態を戻さない場合は、IsBusy とプログレスバーを変更せずエラーを表示することを検証する
        /// </summary>
        [Fact]
        public void ShowStatus_WithoutReset_KeepsBusyState()
        {
            var viewModel = new Mock<IMainWindowViewModel>();

            GlobalExceptionHandler.ShowStatus(viewModel.Object, "error", resetBusyState: false);

            viewModel.VerifySet(v => v.IsBusy = It.IsAny<bool>(), Times.Never);
            viewModel.VerifySet(v => v.IsProgressBarVisible = It.IsAny<bool>(), Times.Never);
            viewModel.Verify(v => v.SetStatus("error", StatusKind.Error), Times.Once);
        }

        /// <summary>
        /// 処理中の状態を戻す場合は、IsBusy とプログレスバーを解除してエラーを表示することを検証する
        /// </summary>
        [Fact]
        public void ShowStatus_WithReset_ClearsBusyState()
        {
            var viewModel = new Mock<IMainWindowViewModel>();

            GlobalExceptionHandler.ShowStatus(viewModel.Object, "error", resetBusyState: true);

            viewModel.VerifySet(v => v.IsBusy = false, Times.Once);
            viewModel.VerifySet(v => v.IsProgressBarVisible = false, Times.Once);
            viewModel.Verify(v => v.SetStatus("error", StatusKind.Error), Times.Once);
        }

        /// <summary>致命的とみなす例外のケース</summary>
        public static TheoryData<Exception> FatalExceptionCases =>
            new TheoryData<Exception>
            {
                new OutOfMemoryException(),
                new InsufficientMemoryException(),
                new AccessViolationException(),
                new SEHException(),
                new BadImageFormatException(),
                new TypeLoadException(),
                new NullReferenceException(),
                new InvalidCastException(),
                new IndexOutOfRangeException(),
                new RankException(),
                new ArrayTypeMismatchException(),
            };

        /// <summary>継続可能とみなす例外のケース</summary>
        public static TheoryData<Exception> RecoverableExceptionCases =>
            new TheoryData<Exception>
            {
                new InvalidOperationException("ui failure"),
                new ArgumentException("bad arg"),
                new ArgumentNullException("name"),
                new IOException("io failure"),
                new UnauthorizedAccessException(),
                new NotSupportedException(),
                new COMException(),
            };
    }
}
