using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="MicrosoftWordToPdfConversionService"/> の入力検証とキャンセル処理を検証する
    /// </summary>
    public class MicrosoftWordToPdfConversionServiceTests
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private readonly MicrosoftWordToPdfConversionService _service =
            new MicrosoftWordToPdfConversionService(WordToPdfConversionSettingsTestHelper.Create().Object);


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/

        /// <summary>
        /// 空のパスを指定した場合に ArgumentException がスローされることを検証する
        /// </summary>
        [Fact]
        public async Task ConvertToPdfAsync_EmptyPath_ThrowsArgumentException()
        {
            Func<Task> act = () => _service.ConvertToPdfAsync(string.Empty);

            await Assert.ThrowsAsync<ArgumentException>(act);
        }

        /// <summary>
        /// 存在しない Word ファイルを指定した場合に FileNotFoundException がスローされることを検証する
        /// </summary>
        [Fact]
        public async Task ConvertToPdfAsync_MissingFile_ThrowsFileNotFoundException()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx");

            Func<Task> act = () => _service.ConvertToPdfAsync(missingPath);

            await Assert.ThrowsAsync<FileNotFoundException>(act);
        }

        /// <summary>
        /// Word 以外の拡張子を指定した場合に ArgumentException がスローされることを検証する
        /// </summary>
        [Fact]
        public async Task ConvertToPdfAsync_NotWordFile_ThrowsArgumentException()
        {
            string path = Path.GetTempFileName();

            try
            {
                Func<Task> act = () => _service.ConvertToPdfAsync(path);

                var ex = await Assert.ThrowsAsync<ArgumentException>(act);
                Assert.Contains("Word ファイル", ex.Message);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// キャンセル済みトークンでは OperationCanceledException がスローされることを検証する
        /// </summary>
        [Fact]
        public async Task ConvertToPdfAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            string wordPath = CreateTempWordFile();

            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    cts.Cancel();
                    Func<Task> act = () => _service.ConvertToPdfAsync(wordPath, cts.Token);
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
                }
            }
            finally
            {
                File.Delete(wordPath);
            }
        }

        /// <summary>
        /// Microsoft Word が未インストールの環境では InvalidOperationException がスローされることを検証する
        /// </summary>
        [Fact]
        public async Task ConvertToPdfAsync_WhenWordNotInstalled_ThrowsInvalidOperationException()
        {
            if (Type.GetTypeFromProgID("Word.Application") != null)
            {
                return;
            }

            string wordPath = CreateTempWordFile();

            try
            {
                Func<Task> act = () => _service.ConvertToPdfAsync(wordPath);

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
                Assert.Contains("Microsoft Word", ex.Message);
            }
            finally
            {
                File.Delete(wordPath);
            }
        }

        /// <summary>
        /// 文書を開く前のマクロ無効化で Word の AutomationSecurity が ForceDisable に設定されることを検証する
        /// </summary>
        [Fact]
        public void DisableMacros_SetsAutomationSecurityToForceDisable()
        {
            var wordApp = new FakeWordApplication();

            MicrosoftWordToPdfConversionService.DisableMacros(wordApp);

            Assert.Equal(MicrosoftWordToPdfConversionService.MsoAutomationSecurityForceDisable, wordApp.AutomationSecurity);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        private static string CreateTempWordFile()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.docx");
            File.WriteAllText(path, "placeholder");
            return path;
        }


        /********************************************************************************/
        /*                                   ネスト型                                   */
        /********************************************************************************/
        /// <summary>
        /// dynamic 経由で設定されるプロパティを記録する Word アプリケーションの代替
        /// </summary>
        public sealed class FakeWordApplication
        {
            /// <summary>マクロのセキュリティレベル</summary>
            public int AutomationSecurity { get; set; }
        }

        /// <summary>
        /// 正常終了が上限時間内に完了した場合は強制終了しないことを検証する
        /// </summary>
        [Fact]
        public async Task RunCleanupWithKillFallbackAsync_CleanupCompletes_DoesNotKill()
        {
            bool killed = false;

            await MicrosoftWordToPdfConversionService.RunCleanupWithKillFallbackAsync(
                () => { },
                () => killed = true,
                TimeSpan.FromSeconds(30));

            Assert.False(killed);
        }

        /// <summary>
        /// 正常終了が戻らない場合に、上限時間後に強制終了して呼び出し元へ制御を戻すことを検証する
        /// </summary>
        [Fact]
        public async Task RunCleanupWithKillFallbackAsync_CleanupHangs_KillsAfterGracePeriod()
        {
            using var release = new ManualResetEventSlim(false);
            bool killed = false;

            try
            {
                await MicrosoftWordToPdfConversionService.RunCleanupWithKillFallbackAsync(
                    () => release.Wait(),
                    () => killed = true,
                    TimeSpan.FromMilliseconds(50));

                Assert.True(killed);
            }
            finally
            {
                release.Set();
            }
        }
    }
}
