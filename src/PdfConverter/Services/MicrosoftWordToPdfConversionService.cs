using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Infrastructure;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// Microsoft Word COM を使用して Word ファイルを PDF に変換するサービス
    /// </summary>
    public class MicrosoftWordToPdfConversionService : IWordToPdfConversionService
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>Word → PDF 変換設定</summary>
        private readonly IWordToPdfConversionSettings _settings;

        /// <summary>Word の PDF 保存形式 (<c>wdExportFormatPDF</c>)</summary>
        private const int WdExportFormatPdf = 17;

        /// <summary>印刷向け最適化 (<c>wdExportOptimizeForPrint</c>)</summary>
        private const int WdExportOptimizeForPrint = 0;

        /// <summary>画面表示向け最適化 (<c>wdExportOptimizeForOnScreen</c>)</summary>
        private const int WdExportOptimizeForOnScreen = 1;

        /// <summary>しおりを出力しない (<c>wdExportCreateNoBookmarks</c>)</summary>
        private const int WdExportCreateNoBookmarks = 0;

        /// <summary>見出しからしおりを作成 (<c>wdExportCreateHeadingBookmarks</c>)</summary>
        private const int WdExportCreateHeadingBookmarks = 1;

        /// <summary>文書内のマクロをすべて無効化する (<c>msoAutomationSecurityForceDisable</c>)</summary>
        internal const int MsoAutomationSecurityForceDisable = 3;

        /// <summary>Word の正常終了 (<c>Quit</c>) を待つ上限時間。超えたら Word プロセスを強制終了する</summary>
        internal static readonly TimeSpan CleanupGracePeriod = TimeSpan.FromSeconds(10);


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定した設定を使用して Word COM 経由の変換を行う
        /// </summary>
        /// <param name="settings">Word → PDF 変換設定</param>
        public MicrosoftWordToPdfConversionService(IWordToPdfConversionSettings settings)
        {
            _settings = settings;
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public Task<string> ConvertToPdfAsync(string wordFilePath, CancellationToken cancellationToken = default)
        {
            DocumentFileHelper.ValidateWordFilePath(wordFilePath);

            var comHolder = new WordComHolder();
            Task<string> conversionTask = StaTaskRunner.RunAsync(
                token => ConvertOnStaThread(wordFilePath, _settings, comHolder, token),
                () => _ = comHolder.CleanupWithKillFallbackAsync(),
                cancellationToken);

            return WaitWithTimeoutAsync(conversionTask, comHolder, cancellationToken);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 変換処理にタイムアウトを適用する
        /// </summary>
        /// <param name="conversionTask">変換処理</param>
        /// <param name="comHolder">Word COM オブジェクトの参照</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>生成された PDF ファイルの絶対パス</returns>
        private static async Task<string> WaitWithTimeoutAsync(
            Task<string> conversionTask,
            WordComHolder comHolder,
            CancellationToken cancellationToken)
        {
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                Task delayTask = Task.Delay(WordToPdfConversionTimeouts.Conversion, timeoutCts.Token);
                Task completedTask = await Task.WhenAny(conversionTask, delayTask).ConfigureAwait(false);

                if (completedTask == conversionTask)
                {
                    timeoutCts.Cancel();
                    return await conversionTask.ConfigureAwait(false);
                }

                await comHolder.CleanupWithKillFallbackAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                throw new TimeoutException("Microsoft Word による PDF 変換がタイムアウトしました。");
            }
        }

        /// <summary>
        /// STA スレッド上で Word COM を使用して PDF を生成する
        /// </summary>
        /// <param name="wordFilePath">Word ファイルの絶対パス</param>
        /// <param name="settings">Word → PDF 変換設定</param>
        /// <param name="comHolder">Word COM オブジェクトの参照</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>生成された PDF ファイルの絶対パス</returns>
        private static string ConvertOnStaThread(
            string wordFilePath,
            IWordToPdfConversionSettings settings,
            WordComHolder comHolder,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Type? wordType = Type.GetTypeFromProgID("Word.Application");
            if (wordType == null)
            {
                throw new InvalidOperationException("Microsoft Word がインストールされていません。Word → PDF 変換には Word のインストールが必要です。");
            }

            string outputPath = PdfConverterTempDirectory.CreateTempPdfPath();

            dynamic? wordApp = null;
            dynamic? document = null;

            try
            {
                wordApp = Activator.CreateInstance(wordType)
                    ?? throw new InvalidOperationException("Microsoft Word を起動できませんでした。");

                // 以降の設定や文書オープンで例外が発生しても Word プロセスを終了できるよう、起動直後に登録する
                comHolder.Set(null, wordApp);
                comHolder.RegisterProcess(wordApp);

                wordApp.Visible = false;
                wordApp.DisplayAlerts = 0;
                DisableMacros(wordApp);

                cancellationToken.ThrowIfCancellationRequested();

                document = wordApp.Documents.Open(
                    wordFilePath,
                    ConfirmConversions: false,
                    ReadOnly: true,
                    AddToRecentFiles: false,
                    Visible: false);

                comHolder.Set(document, wordApp);

                cancellationToken.ThrowIfCancellationRequested();
                ExportAsPdf(wordApp, document, outputPath, settings);

                if (!File.Exists(outputPath))
                {
                    throw new InvalidOperationException("Word から PDF への変換結果ファイルを取得できませんでした。");
                }

                return outputPath;
            }
            catch
            {
                PdfConverterTempDirectory.TryDeleteFile(outputPath);
                throw;
            }
            finally
            {
                comHolder.TryCleanup();
            }
        }

        /// <summary>
        /// 正常終了の処理を別スレッドで実行し、上限時間内に戻らなければ強制終了する
        /// </summary>
        /// <param name="cleanup">正常終了の処理 (Word が応答しないと戻らない場合がある)</param>
        /// <param name="kill">強制終了の処理</param>
        /// <param name="gracePeriod">正常終了を待つ上限時間</param>
        /// <returns>正常終了が完了したか強制終了を試みた時点で完了するタスク</returns>
        internal static async Task RunCleanupWithKillFallbackAsync(Action cleanup, Action kill, TimeSpan gracePeriod)
        {
            Task cleanupTask = Task.Run(cleanup);
            Task completedTask = await Task.WhenAny(cleanupTask, Task.Delay(gracePeriod)).ConfigureAwait(false);

            if (completedTask != cleanupTask)
            {
                ApplicationLogger.Warning(nameof(MicrosoftWordToPdfConversionService), "Word did not quit in time. Killing the process");
                kill();
            }
        }

        /// <summary>
        /// 文書を開く前に Word のマクロ実行を無効化する
        /// </summary>
        /// <param name="wordApp">Word アプリケーション</param>
        /// <remarks>
        /// COM 経由で起動した Word はマクロ有効 (<c>msoAutomationSecurityLow</c>) が既定のため、
        /// 信頼できない .doc の AutoOpen マクロなどが実行されないよう明示的に無効化する<br/>
        /// 設定に失敗した場合は文書を開かずに例外を伝播させる
        /// </remarks>
        internal static void DisableMacros(dynamic wordApp)
        {
            wordApp.AutomationSecurity = MsoAutomationSecurityForceDisable;
        }

        /// <summary>
        /// 設定に応じて Word 文書を PDF として書き出す
        /// </summary>
        /// <param name="wordApp">Word アプリケーション</param>
        /// <param name="document">Word 文書</param>
        /// <param name="outputPath">出力先 PDF パス</param>
        /// <param name="settings">Word → PDF 変換設定</param>
        private static void ExportAsPdf(
            dynamic wordApp,
            dynamic document,
            string outputPath,
            IWordToPdfConversionSettings settings)
        {
            int optimizeFor = settings.OptimizeFor == WordToPdfOptimizeFor.Online
                ? WdExportOptimizeForOnScreen
                : WdExportOptimizeForPrint;
            int createBookmarks = settings.ExportBookmarks
                ? WdExportCreateHeadingBookmarks
                : WdExportCreateNoBookmarks;
            bool usePdfA = settings.PdfFormat == WordToPdfPdfFormat.PdfA;

            // PrintComments は利用者の Word 全体に効くグローバル設定のため、元の値を退避して書き出し後に復元する
            bool originalPrintComments = wordApp.Options.PrintComments;
            try
            {
                wordApp.Options.PrintComments = settings.ExportComments;
                TrySetShowComments(wordApp, settings.ExportComments);

                document.ExportAsFixedFormat2(
                    OutputFileName: outputPath,
                    ExportFormat: WdExportFormatPdf,
                    OpenAfterExport: false,
                    OptimizeFor: optimizeFor,
                    CreateBookmarks: createBookmarks,
                    UseISO19005_1: usePdfA);
            }
            finally
            {
                TryRestorePrintComments(wordApp, originalPrintComments);
            }
        }

        /// <summary>
        /// 退避しておいたコメント印刷設定を Word に戻す (失敗しても元の例外を隠さないよう警告のみ記録する)
        /// </summary>
        /// <param name="wordApp">Word アプリケーション</param>
        /// <param name="printComments">復元するコメント印刷設定</param>
        private static void TryRestorePrintComments(dynamic wordApp, bool printComments)
        {
            try
            {
                wordApp.Options.PrintComments = printComments;
            }
            catch (Exception ex)
            {
                ApplicationLogger.Warning(nameof(MicrosoftWordToPdfConversionService), "Failed to restore Word PrintComments option", ex);
            }
        }

        /// <summary>
        /// コメント表示を切り替える (利用できない環境では無視する)
        /// </summary>
        /// <param name="wordApp">Word アプリケーション</param>
        /// <param name="showComments">コメントを表示するかどうか</param>
        private static void TrySetShowComments(dynamic wordApp, bool showComments)
        {
            try
            {
                wordApp.ActiveWindow.View.ShowComments = showComments;
            }
            catch
            {
                ApplicationLogger.Warning(nameof(MicrosoftWordToPdfConversionService), "ShowComments is not supported in this Word version");
            }
        }


        /********************************************************************************/
        /*                                   ネスト型                                   */
        /********************************************************************************/
        /// <summary>
        /// Word COM オブジェクトの参照を保持し、キャンセル時に解放する
        /// </summary>
        private sealed class WordComHolder
        {


            /********************************************************************************/
            /*                                 ローカル変数                                 */
            /********************************************************************************/
            /// <summary>開いている Word ファイル</summary>
            private object? _document;

            /// <summary>Word アプリケーション</summary>
            private object? _wordApp;

            /// <summary>Word アプリケーションのプロセス (強制終了用。COM 呼び出しなしで操作できるよう <see cref="_sync"/> とは別に保持する)</summary>
            private Process? _wordProcess;

            /// <summary>COM 参照操作の排他制御用オブジェクト</summary>
            private readonly object _sync = new object();


            /********************************************************************************/
            /*                              パブリックメソッド                              */
            /********************************************************************************/
            /// <summary>
            /// Word COM オブジェクトの参照を登録する
            /// </summary>
            /// <param name="document">開いている Word ファイル</param>
            /// <param name="wordApp">Word アプリケーション</param>
            public void Set(object? document, object? wordApp)
            {
                lock (_sync)
                {
                    _document = document;
                    _wordApp = wordApp;
                }
            }

            /// <summary>
            /// Word アプリケーションのプロセスを特定して保持する (強制終了用)
            /// </summary>
            /// <param name="wordApp">Word アプリケーション</param>
            public void RegisterProcess(dynamic wordApp)
            {
                try
                {
                    int hwnd = wordApp.Hwnd;
                    if (GetWindowThreadProcessId(new IntPtr(hwnd), out uint processId) == 0 || processId == 0)
                    {
                        return;
                    }

                    var process = Process.GetProcessById((int)processId);

                    // プロセス ID の再利用で別のプロセスを終了しないよう、ハンドルを先に開いて保持する
                    _ = process.SafeHandle;
                    Interlocked.Exchange(ref _wordProcess, process)?.Dispose();
                }
                catch (Exception ex)
                {
                    ApplicationLogger.Warning(nameof(MicrosoftWordToPdfConversionService), "Failed to get Word process", ex);
                }
            }

            /// <summary>
            /// Word を正常終了させ、<see cref="MicrosoftWordToPdfConversionService.CleanupGracePeriod"/> 内に終わらなければプロセスを強制終了する<br/>
            /// Word が応答しない場合も呼び出し元をブロックしない
            /// </summary>
            /// <returns>非同期操作</returns>
            public Task CleanupWithKillFallbackAsync()
            {
                return RunCleanupWithKillFallbackAsync(TryCleanup, TryKillProcess, CleanupGracePeriod);
            }

            /// <summary>
            /// Word COM オブジェクトを終了して解放する
            /// </summary>
            public void TryCleanup()
            {
                lock (_sync)
                {
                    ReleaseComObject(ref _document, CloseDocument);
                    ReleaseComObject(ref _wordApp, QuitWordApplication);
                    Interlocked.Exchange(ref _wordProcess, null)?.Dispose();
                }
            }


            /********************************************************************************/
            /*                             プライベートメソッド                             */
            /********************************************************************************/
            /// <summary>
            /// Word プロセスを子プロセスも含めて強制終了する
            /// </summary>
            private void TryKillProcess()
            {
                Process? process = Interlocked.Exchange(ref _wordProcess, null);
                if (process == null)
                {
                    return;
                }

                using (process)
                {
                    ProcessWaitHelper.TryKillProcess(process);
                }
            }

            /// <summary>
            /// Word ファイルを保存せずに閉じる
            /// </summary>
            /// <param name="document">Word ファイル</param>
            private static void CloseDocument(dynamic document)
            {
                document.Close(SaveChanges: false);
            }

            /// <summary>
            /// Word アプリケーションを終了する
            /// </summary>
            /// <param name="wordApp">Word アプリケーション</param>
            private static void QuitWordApplication(dynamic wordApp)
            {
                wordApp.Quit(SaveChanges: false);
            }

            /// <summary>
            /// COM オブジェクトを終了して解放する
            /// </summary>
            /// <param name="comObject">COM オブジェクト</param>
            /// <param name="closeAction">終了処理</param>
            private static void ReleaseComObject(ref object? comObject, Action<dynamic> closeAction)
            {
                if (comObject == null)
                {
                    return;
                }

                dynamic target = comObject;
                comObject = null;

                try
                {
                    closeAction(target);
                }
                catch (Exception ex)
                {
                    ApplicationLogger.Warning(nameof(MicrosoftWordToPdfConversionService), "Failed to close Word COM object", ex);
                }

                try
                {
                    Marshal.FinalReleaseComObject(target);
                }
                catch (Exception ex)
                {
                    ApplicationLogger.Warning(nameof(MicrosoftWordToPdfConversionService), "Failed to release Word COM object", ex);
                }
            }


            /********************************************************************************/
            /*                               Win32 API 定義                                 */
            /********************************************************************************/
            [DllImport("user32.dll")]
            private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        }
    }
}
