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
        public async Task<int> RunAsync(BatchCliOptions options, CancellationToken cancellationToken = default)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

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


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
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
            string destinationDirectory = Path.GetDirectoryName(destinationPath);
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

            if (!ResolutionValueParser.TryParse(resolutionMode, resolutionValue, out double parsedResolution, out string resolutionError))
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
            IReadOnlyList<int> pageIndexes = null;
            if (!saveAllPages)
            {
                pageIndexes = PageRangeParser.Parse(options.PageRange, pageCount);
            }

            EnsureOutputDirectoryIsWritable(options, outputFolder, pageIndexes, saveAllPages, pageCount, format);

            var progress = options.Quiet
                ? null
                : new Progress<SaveProgressReport>(report => BatchConsoleHelper.WriteLine(report.Message));

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
            IReadOnlyList<int> pageIndexes,
            bool saveAllPages,
            int pageCount,
            OutputImageFormat format)
        {
            if (options.OverwriteExisting)
            {
                return;
            }

            string extension = ImageBitmapHelper.GetFileExtension(format);
            IEnumerable<int> pages = saveAllPages
                ? Enumerable.Range(0, pageCount)
                : pageIndexes;

            string existingFile = pages
                .Select(index => Path.Combine(outputFolder, $"page_{index + 1}{extension}"))
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
    }
}
