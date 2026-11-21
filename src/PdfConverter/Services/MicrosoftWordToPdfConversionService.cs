using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
                token => ConvertOnStaThread(wordFilePath, _settings, token, comHolder),
                cancellationToken,
                comHolder.TryCleanup);

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

                comHolder.TryCleanup();
                cancellationToken.ThrowIfCancellationRequested();

                throw new TimeoutException("Microsoft Word による PDF 変換がタイムアウトしました。");
            }
        }

        /// <summary>
        /// STA スレッド上で Word COM を使用して PDF を生成する
        /// </summary>
        /// <param name="wordFilePath">Word ファイルの絶対パス</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <param name="comHolder">Word COM オブジェクトの参照</param>
        /// <returns>生成された PDF ファイルの絶対パス</returns>
        private static string ConvertOnStaThread(
            string wordFilePath,
            IWordToPdfConversionSettings settings,
            CancellationToken cancellationToken,
            WordComHolder comHolder)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Type wordType = Type.GetTypeFromProgID("Word.Application");
            if (wordType == null)
            {
                throw new InvalidOperationException("Microsoft Word がインストールされていません。Word → PDF 変換には Word のインストールが必要です。");
            }

            string tempDirectory = Path.Combine(Path.GetTempPath(), "PdfConverter");
            Directory.CreateDirectory(tempDirectory);
            string outputPath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.pdf");

            dynamic wordApp = null;
            dynamic document = null;

            try
            {
                wordApp = Activator.CreateInstance(wordType);
                wordApp.Visible = false;
                wordApp.DisplayAlerts = 0;

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
            finally
            {
                comHolder.TryCleanup();
            }
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
            private object _document;

            /// <summary>Word アプリケーション</summary>
            private object _wordApp;

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
            public void Set(object document, object wordApp)
            {
                lock (_sync)
                {
                    _document = document;
                    _wordApp = wordApp;
                }
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
                }
            }


            /********************************************************************************/
            /*                             プライベートメソッド                             */
            /********************************************************************************/
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
            private static void ReleaseComObject(ref object comObject, Action<dynamic> closeAction)
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
                catch
                {
                }

                try
                {
                    Marshal.FinalReleaseComObject(target);
                }
                catch
                {
                }
            }
        }
    }
}
