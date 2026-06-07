using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PdfConverter.Services
{
    /// <summary>
    /// Microsoft Word COMを使用してWordファイルをPDFに変換するサービス
    /// </summary>
    public class MicrosoftWordToPdfConversionService : IWordToPdfConversionService
    {
        /********************************************************************************/
        /*                                 ローカル定数                                 */
        /********************************************************************************/
        /// <summary>WordのPDF保存形式 (<c>wdFormatPDF</c>)</summary>
        private const int WdFormatPdf = 17;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public Task<string> ConvertToPdfAsync(string wordFilePath, CancellationToken cancellationToken = default)
        {
            DocumentFileHelper.ValidateWordFilePath(wordFilePath);

            var comHolder = new WordComHolder();
            Task<string> conversionTask = StaTaskRunner.RunAsync(
                token => ConvertOnStaThread(wordFilePath, token, comHolder),
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
        /// <returns>生成されたPDFファイルの絶対パス</returns>
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
        /// STAスレッド上でWord COMを使用してPDFを生成する
        /// </summary>
        /// <param name="wordFilePath">Wordファイルの絶対パス</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <param name="comHolder">Word COM オブジェクトの参照</param>
        /// <returns>生成されたPDFファイルの絶対パス</returns>
        private static string ConvertOnStaThread(
            string wordFilePath,
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
                document.SaveAs2(outputPath, WdFormatPdf);

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


        /********************************************************************************/
        /*                                   ネスト型                                   */
        /********************************************************************************/
        /// <summary>
        /// Word COMオブジェクトの参照を保持し、キャンセル時に解放する
        /// </summary>
        private sealed class WordComHolder
        {
            /********************************************************************************/
            /*                                 ローカル変数                                 */
            /********************************************************************************/
            /// <summary>開いているWordファイル</summary>
            private object _document;

            /// <summary>Wordアプリケーション</summary>
            private object _wordApp;

            /// <summary>COM参照操作の排他制御用オブジェクト</summary>
            private readonly object _sync = new object();


            /********************************************************************************/
            /*                              パブリックメソッド                              */
            /********************************************************************************/
            /// <summary>
            /// Word COMオブジェクトの参照を登録する
            /// </summary>
            /// <param name="document">開いているWordファイル</param>
            /// <param name="wordApp">Wordアプリケーション</param>
            public void Set(object document, object wordApp)
            {
                lock (_sync)
                {
                    _document = document;
                    _wordApp = wordApp;
                }
            }

            /// <summary>
            /// Word COMオブジェクトを終了して解放する
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
            /// Wordファイルを保存せずに閉じる
            /// </summary>
            /// <param name="document">Wordファイル</param>
            private static void CloseDocument(dynamic document)
            {
                document.Close(SaveChanges: false);
            }

            /// <summary>
            /// Wordアプリケーションを終了する
            /// </summary>
            /// <param name="wordApp">Wordアプリケーション</param>
            private static void QuitWordApplication(dynamic wordApp)
            {
                wordApp.Quit(SaveChanges: false);
            }

            /// <summary>
            /// COMオブジェクトを終了して解放する
            /// </summary>
            /// <param name="comObject">COMオブジェクト</param>
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
