using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Infrastructure;

namespace PdfConverter.Services
{
    /// <summary>
    /// LibreOffice headless を使用して Word ファイルを PDF に変換するサービス
    /// </summary>
    public sealed class LibreOfficeToPdfConversionService : IWordToPdfConversionService
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>Word → PDF 変換設定</summary>
        private readonly IWordToPdfConversionSettings _settings;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定した設定を使用して LibreOffice 経由の変換を行う
        /// </summary>
        /// <param name="settings">Word → PDF 変換設定</param>
        public LibreOfficeToPdfConversionService(IWordToPdfConversionSettings settings)
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
            return Task.Run(() => ConvertInternal(wordFilePath, cancellationToken), cancellationToken);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// LibreOffice を起動して PDF を生成する
        /// </summary>
        /// <param name="wordFilePath">Word ファイルの絶対パス</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>生成された PDF ファイルの絶対パス</returns>
        private string ConvertInternal(string wordFilePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string sofficePath = LibreOfficePathHelper.Resolve(_settings.LibreOfficePath);
            string outputDirectory = PdfConverterTempDirectory.CreateTempWorkDirectory();

            try
            {
                string arguments = BuildArguments(outputDirectory, wordFilePath, _settings);
                var startInfo = new ProcessStartInfo
                {
                    FileName = sofficePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    CapturedProcessOutput capturedOutput = ProcessWaitHelper.WaitForExitWithCapturedOutput(
                        process,
                        WordToPdfConversionTimeouts.Conversion,
                        cancellationToken,
                        "LibreOffice による PDF 変換がタイムアウトしました。");

                    if (process.ExitCode != 0)
                    {
                        string errorMessage =
                            $"LibreOffice による PDF 変換に失敗しました (終了コード {process.ExitCode})。{capturedOutput.StandardError}";
                        ApplicationLogger.Error(
                            "LibreOffice",
                            $"Conversion failed (exit {process.ExitCode}). stderr: {capturedOutput.StandardError}");
                        throw new InvalidOperationException(errorMessage);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                string generatedPdfPath = Path.Combine(
                    outputDirectory,
                    Path.GetFileNameWithoutExtension(wordFilePath) + ".pdf");

                if (!File.Exists(generatedPdfPath))
                {
                    throw new InvalidOperationException("LibreOffice から PDF への変換結果ファイルを取得できませんでした。");
                }

                string finalPdfPath = PdfConverterTempDirectory.CreateTempPdfPath();
                File.Move(generatedPdfPath, finalPdfPath);
                return finalPdfPath;
            }
            finally
            {
                PdfConverterTempDirectory.TryDeleteDirectory(outputDirectory);
            }
        }

        /// <summary>
        /// LibreOffice 起動引数を組み立てる
        /// </summary>
        /// <param name="outputDirectory">出力先ディレクトリ</param>
        /// <param name="wordFilePath">Word ファイルの絶対パス</param>
        /// <param name="settings">Word → PDF 変換設定</param>
        /// <returns>起動引数</returns>
        internal static string BuildArguments(
            string outputDirectory,
            string wordFilePath,
            IWordToPdfConversionSettings settings)
        {
            string convertTo = LibreOfficePdfExportFilterBuilder.BuildConvertToArgument(settings);
            return string.Format(
                "--headless --nologo --nofirststartwizard --convert-to {0} --outdir {1} {2}",
                QuoteArgument(convertTo),
                QuoteArgument(outputDirectory),
                QuoteArgument(wordFilePath));
        }

        /// <summary>
        /// コマンドライン引数を引用符で囲む
        /// </summary>
        /// <param name="value">引数値</param>
        /// <returns>引用符で囲んだ引数</returns>
        internal static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

    }
}
