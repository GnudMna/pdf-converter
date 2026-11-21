using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Infrastructure;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// バッチ / ヘッドレスモードでの PDF / Word 変換を実行する
    /// </summary>
    public sealed class BatchExportService : IBatchExportService
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>PDF 変換サービス</summary>
        private readonly IPdfConversionService _pdfConversionService;

        /// <summary>入力ドキュメントから PDF ソースを提供するサービス</summary>
        private readonly IDocumentPdfSourceService _documentPdfSourceService;

        /// <summary>保存済みの画像出力設定</summary>
        private readonly IImageExportSettings _imageExportSettings;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定したサービスを使用してバッチ変換を実行する
        /// </summary>
        /// <param name="pdfConversionService">PDF 変換サービス</param>
        /// <param name="documentPdfSourceService">入力ドキュメントから PDF ソースを提供するサービス</param>
        /// <param name="imageExportSettings">保存済みの画像出力設定</param>
        public BatchExportService(
            IPdfConversionService pdfConversionService,
            IDocumentPdfSourceService documentPdfSourceService,
            IImageExportSettings imageExportSettings)
        {
            _pdfConversionService = pdfConversionService;
            _documentPdfSourceService = documentPdfSourceService;
            _imageExportSettings = imageExportSettings;
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        /// <remarks>
        /// 呼び出し元 (App 起動時の UI スレッド) は完了を同期的に待つため、
        /// 処理全体をスレッドプール上で実行し、継続が呼び出し元の SynchronizationContext へ戻ってデッドロックしないようにする
        /// </remarks>
        public async Task<int> RunAsync(BatchCliOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            return await Task.Run(() => RunCoreAsync(options, cancellationToken), CancellationToken.None)
                .ConfigureAwait(false);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>入力ファイルを順に変換する</summary>
        /// <param name="options">バッチオプション</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>終了コード</returns>
        private async Task<int> RunCoreAsync(BatchCliOptions options, CancellationToken cancellationToken)
        {
            if (options.ShowHelp)
            {
                BatchConsoleHelper.WriteLine(BatchCliParser.GetHelpText());
                return BatchCliParser.SuccessExitCode;
            }

            bool hasFailure = false;

            foreach (string inputPath in options.InputFiles)
            {
                string normalizedPath;
                try
                {
                    normalizedPath = DocumentFileHelper.ValidateAndNormalizeSupportedDocumentPath(inputPath);
                }
                catch (Exception ex)
                {
                    hasFailure = true;
                    WriteError($"入力ファイルをスキップします: {inputPath} ({ExceptionFormattingHelper.GetUserMessage(ex)})");
                    continue;
                }

                try
                {
                    if (options.ExportPdfOnly)
                    {
                        await ExportPdfAsync(options, normalizedPath, cancellationToken);
                    }
                    else
                    {
                        await ExportImagesAsync(options, normalizedPath, cancellationToken);
                    }

                    if (!options.Quiet)
                    {
                        BatchConsoleHelper.WriteLine($"完了: {normalizedPath}");
                    }
                }
                catch (Exception ex) when (CancellationExceptionHelper.IsOrContainsCancellation(ex))
                {
                    WriteError("処理はキャンセルされました。");
                    return BatchCliParser.RuntimeErrorExitCode;
                }
                catch (Exception ex)
                {
                    hasFailure = true;
                    ApplicationLogger.Error("Batch", $"Batch export failed: {normalizedPath}", ex);
                    WriteError($"エラー: {normalizedPath} ({ExceptionFormattingHelper.GetUserMessage(ex)})");
                }
            }

            return hasFailure ? BatchCliParser.RuntimeErrorExitCode : BatchCliParser.SuccessExitCode;
        }

        /// <summary>Word 文書を PDF として出力する</summary>
        /// <param name="options">バッチオプション</param>
        /// <param name="sourcePath">入力ファイルの絶対パス</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        private async Task ExportPdfAsync(BatchCliOptions options, string sourcePath, CancellationToken cancellationToken)
        {
            if (!DocumentFileHelper.IsWordFile(sourcePath))
            {
                throw new InvalidOperationException("PDF ファイルには --export-pdf は使用できません。");
            }

            string destinationPath = ResolvePdfDestinationPath(options, sourcePath);
            string? destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            if (File.Exists(destinationPath) && !options.OverwriteExisting)
            {
                throw new IOException($"出力先ファイルが既に存在します: {destinationPath} (--overwrite で上書きできます)");
            }

            if (!options.Quiet)
            {
                BatchConsoleHelper.WriteLine($"PDF 変換中: {Path.GetFileName(sourcePath)}");
            }

            string pdfPath = await _documentPdfSourceService.GetPdfPathAsync(sourcePath, cancellationToken);
            File.Copy(pdfPath, destinationPath, overwrite: options.OverwriteExisting);
            ApplicationLogger.Info("Batch", $"PDF export completed: {destinationPath}");
        }

        /// <summary>ドキュメントのページを画像として出力する</summary>
        /// <param name="options">バッチオプション</param>
        /// <param name="sourcePath">入力ファイルの絶対パス</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        private async Task ExportImagesAsync(BatchCliOptions options, string sourcePath, CancellationToken cancellationToken)
        {
            OutputImageFormat format = options.Format ?? _imageExportSettings.OutputImageFormat;
            ResolutionMode resolutionMode = options.ResolutionMode ?? _imageExportSettings.ResolutionMode;
            string resolutionValue = options.ResolutionValue ?? _imageExportSettings.ResolutionValue;
            bool preserveTransparency = options.PreserveTransparency
                ?? ImageExportSettings.ResolvePreserveTransparency(format, _imageExportSettings.PreserveTransparency);

            if (!ResolutionValueParser.TryParse(resolutionMode, resolutionValue, out double parsedResolution, out string? resolutionError))
            {
                throw new InvalidOperationException(resolutionError);
            }

            string outputRoot = Path.GetFullPath(options.OutputPath.Trim().Trim('"'));
            Directory.CreateDirectory(outputRoot);

            string outputFolder = options.InputFiles.Count > 1
                ? Path.Combine(outputRoot, SanitizeDirectoryName(Path.GetFileNameWithoutExtension(sourcePath)))
                : outputRoot;
            Directory.CreateDirectory(outputFolder);

            if (!options.Quiet)
            {
                BatchConsoleHelper.WriteLine($"変換中: {Path.GetFileName(sourcePath)} -> {outputFolder}");
            }

            string pdfPath = await _documentPdfSourceService.GetPdfPathAsync(sourcePath, cancellationToken);
            int pageCount = await _pdfConversionService.GetPdfPageCountAsync(pdfPath, cancellationToken);

            bool saveAllPages = string.IsNullOrWhiteSpace(options.PageRange);
            IReadOnlyList<int>? pageIndexes = null;
            if (!string.IsNullOrWhiteSpace(options.PageRange))
            {
                pageIndexes = PageRangeParser.Parse(options.PageRange, pageCount);
            }

            EnsureOutputDirectoryIsWritable(options, outputFolder, pageIndexes, saveAllPages, pageCount, format);

            // Progress<T> は SynchronizationContext 経由で非同期に通知するため、出力漏れや順序の乱れを避けて同期的に書き出す
            IProgress<SaveProgressReport>? progress = options.Quiet
                ? null
                : new ConsoleProgress();

            await _pdfConversionService.SavePdfPagesToImagesAsync(
                pdfPath,
                pageIndexes,
                outputFolder,
                saveAllPages,
                resolutionMode,
                parsedResolution,
                format,
                preserveTransparency,
                progress,
                cancellationToken);

            ApplicationLogger.Info(
                "Batch",
                $"Image export completed: source={sourcePath}, folder={outputFolder}, pages={pageCount}, format={format}");
        }

        /// <summary>出力先に既存ファイルがある場合の上書き可否を確認する</summary>
        /// <param name="options">バッチオプション</param>
        /// <param name="outputFolder">出力フォルダー</param>
        /// <param name="pageIndexes">保存対象ページ</param>
        /// <param name="saveAllPages">全ページ保存かどうか</param>
        /// <param name="pageCount">総ページ数</param>
        /// <param name="format">出力形式</param>
        private static void EnsureOutputDirectoryIsWritable(
            BatchCliOptions options,
            string outputFolder,
            IReadOnlyList<int>? pageIndexes,
            bool saveAllPages,
            int pageCount,
            OutputImageFormat format)
        {
            if (options.OverwriteExisting)
            {
                return;
            }

            IEnumerable<int> pages = saveAllPages || pageIndexes == null
                ? Enumerable.Range(0, pageCount)
                : pageIndexes;

            string? existingFile = pages
                .Select(index => Path.Combine(outputFolder, PageImageFileNameHelper.GetFileName(index, pageCount, format)))
                .FirstOrDefault(File.Exists);

            if (existingFile != null)
            {
                throw new IOException($"出力先に既存ファイルがあります: {existingFile} (--overwrite で上書きできます)");
            }
        }

        /// <summary>PDF 出力先パスを解決する</summary>
        /// <param name="options">バッチオプション</param>
        /// <param name="sourcePath">入力ファイルの絶対パス</param>
        /// <returns>PDF 出力先の絶対パス</returns>
        private static string ResolvePdfDestinationPath(BatchCliOptions options, string sourcePath)
        {
            string outputPath = Path.GetFullPath(options.OutputPath.Trim().Trim('"'));
            if (outputPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                if (options.InputFiles.Count > 1)
                {
                    throw new InvalidOperationException("複数ファイルを変換する場合、--output にはフォルダーを指定してください。");
                }

                return outputPath;
            }

            Directory.CreateDirectory(outputPath);
            string fileName = Path.GetFileNameWithoutExtension(sourcePath) + ".pdf";
            return Path.Combine(outputPath, fileName);
        }

        /// <summary>フォルダー名として安全な文字列に変換する</summary>
        /// <param name="name">元の名前</param>
        /// <returns>サニタイズ後の名前</returns>
        private static string SanitizeDirectoryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "output";
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            var builder = new char[name.Length];
            for (int index = 0; index < name.Length; index++)
            {
                char current = name[index];
                builder[index] = invalidChars.Contains(current) ? '_' : current;
            }

            string sanitized = new string(builder).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "output" : sanitized;
        }

        /// <summary>標準エラーへメッセージを出力する</summary>
        /// <param name="message">メッセージ</param>
        private static void WriteError(string message)
        {
            BatchConsoleHelper.WriteErrorLine(message);
        }


        /********************************************************************************/
        /*                                   ネスト型                                   */
        /********************************************************************************/
        /// <summary>
        /// 保存進捗を呼び出し元スレッドで即座に標準出力へ書き出す<br/>
        /// 保存先への反映後に再通知される完了メッセージなど、直前と同じ内容は出力しない
        /// </summary>
        private sealed class ConsoleProgress : IProgress<SaveProgressReport>
        {
            /// <summary>排他用オブジェクト</summary>
            private readonly object _lock = new object();

            /// <summary>直前に出力したメッセージ</summary>
            private string? _lastMessage;

            /// <inheritdoc/>
            public void Report(SaveProgressReport value)
            {
                string? message = value?.Message;
                lock (_lock)
                {
                    if (string.Equals(message, _lastMessage, StringComparison.Ordinal))
                    {
                        return;
                    }

                    _lastMessage = message;
                    BatchConsoleHelper.WriteLine(message);
                }
            }
        }
    }
}
