using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Infrastructure;

namespace PdfConverter.Services
{
    /// <summary>
    /// gh-release-bridge 経由の更新確認・適用の実装
    /// </summary>
    public sealed class UpdateService : IUpdateService, IDisposable
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>gh-release-bridge のベース URL</summary>
        public const string DefaultBridgeBaseUrl =
            "https://gh-release-bridge.gnudlemna-4656.workers.dev";

        /// <summary>GitHub リポジトリ所有者</summary>
        public const string ReleaseOwner = "GnudMna";

        /// <summary>GitHub リポジトリ名</summary>
        public const string ReleaseRepo = "pdf-converter";

        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);


        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly Func<string> _currentVersionProvider;
        private readonly Func<string> _platformProvider;
        private readonly Func<string> _releaseKeyProvider;
        private readonly string _bridgeBaseUrl;
        private readonly Func<string, string, bool> _processStarter;
        private bool _disposed;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 本番向け既定設定で初期化する
        /// </summary>
        public UpdateService()
            : this(
                CreateDefaultHttpClient(),
                ownsHttpClient: true,
                () => ApplicationMetadata.Version,
                UpdateAssetSelector.GetCurrentPlatform,
                () => UpdateReleaseKey.Value,
                DefaultBridgeBaseUrl,
                StartSetupProcess)
        {
        }

        /// <summary>
        /// テスト向けに依存を差し替えて初期化する
        /// </summary>
        internal UpdateService(
            HttpClient httpClient,
            bool ownsHttpClient,
            Func<string> currentVersionProvider,
            Func<string> platformProvider,
            Func<string> releaseKeyProvider,
            string bridgeBaseUrl,
            Func<string, string, bool> processStarter)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _ownsHttpClient = ownsHttpClient;
            _currentVersionProvider = currentVersionProvider
                ?? throw new ArgumentNullException(nameof(currentVersionProvider));
            _platformProvider = platformProvider
                ?? throw new ArgumentNullException(nameof(platformProvider));
            _releaseKeyProvider = releaseKeyProvider
                ?? throw new ArgumentNullException(nameof(releaseKeyProvider));
            _bridgeBaseUrl = (bridgeBaseUrl ?? DefaultBridgeBaseUrl).TrimEnd('/');
            _processStarter = processStarter ?? throw new ArgumentNullException(nameof(processStarter));
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public async Task<UpdateCheckResult> CheckForUpdateAsync(
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            string currentVersion = _currentVersionProvider() ?? "0.0.0";
            string url = $"{_bridgeBaseUrl}/{ReleaseOwner}/{ReleaseRepo}/version";

            try
            {
                using (HttpResponseMessage response = await _httpClient
                    .GetAsync(url, cancellationToken)
                    .ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return UpdateCheckResult.Failed(
                            $"最新版の照会に失敗しました。(HTTP {(int)response.StatusCode})");
                    }

                    string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    VersionApiResponse parsed = VersionApiJsonParser.Parse(json);
                    return UpdateEvaluator.Evaluate(currentVersion, parsed, _platformProvider());
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return UpdateCheckResult.Failed($"最新版の照会に失敗しました。\n\n{ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<UpdateApplyResult> ApplyUpdateAsync(
            AvailableUpdate? update,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (update == null)
            {
                return UpdateApplyResult.Failed("更新情報がありません。");
            }

            string releaseKey = _releaseKeyProvider();
            if (string.IsNullOrEmpty(releaseKey))
            {
                return UpdateApplyResult.Failed(
                    "リリースキーが埋め込まれていません。UpdateReleaseKey.local.props または環境変数 PDF_CONVERTER_KEY を設定してビルドしてください。");
            }

            if (!UpdateDigestHelper.TryValidateDeclaredSize(update.Size, out string? sizeError))
            {
                return UpdateApplyResult.Failed(sizeError);
            }

            // 更新サーバーの応答をパスに組み込むため、作業フォルダー外への書き込みを防ぐ
            string versionDirectoryName = update.LatestVersion ?? "unknown";
            if (!UpdateAssetSelector.IsSafeFileName(versionDirectoryName)
                || !UpdateAssetSelector.IsSafeFileName(update.AssetName))
            {
                return UpdateApplyResult.Failed("更新情報のバージョンまたはファイル名が不正です。");
            }

            string updatesRoot = Path.GetFullPath(Path.Combine(
                PdfConverterTempDirectory.GetRootPath(),
                "updates"));
            string workDir = Path.GetFullPath(Path.Combine(updatesRoot, versionDirectoryName));
            string destinationPath = Path.GetFullPath(Path.Combine(workDir, update.AssetName));
            if (!string.Equals(Path.GetDirectoryName(workDir), updatesRoot, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetDirectoryName(destinationPath), workDir, StringComparison.OrdinalIgnoreCase))
            {
                return UpdateApplyResult.Failed("更新情報のバージョンまたはファイル名が不正です。");
            }

            Directory.CreateDirectory(workDir);
            string downloadUrl =
                $"{_bridgeBaseUrl}/{ReleaseOwner}/{ReleaseRepo}/{Uri.EscapeDataString(update.Tag)}/{Uri.EscapeDataString(update.AssetName)}";

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", releaseKey);

                    using (HttpResponseMessage response = await _httpClient
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                        .ConfigureAwait(false))
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            return UpdateApplyResult.Failed("ダウンロード認証に失敗しました。");
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            return UpdateApplyResult.Failed(
                                $"インストーラーのダウンロードに失敗しました。(HTTP {(int)response.StatusCode})");
                        }

                        await DownloadToFileAsync(
                                response,
                                destinationPath,
                                update.Size,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                long actualSize = new FileInfo(destinationPath).Length;
                if (actualSize != update.Size)
                {
                    TryDelete(destinationPath);
                    return UpdateApplyResult.Failed(
                        $"ダウンロードサイズが一致しません。(期待: {update.Size}, 実際: {actualSize})");
                }

                string actualHash = UpdateDigestHelper.ComputeSha256Hex(destinationPath);
                if (!string.Equals(actualHash, update.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(destinationPath);
                    return UpdateApplyResult.Failed("ダウンロードファイルのハッシュが一致しません。");
                }

                if (!_processStarter(destinationPath, workDir))
                {
                    return UpdateApplyResult.Failed("インストーラーの起動に失敗しました。");
                }

                return UpdateApplyResult.SetupStarted(destinationPath);
            }
            catch (OperationCanceledException)
            {
                TryDelete(destinationPath);
                throw;
            }
            catch (Exception ex)
            {
                TryDelete(destinationPath);
                return UpdateApplyResult.Failed($"更新の適用に失敗しました。\n\n{ex.Message}");
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        private static HttpClient CreateDefaultHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = DefaultTimeout,
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PDF-Converter-Update");
            return client;
        }

        private static bool StartSetupProcess(string setupPath, string workingDirectory)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = setupPath,
                WorkingDirectory = workingDirectory,
                UseShellExecute = true,
            };

            Process? process = Process.Start(startInfo);
            return process != null;
        }

        private static async Task DownloadToFileAsync(
            HttpResponseMessage response,
            string destinationPath,
            long expectedSize,
            CancellationToken cancellationToken)
        {
            long absoluteMax = Math.Max(expectedSize, UpdateDigestHelper.MaxUpdateDownloadBytes);
            using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            using (var destination = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)
                    .ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (total > expectedSize || total > absoluteMax)
                    {
                        throw new InvalidDataException("ダウンロードサイズが宣言値または上限を超えました。");
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // ベストエフォート
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }
}
