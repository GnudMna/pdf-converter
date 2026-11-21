using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PdfConverter.Services
{
    /// <summary>
    /// LibreOffice headlessを使用してWordファイルをPDFに変換するサービス
    /// </summary>
    public sealed class LibreOfficeToPdfConversionService : IWordToPdfConversionService
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>Word → PDF変換設定</summary>
        private readonly IWordToPdfConversionSettings _settings;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定した設定を使用してLibreOffice経由の変換を行う
        /// </summary>
        /// <param name="settings">Word → PDF変換設定</param>
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
        /// LibreOfficeを起動してPDFを生成する
        /// </summary>
        /// <param name="wordFilePath">Wordファイルの絶対パス</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>生成されたPDFファイルの絶対パス</returns>
        private string ConvertInternal(string wordFilePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string sofficePath = LibreOfficePathHelper.Resolve(_settings.LibreOfficePath);
            string outputDirectory = Path.Combine(Path.GetTempPath(), "PdfConverter", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputDirectory);

            try
            {
                string arguments = BuildArguments(outputDirectory, wordFilePath);
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
                    ProcessWaitHelper.WaitForExit(
                        process,
                        WordToPdfConversionTimeouts.Conversion,
                        cancellationToken,
                        "LibreOffice による PDF 変換がタイムアウトしました。");

                    if (process.ExitCode != 0)
                    {
                        string error = ReadAvailableStream(process.StandardError);
                        throw new InvalidOperationException(
                            $"LibreOffice による PDF 変換に失敗しました (終了コード {process.ExitCode})。{error}");
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

                string finalPdfPath = Path.Combine(Path.GetTempPath(), "PdfConverter", $"{Guid.NewGuid():N}.pdf");
                Directory.CreateDirectory(Path.GetDirectoryName(finalPdfPath));
                File.Move(generatedPdfPath, finalPdfPath);
                return finalPdfPath;
            }
            finally
            {
                TryDeleteDirectory(outputDirectory);
            }
        }

        /// <summary>
        /// LibreOffice起動引数を組み立てる
        /// </summary>
        /// <param name="outputDirectory">出力先ディレクトリ</param>
        /// <param name="wordFilePath">Wordファイルの絶対パス</param>
        /// <returns>起動引数</returns>
        private static string BuildArguments(string outputDirectory, string wordFilePath)
        {
            return string.Format(
                "--headless --nologo --nofirststartwizard --convert-to pdf --outdir {0} {1}",
                QuoteArgument(outputDirectory),
                QuoteArgument(wordFilePath));
        }

        /// <summary>
        /// コマンドライン引数を引用符で囲む
        /// </summary>
        /// <param name="value">引数値</param>
        /// <returns>引用符で囲んだ引数</returns>
        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        /// <summary>
        /// 利用可能な標準出力/エラー文字列を読み取る
        /// </summary>
        /// <param name="reader">読み取り元</param>
        /// <returns>読み取った文字列</returns>
        private static string ReadAvailableStream(StreamReader reader)
        {
            if (reader == null)
            {
                return string.Empty;
            }

            try
            {
                return reader.ReadToEnd().Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 一時ディレクトリを削除する
        /// </summary>
        /// <param name="directoryPath">削除対象ディレクトリ</param>
        private static void TryDeleteDirectory(string directoryPath)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
