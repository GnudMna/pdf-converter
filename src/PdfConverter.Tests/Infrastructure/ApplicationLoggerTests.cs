using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Infrastructure;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Infrastructure
{
    /// <summary>
    /// <see cref="ApplicationLogger"/> の動作を検証する
    /// </summary>
    [Collection(GlobalStateTestCollection.Name)]
    public sealed class ApplicationLoggerTests : IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private readonly string _logDirectory;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        public ApplicationLoggerTests()
        {
            _logDirectory = Path.Combine(Path.GetTempPath(), "PdfConverterTests", Guid.NewGuid().ToString("N"));
            ApplicationLogger.LogDirectory = _logDirectory;
            ApplicationLogger.MaxLogFileBytesForTesting = null;
        }

        public void Dispose()
        {
            ApplicationLogger.LogDirectory = null;
            ApplicationLogger.MaxLogFileBytesForTesting = null;

            if (Directory.Exists(_logDirectory))
            {
                Directory.Delete(_logDirectory, recursive: true);
            }
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/

        /// <summary>
        /// Info ログは app.log に記録されることを検証する
        /// </summary>
        [Fact]
        public void Info_WritesToAppLog()
        {
            ApplicationLogger.Info("UnitTest", "operation started");

            string logPath = Path.Combine(_logDirectory, "app.log");
            Assert.True(File.Exists(logPath));
            string content = File.ReadAllText(logPath);
            Assert.Contains("[Info]", content);
            Assert.Contains("UnitTest", content);
            Assert.Contains("operation started", content);
        }

        /// <summary>
        /// Warning ログは app.log に記録されることを検証する
        /// </summary>
        [Fact]
        public void Warning_WritesToAppLog()
        {
            ApplicationLogger.Warning("UnitTest", "cleanup failed", new IOException("locked"));

            string content = File.ReadAllText(Path.Combine(_logDirectory, "app.log"));
            Assert.Contains("[Warning]", content);
            Assert.Contains("cleanup failed", content);
            Assert.Contains(nameof(IOException), content);
        }

        /// <summary>
        /// Error ログは error.log に記録されることを検証する
        /// </summary>
        [Fact]
        public void Error_WritesToErrorLog()
        {
            ApplicationLogger.Error("UnitTest", "save failed", new InvalidOperationException("boom"));

            string logPath = Path.Combine(_logDirectory, "error.log");
            Assert.True(File.Exists(logPath));
            string content = File.ReadAllText(logPath);
            Assert.Contains("[Error]", content);
            Assert.Contains("save failed", content);
            Assert.Contains("boom", content);
        }

        /// <summary>
        /// ログファイルが上限を超えた場合にローテーションされることを検証する
        /// </summary>
        [Fact]
        public void Write_RotatesLogWhenSizeLimitExceeded()
        {
            ApplicationLogger.MaxLogFileBytesForTesting = 64;
            ApplicationLogger.Info("RotateTest", new string('x', 80));
            ApplicationLogger.Info("RotateTest", "rotated");

            string logPath = Path.Combine(_logDirectory, "app.log");
            string archivePath = Path.Combine(_logDirectory, "app.log.1");

            Assert.True(File.Exists(archivePath));
            Assert.True(File.Exists(logPath));
            Assert.Contains("xxxx", File.ReadAllText(archivePath));
            Assert.Contains("rotated", File.ReadAllText(logPath));
        }

        /// <summary>
        /// 他プロセスがログ用ミューテックスを保持している間は、書き込みを待機することを検証する
        /// </summary>
        [Fact]
        public async Task Info_WhileMutexHeldByOtherThread_WaitsUntilReleased()
        {
            string logPath = Path.Combine(_logDirectory, "app.log");
            using var held = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();

            var holder = new Thread(() =>
            {
                using var mutex = new Mutex(false, ApplicationLogger.GetMutexName(_logDirectory));
                mutex.WaitOne();
                held.Set();
                release.Wait();
                mutex.ReleaseMutex();
            });
            holder.Start();
            Assert.True(held.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

            Task writer = Task.Run(() => ApplicationLogger.Info("MutexTest", "waited"), TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            Assert.False(writer.IsCompleted);
            Assert.False(File.Exists(logPath));

            release.Set();
            await writer.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            holder.Join();
            Assert.Contains("waited", File.ReadAllText(logPath));
        }
    }
}
