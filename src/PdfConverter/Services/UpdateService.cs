using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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

        /// <summary>HTTP リクエスト全体の既定のタイムアウト</summary>
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 本体の読み込みで、データが届かないまま待ち続けてよい時間
        /// (<see cref="HttpCompletionOption.ResponseHeadersRead"/> では <see cref="HttpClient.Timeout"/> が効かないため)
        /// </summary>
        private static readonly TimeSpan DefaultDownloadStallTimeout = TimeSpan.FromSeconds(30);


        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>更新サーバーとの通信に使う HTTP クライアント</summary>
        private readonly HttpClient _httpClient;

        /// <summary>破棄時に <see cref="_httpClient"/> も破棄するかどうか</summary>
        private readonly bool _ownsHttpClient;

        /// <summary>現在のバージョンを返す関数</summary>
        private readonly Func<string> _currentVersionProvider;

        /// <summary>プラットフォーム名 (<c>x64</c> / <c>x86</c>) を返す関数</summary>
        private readonly Func<string> _platformProvider;

        /// <summary>アセットのダウンロード認証に使うリリースキーを返す関数</summary>
        private readonly Func<string> _releaseKeyProvider;

        /// <summary>インストーラーの署名検証に使う公開鍵 (Base64) を返す関数</summary>
        private readonly Func<string> _signingPublicKeyProvider;

        /// <summary>gh-release-bridge のベース URL (末尾の <c>/</c> なし)</summary>
        private readonly string _bridgeBaseUrl;

        /// <summary>インストーラーを起動する関数 (引数はインストーラーのパスと作業フォルダー、戻り値は起動できたかどうか)</summary>
        private readonly Func<string, string, bool> _processStarter;

        /// <summary>ダウンロード中にデータが届かないまま待ち続けてよい時間</summary>
        private readonly TimeSpan _downloadStallTimeout;

        /// <summary>破棄済みかどうか</summary>
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
                () => UpdateSigningKey.PublicKey,
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
            Func<string> signingPublicKeyProvider,
            string bridgeBaseUrl,
            Func<string, string, bool> processStarter,
            TimeSpan? downloadStallTimeout = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _ownsHttpClient = ownsHttpClient;
            _currentVersionProvider = currentVersionProvider
                ?? throw new ArgumentNullException(nameof(currentVersionProvider));
            _platformProvider = platformProvider
                ?? throw new ArgumentNullException(nameof(platformProvider));
            _releaseKeyProvider = releaseKeyProvider
                ?? throw new ArgumentNullException(nameof(releaseKeyProvider));
            _signingPublicKeyProvider = signingPublicKeyProvider
                ?? throw new ArgumentNullException(nameof(signingPublicKeyProvider));
            _bridgeBaseUrl = (bridgeBaseUrl ?? DefaultBridgeBaseUrl).TrimEnd('/');
            _processStarter = processStarter ?? throw new ArgumentNullException(nameof(processStarter));
            _downloadStallTimeout = downloadStallTimeout ?? DefaultDownloadStallTimeout;
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

            // 署名を検証できないインストーラーは起動しないため、ダウンロードの前に確認する
            string signingPublicKey = _signingPublicKeyProvider();
            if (string.IsNullOrEmpty(signingPublicKey))
            {
                return UpdateApplyResult.Failed(
                    "更新の署名検証用公開鍵が埋め込まれていません。UpdateSigningKey.cs の PublicKey を設定してビルドしてください。");
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

                // ハッシュは更新サーバーが返す値のため、サーバーとは独立に作った署名で真正性を確かめる
                (string? signatureText, string? downloadError) = await DownloadSignatureAsync(
                        $"{downloadUrl}.sig",
                        releaseKey,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (signatureText == null)
                {
                    TryDelete(destinationPath);
                    return UpdateApplyResult.Failed(downloadError ?? "署名ファイルを取得できませんでした。");
                }

                if (!UpdateSignatureVerifier.TryVerify(
                        signingPublicKey,
                        update.Tag,
                        update.AssetName,
                        actualSize,
                        actualHash,
                        signatureText,
                        out string? signatureError))
                {
                    TryDelete(destinationPath);
                    return UpdateApplyResult.Failed(signatureError);
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
        /// <summary>
        /// 既定のタイムアウトと User-Agent を設定した HTTP クライアントを生成する
        /// </summary>
        /// <returns>HTTP クライアント</returns>
        private static HttpClient CreateDefaultHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = DefaultTimeout,
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PDF-Converter-Update");
            return client;
        }

        /// <summary>
        /// インストーラーをシェル経由で起動する
        /// </summary>
        /// <param name="setupPath">インストーラーのパス</param>
        /// <param name="workingDirectory">作業フォルダー</param>
        /// <returns>プロセスを起動できた場合は <c>true</c></returns>
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

        /// <summary>
        /// 署名ファイルをダウンロードして文字列として返す
        /// </summary>
        /// <param name="url">署名ファイルの URL</param>
        /// <param name="releaseKey">ダウンロード認証に使うリリースキー</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>署名ファイルの内容。失敗した場合は <c>Text</c> が <c>null</c> で、<c>ErrorMessage</c> に理由を入れる</returns>
        private async Task<(string? Text, string? ErrorMessage)> DownloadSignatureAsync(
            string url,
            string releaseKey,
            CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", releaseKey);

                using (HttpResponseMessage response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return (null, $"署名ファイルのダウンロードに失敗しました。(HTTP {(int)response.StatusCode})");
                    }

                    // 上限より 1 バイト多く読み、超過を検出する
                    var buffer = new byte[UpdateSignatureVerifier.MaxSignatureFileBytes + 1];
                    int total = 0;
                    using (Stream source = await response.Content
                        .ReadAsStreamAsync(cancellationToken)
                        .ConfigureAwait(false))
                    using (var stallCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        int read;
                        while (total < buffer.Length
                            && (read = await ReadWithStallTimeoutAsync(
                                    source,
                                    buffer.AsMemory(total),
                                    stallCts,
                                    cancellationToken)
                                .ConfigureAwait(false)) > 0)
                        {
                            total += read;
                        }
                    }

                    if (total > UpdateSignatureVerifier.MaxSignatureFileBytes)
                    {
                        return (null, "署名ファイルのサイズが上限を超えています。");
                    }

                    return (Encoding.ASCII.GetString(buffer, 0, total), null);
                }
            }
        }

        /// <summary>
        /// 本体を 1 回読み込む。データが一定時間届かないときは <see cref="TimeoutException"/> を投げる
        /// </summary>
        /// <param name="source">読み込み元</param>
        /// <param name="buffer">読み込み先</param>
        /// <param name="stallCts">読み込みごとに待ち時間を数え直す、呼び出し元のキャンセル元と連動したトークン源</param>
        /// <param name="cancellationToken">呼び出し元のキャンセルトークン</param>
        /// <returns>読み込んだバイト数</returns>
        private async ValueTask<int> ReadWithStallTimeoutAsync(
            Stream source,
            Memory<byte> buffer,
            CancellationTokenSource stallCts,
            CancellationToken cancellationToken)
        {
            // CancelAfter は未発火の間なら待ち時間を数え直せる
            stallCts.CancelAfter(_downloadStallTimeout);
            try
            {
                return await source.ReadAsync(buffer, stallCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("更新サーバーからの応答が一定時間ありませんでした。通信状況を確認してください。");
            }
        }

        /// <summary>
        /// レスポンス本文をファイルへ保存する。宣言サイズまたは上限を超えた場合は中断する
        /// </summary>
        /// <param name="response">ダウンロードのレスポンス</param>
        /// <param name="destinationPath">保存先のパス</param>
        /// <param name="expectedSize">宣言されたバイト数</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>非同期操作のタスク</returns>
        /// <exception cref="InvalidDataException">受信したサイズが宣言値または上限を超えた場合</exception>
        /// <exception cref="TimeoutException">データが一定時間届かなかった場合</exception>
        private async Task DownloadToFileAsync(
            HttpResponseMessage response,
            string destinationPath,
            long expectedSize,
            CancellationToken cancellationToken)
        {
            long absoluteMax = Math.Max(expectedSize, UpdateDigestHelper.MaxUpdateDownloadBytes);
            using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            using (var stallCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
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
                while ((read = await ReadWithStallTimeoutAsync(source, buffer, stallCts, cancellationToken)
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

        /// <summary>
        /// ファイルを削除する (削除できなくても例外は送出しない)
        /// </summary>
        /// <param name="path">削除するファイルのパス</param>
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

        /// <summary>破棄済みの場合に例外を送出する</summary>
        /// <exception cref="ObjectDisposedException">破棄済みの場合</exception>
        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }
}
