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
            if (string.IsNullOrWhiteSpace(wordFilePath))
            {
                throw new ArgumentException("Word ファイルのパスが指定されていません。", nameof(wordFilePath));
            }

            if (!File.Exists(wordFilePath))
            {
                throw new FileNotFoundException("Word ファイルが見つかりません。", wordFilePath);
            }

            if (!DocumentFileHelper.IsWordFile(wordFilePath))
            {
                throw new ArgumentException("Word ファイル (.doc / .docx) を指定してください。", nameof(wordFilePath));
            }

            return StaTaskRunner.RunAsync(
                token => ConvertOnStaThread(wordFilePath, token),
                cancellationToken);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// STAスレッド上でWord COMを使用してPDFを生成する
        /// </summary>
        /// <param name="wordFilePath">Wordファイルの絶対パス</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>生成されたPDFファイルの絶対パス</returns>
        private static string ConvertOnStaThread(string wordFilePath, CancellationToken cancellationToken)
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
                if (document != null)
                {
                    try
                    {
                        document.Close(SaveChanges: false);
                    }
                    catch
                    {
                    }

                    Marshal.FinalReleaseComObject(document);
                }

                if (wordApp != null)
                {
                    try
                    {
                        wordApp.Quit(SaveChanges: false);
                    }
                    catch
                    {
                    }

                    Marshal.FinalReleaseComObject(wordApp);
                }
            }
        }
    }
}
