using System;
using System.Collections.Generic;
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
    /// <remarks>
    /// ユーザーが使っている LibreOffice の設定や起動中のプロセスに影響しない・されないよう、
    /// インスタンスディレクトリ配下の変換専用プロファイル (<c>-env:UserInstallation</c>) で起動する<br/>
    /// プロファイルは起動を速くするためプロセス内で再利用し、同じプロファイルを同時に使えないため変換は順番に実行する<br/>
    /// 変換に失敗した場合 (タイムアウト・キャンセルによる強制終了を含む) は、
    /// ロックファイルなどが残ったプロファイルを使わないよう次回の変換で作り直す
    /// </remarks>
    public sealed class LibreOfficeToPdfConversionService : IWordToPdfConversionService
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>変換専用プロファイルのディレクトリ名の接頭辞</summary>
        internal const string ProfileDirectoryPrefix = "libreoffice-profile-";


        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>変換専用プロファイルを使う変換を 1 件ずつに制限するセマフォ</summary>
        private static readonly SemaphoreSlim ProfileSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>現在の変換専用プロファイルの絶対パス (未作成または作り直しが必要な場合は <c>null</c>)</summary>
        /// <remarks><see cref="ProfileSemaphore"/> を取得している間のみ読み書きする</remarks>
        private static string? _profileDirectory;

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

        /// <summary>
        /// 実行中の変換が終わり、変換専用プロファイルが使われなくなるまで待つ<br/>
        /// キャンセルは soffice の終了を待たずに通知されるため、直後にプロファイルを含むインスタンスディレクトリを削除する呼び出し元が、
        /// 使用中のプロファイルを消さないために使う
        /// </summary>
        /// <param name="timeout">待機上限時間</param>
        internal static void WaitForPendingConversion(TimeSpan timeout)
        {
            if (ProfileSemaphore.Wait(timeout))
            {
                ProfileSemaphore.Release();
            }
            else
            {
                ApplicationLogger.Warning(nameof(LibreOfficeToPdfConversionService), "LibreOffice conversion did not finish in time");
            }
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

            ProfileSemaphore.Wait(cancellationToken);
            try
            {
                string profileDirectory = GetOrCreateProfileDirectory();
                try
                {
                    return ConvertWithProfile(sofficePath, profileDirectory, wordFilePath, cancellationToken);
                }
                catch
                {
                    ResetProfileDirectory(profileDirectory);
                    throw;
                }
            }
            finally
            {
                ProfileSemaphore.Release();
            }
        }

        /// <summary>
        /// 変換専用プロファイルを指定して LibreOffice を起動し、PDF を生成する
        /// </summary>
        /// <param name="sofficePath"><c>soffice.exe</c> の絶対パス</param>
        /// <param name="profileDirectory">変換専用プロファイルの絶対パス</param>
        /// <param name="wordFilePath">Word ファイルの絶対パス</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>生成された PDF ファイルの絶対パス</returns>
        private string ConvertWithProfile(
            string sofficePath,
            string profileDirectory,
            string wordFilePath,
            CancellationToken cancellationToken)
        {
            string outputDirectory = PdfConverterTempDirectory.CreateTempWorkDirectory();

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = sofficePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                foreach (string argument in BuildArguments(profileDirectory, outputDirectory, wordFilePath, _settings))
                {
                    startInfo.ArgumentList.Add(argument);
                }

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    CapturedProcessOutput capturedOutput = ProcessWaitHelper.WaitForExitWithCapturedOutput(
                        process,
                        WordToPdfConversionTimeouts.Conversion,
                        "LibreOffice による PDF 変換がタイムアウトしました。",
                        cancellationToken);

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
        /// <remarks>
        /// 引用符やエスケープは <see cref="ProcessStartInfo.ArgumentList"/> に任せるため、各引数はそのままの値で返す
        /// </remarks>
        /// <param name="profileDirectory">変換専用プロファイルの絶対パス</param>
        /// <param name="outputDirectory">出力先ディレクトリ</param>
        /// <param name="wordFilePath">Word ファイルの絶対パス</param>
        /// <param name="settings">Word → PDF 変換設定</param>
        /// <returns>起動引数の一覧</returns>
        internal static IReadOnlyList<string> BuildArguments(
            string profileDirectory,
            string outputDirectory,
            string wordFilePath,
            IWordToPdfConversionSettings settings)
        {
            return
            [
                "-env:UserInstallation=" + new Uri(profileDirectory).AbsoluteUri,
                "--headless",
                "--nologo",
                "--nofirststartwizard",
                "--norestore",
                "--convert-to",
                LibreOfficePdfExportFilterBuilder.BuildConvertToArgument(settings),
                "--outdir",
                outputDirectory,
                wordFilePath,
            ];
        }

        /// <summary>
        /// 変換専用プロファイルのパスを返す (未作成・作り直しが必要な場合は新しいパスを決める)
        /// </summary>
        /// <remarks>
        /// 呼び出し元で <see cref="ProfileSemaphore"/> を取得していること<br/>
        /// ディレクトリ自体は LibreOffice が初回起動時に作成する
        /// </remarks>
        /// <returns>インスタンスディレクトリ配下のプロファイルの絶対パス</returns>
        private static string GetOrCreateProfileDirectory()
        {
            string instancePath = PdfConverterTempDirectory.EnsureInstanceDirectoryExists();
            if (_profileDirectory == null
                || !string.Equals(Path.GetDirectoryName(_profileDirectory), instancePath, StringComparison.OrdinalIgnoreCase))
            {
                _profileDirectory = Path.Combine(instancePath, ProfileDirectoryPrefix + Guid.NewGuid().ToString("N"));
            }

            return _profileDirectory;
        }

        /// <summary>
        /// 変換に失敗したプロファイルを破棄し、次回の変換で別のパスに作り直させる
        /// </summary>
        /// <remarks>
        /// 呼び出し元で <see cref="ProfileSemaphore"/> を取得していること<br/>
        /// 強制終了直後は LibreOffice の子プロセスがファイルを開いたままのことがあるため、削除はベストエフォートとし、
        /// 削除できなかった場合もインスタンスディレクトリと一緒に終了時に削除される
        /// </remarks>
        /// <param name="profileDirectory">破棄するプロファイルの絶対パス</param>
        private static void ResetProfileDirectory(string profileDirectory)
        {
            if (string.Equals(_profileDirectory, profileDirectory, StringComparison.OrdinalIgnoreCase))
            {
                _profileDirectory = null;
            }

            PdfConverterTempDirectory.TryDeleteDirectory(profileDirectory);
        }
    }
}
