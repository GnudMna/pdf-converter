using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Infrastructure;

namespace PdfConverter.Services
{
    /// <summary>
    /// GitHub Release からの更新確認・適用の実装
    /// </summary>
    public sealed class UpdateService : IUpdateService, IDisposable
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>GitHub REST API のベース URL</summary>
        public const string DefaultApiBaseUrl = "https://api.github.com";

        /// <summary>GitHub Release のアセットをダウンロードするベース URL</summary>
        public const string DefaultDownloadBaseUrl = "https://github.com";

        /// <summary>GitHub リポジトリ所有者</summary>
        public const string ReleaseOwner = "GnudMna";

        /// <summary>GitHub リポジトリ名</summary>
        public const string ReleaseRepo = "pdf-converter";

        /// <summary>要求する GitHub REST API の版</summary>
        private const string GitHubApiVersion = "2022-11-28";

        /// <summary>インストーラーが作業フォルダーへ書き出すログのファイル名</summary>
        internal const string SetupLogFileName = "setup.log";

        /// <summary>ポータブル版の zip を展開する作業フォルダー内のフォルダー名</summary>
        internal const string PortableStagingDirectoryName = "portable";

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

        /// <summary>インストーラーの署名検証に使う公開鍵 (Base64) を返す関数</summary>
        private readonly Func<string> _signingPublicKeyProvider;

        /// <summary>GitHub REST API のベース URL (末尾の <c>/</c> なし)</summary>
        private readonly string _apiBaseUrl;

        /// <summary>アセットをダウンロードするベース URL (末尾の <c>/</c> なし)</summary>
        private readonly string _downloadBaseUrl;

        /// <summary>インストーラーを起動する関数 (引数はインストーラーのパスと作業フォルダー、戻り値は起動できたかどうか)</summary>
        private readonly Func<string, string, bool> _processStarter;

        /// <summary>ダウンロード中にデータが届かないまま待ち続けてよい時間</summary>
        private readonly TimeSpan _downloadStallTimeout;

        /// <summary>更新に使うパッケージの種類 (インストール版はインストーラー、ポータブル版は zip) を返す関数</summary>
        private readonly Func<UpdatePackageKind> _packageKindProvider;

        /// <summary>ポータブル版のアプリフォルダーを返す関数</summary>
        private readonly Func<string> _appDirectoryProvider;

        /// <summary>ポータブル版の更新処理を起動する関数 (引数は実行ファイルのパスと起動引数、戻り値は起動できたかどうか)</summary>
        private readonly Func<string, IReadOnlyList<string>, bool> _portableUpdaterStarter;

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
                () => UpdateSigningKey.PublicKey,
                DefaultApiBaseUrl,
                DefaultDownloadBaseUrl,
                StartSetupProcess,
                packageKindProvider: () => PortableMode.IsEnabled ? UpdatePackageKind.Portable : UpdatePackageKind.Installer,
                appDirectoryProvider: () => PortableMode.AppDirectory,
                portableUpdaterStarter: StartPortableUpdaterProcess)
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
            Func<string> signingPublicKeyProvider,
            string apiBaseUrl,
            string downloadBaseUrl,
            Func<string, string, bool> processStarter,
            TimeSpan? downloadStallTimeout = null,
            Func<UpdatePackageKind>? packageKindProvider = null,
            Func<string>? appDirectoryProvider = null,
            Func<string, IReadOnlyList<string>, bool>? portableUpdaterStarter = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _ownsHttpClient = ownsHttpClient;
            _currentVersionProvider = currentVersionProvider
                ?? throw new ArgumentNullException(nameof(currentVersionProvider));
            _platformProvider = platformProvider
                ?? throw new ArgumentNullException(nameof(platformProvider));
            _signingPublicKeyProvider = signingPublicKeyProvider
                ?? throw new ArgumentNullException(nameof(signingPublicKeyProvider));
            _apiBaseUrl = (apiBaseUrl ?? DefaultApiBaseUrl).TrimEnd('/');
            _downloadBaseUrl = (downloadBaseUrl ?? DefaultDownloadBaseUrl).TrimEnd('/');
            _processStarter = processStarter ?? throw new ArgumentNullException(nameof(processStarter));
            _downloadStallTimeout = downloadStallTimeout ?? DefaultDownloadStallTimeout;
            _packageKindProvider = packageKindProvider ?? (() => UpdatePackageKind.Installer);
            _appDirectoryProvider = appDirectoryProvider ?? (() => PortableMode.AppDirectory);
            _portableUpdaterStarter = portableUpdaterStarter ?? StartPortableUpdaterProcess;
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
            // 下書き・プレリリースを除いた最新のリリースを照会する
            string url = $"{_apiBaseUrl}/repos/{ReleaseOwner}/{ReleaseRepo}/releases/latest";

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Accept.ParseAdd("application/vnd.github+json");
                    request.Headers.Add("X-GitHub-Api-Version", GitHubApiVersion);

                    using (HttpResponseMessage response = await _httpClient
                        .SendAsync(request, cancellationToken)
                        .ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            return UpdateCheckResult.Failed(DescribeCheckFailure(response));
                        }

                        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                        GitHubRelease parsed = GitHubReleaseJsonParser.Parse(json);
                        return UpdateEvaluator.Evaluate(currentVersion, parsed, _platformProvider(), _packageKindProvider());
                    }
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

            // GitHub の応答をパスに組み込むため、作業フォルダー外への書き込みを防ぐ
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
                $"{_downloadBaseUrl}/{ReleaseOwner}/{ReleaseRepo}/releases/download/{Uri.EscapeDataString(update.Tag)}/{Uri.EscapeDataString(update.AssetName)}";

            try
            {
                using (HttpResponseMessage response = await _httpClient
                    .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return UpdateApplyResult.Failed(
                            $"更新ファイルのダウンロードに失敗しました。(HTTP {(int)response.StatusCode})");
                    }

                    await DownloadToFileAsync(
                            response,
                            destinationPath,
                            update.Size,
                            cancellationToken)
                        .ConfigureAwait(false);
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

                // ハッシュは GitHub が返す値のため、GitHub とは独立に作った署名で真正性を確かめる
                (string? signatureText, string? downloadError) = await DownloadSignatureAsync(
                        $"{downloadUrl}.sig",
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

                if (update.PackageKind == UpdatePackageKind.Portable)
                {
                    return StartPortableUpdate(destinationPath, workDir);
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
        /// 更新時にインストーラー (Inno Setup) へ渡す引数を組み立てる
        /// </summary>
        /// <remarks>
        /// ウィザードのページは出さず、進捗ウィンドウとエラーのメッセージボックスだけを表示する。
        /// <c>/RELAUNCH=1</c> はインストーラー側の独自引数で、インストール後にアプリを起動し直す
        /// </remarks>
        /// <param name="workingDirectory">ログを書き出す作業フォルダー</param>
        /// <returns>インストーラーの引数 (1 要素が 1 引数)</returns>
        internal static IReadOnlyList<string> BuildSetupArguments(string workingDirectory) =>
            [
                "/SILENT",
                "/NORESTART",
                "/CLOSEAPPLICATIONS",
                "/RELAUNCH=1",
                "/LOG=" + Path.Combine(workingDirectory, SetupLogFileName),
            ];

        /// <summary>
        /// 検証済みのポータブル版の zip を展開し、展開した新しい版で更新処理を起動する
        /// </summary>
        /// <param name="zipPath">検証済みの zip のパス</param>
        /// <param name="workDir">作業フォルダー</param>
        /// <returns>適用結果</returns>
        private UpdateApplyResult StartPortableUpdate(string zipPath, string workDir)
        {
            string stagingDir = Path.Combine(workDir, PortableStagingDirectoryName);
            try
            {
                PortableUpdatePackage.Extract(zipPath, stagingDir);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                TryDeleteDirectory(stagingDir);
                return UpdateApplyResult.Failed($"ポータブル版の展開に失敗しました。\n\n{ex.Message}");
            }
            finally
            {
                TryDelete(zipPath);
            }

            string updaterPath = Path.Combine(stagingDir, PortableUpdatePackage.AppExecutableFileName);
            IReadOnlyList<string> arguments = PortableUpdateCommand.BuildArguments(
                _appDirectoryProvider(),
                Environment.ProcessId);
            if (!_portableUpdaterStarter(updaterPath, arguments))
            {
                return UpdateApplyResult.Failed("ポータブル版の更新処理の起動に失敗しました。");
            }

            return UpdateApplyResult.SetupStarted(updaterPath);
        }

        /// <summary>
        /// 展開した新しい版を、ポータブル版の更新処理として起動する
        /// </summary>
        /// <param name="updaterPath">展開した新しい版の実行ファイル</param>
        /// <param name="arguments">起動引数</param>
        /// <returns>プロセスを起動できた場合は <c>true</c></returns>
        private static bool StartPortableUpdaterProcess(string updaterPath, IReadOnlyList<string> arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = updaterPath,
                WorkingDirectory = Path.GetDirectoryName(updaterPath),
                UseShellExecute = false,
            };
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using (Process? process = Process.Start(startInfo))
            {
                return process != null;
            }
        }

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
        /// 最新リリースの照会に失敗した応答から、利用者に示すメッセージを組み立てる
        /// </summary>
        /// <param name="response">失敗した応答</param>
        /// <returns>エラーメッセージ</returns>
        private static string DescribeCheckFailure(HttpResponseMessage response)
        {
            int statusCode = (int)response.StatusCode;
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return $"公開されているリリースが見つかりませんでした。(HTTP {statusCode})";
            }

            // 未認証の API 呼び出しは IP アドレスごとに回数が制限される
            if (response.StatusCode == HttpStatusCode.TooManyRequests
                || (response.StatusCode == HttpStatusCode.Forbidden
                    && response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining)
                    && string.Join(string.Empty, remaining) == "0"))
            {
                return $"GitHub への照会回数が上限に達しました。しばらく待ってから再度お試しください。(HTTP {statusCode})";
            }

            return $"最新版の照会に失敗しました。(HTTP {statusCode})";
        }

        /// <summary>
        /// インストーラーをシェル経由で、ウィザードのページを出さずに起動する
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
            foreach (string argument in BuildSetupArguments(workingDirectory))
            {
                startInfo.ArgumentList.Add(argument);
            }

            Process? process = Process.Start(startInfo);
            return process != null;
        }

        /// <summary>
        /// 署名ファイルをダウンロードして文字列として返す
        /// </summary>
        /// <param name="url">署名ファイルの URL</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>署名ファイルの内容。失敗した場合は <c>Text</c> が <c>null</c> で、<c>ErrorMessage</c> に理由を入れる</returns>
        private async Task<(string? Text, string? ErrorMessage)> DownloadSignatureAsync(
            string url,
            CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await _httpClient
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
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
                throw new TimeoutException("GitHub からの応答が一定時間ありませんでした。通信状況を確認してください。");
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

        /// <summary>
        /// フォルダーを中身ごと削除する (削除できなくても例外は送出しない)
        /// </summary>
        /// <param name="path">削除するフォルダーのパス</param>
        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
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
