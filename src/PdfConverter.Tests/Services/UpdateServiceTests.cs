using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="UpdateService"/> の HTTP モックテスト
    /// </summary>
    [Collection(GlobalStateTestCollection.Name)]
    public sealed class UpdateServiceTests : IDisposable
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        private const string AssetName = "PdfConverter-0.5.0-x64-setup.exe";


        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>ダウンロード先を実際の一時フォルダーから切り離すためのテスト用ルート</summary>
        private readonly string _testRoot;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        public UpdateServiceTests()
        {
            _testRoot = Path.Combine(
                Path.GetTempPath(),
                "PdfConverter-tests",
                Guid.NewGuid().ToString("N"));
            PdfConverterTempDirectory.RootPathProvider = () => _testRoot;
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// ルートパスの差し替えを戻し、ダウンロードしたファイルを含むテスト用ルートを削除する
        /// </summary>
        public void Dispose()
        {
            PdfConverterTempDirectory.RootPathProvider = null;
            PdfTestAssetFactory.DeleteDirectoryIfExists(_testRoot);
        }

        [Fact]
        public async Task CheckForUpdateAsync_ReturnsAvailable()
        {
            string digest = "sha256:" + new string('d', 64);
            string json = $@"{{
  ""tag"": ""v0.5.0"",
  ""assets"": [{{
    ""name"": ""PdfConverter-0.5.0-x64-setup.exe"",
    ""size"": 10,
    ""digest"": ""{digest}""
  }}]
}}";

            using (var handler = new StubHandler((request, _) =>
            {
                Assert.EndsWith("/GnudMna/pdf-converter/version", request.RequestUri.AbsolutePath);
                Assert.Null(request.Headers.Authorization);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
            }))
            using (var client = new HttpClient(handler))
            using (var service = CreateService(client, releaseKey: "test-key", platform: "x64"))
            {
                UpdateCheckResult result = await service.CheckForUpdateAsync(
                    TestContext.Current.CancellationToken);

                Assert.Equal(UpdateCheckKind.Available, result.Kind);
                Assert.Equal("0.5.0", result.LatestVersion);
            }
        }

        [Fact]
        public async Task ApplyUpdateAsync_SendsBearerAndStartsSetup()
        {
            byte[] payload = Encoding.UTF8.GetBytes("setup-bytes");
            string sha256 = Convert.ToHexStringLower(SHA256.HashData(payload));

            string startedPath = null;
            using (var signingKey = new UpdateSigningTestKey())
            using (var handler = new StubHandler((request, _) =>
            {
                Assert.Contains("/GnudMna/pdf-converter/v0.5.0/", request.RequestUri.AbsoluteUri);
                Assert.NotNull(request.Headers.Authorization);
                Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
                Assert.Equal("secret-key", request.Headers.Authorization.Parameter);
                return CreateInstallerOrSignatureResponse(
                    request,
                    payload,
                    signingKey.Sign("v0.5.0", AssetName, payload.Length, sha256));
            }))
            using (var client = new HttpClient(handler))
            using (var service = new UpdateService(
                client,
                ownsHttpClient: false,
                () => "0.4.0",
                () => "x64",
                () => "secret-key",
                () => signingKey.PublicKey,
                "https://bridge.test",
                (path, _) =>
                {
                    startedPath = path;
                    return true;
                }))
            {
                UpdateApplyResult result = await service.ApplyUpdateAsync(
                    CreateUpdate(payload, sha256),
                    TestContext.Current.CancellationToken);

                Assert.Equal(UpdateApplyKind.SetupStarted, result.Kind);
                Assert.NotNull(startedPath);
                Assert.EndsWith(AssetName, startedPath);
            }
        }

        /// <summary>
        /// 署名ファイルを取得できないときは、インストーラーを起動せず削除することを検証する
        /// </summary>
        [Fact]
        public async Task ApplyUpdateAsync_SignatureNotFound_FailsAndDeletesInstaller()
        {
            byte[] payload = Encoding.UTF8.GetBytes("setup-bytes");
            string sha256 = Convert.ToHexStringLower(SHA256.HashData(payload));

            bool started = false;
            using (var signingKey = new UpdateSigningTestKey())
            using (var handler = new StubHandler((request, _) =>
                request.RequestUri.AbsolutePath.EndsWith(".sig", StringComparison.Ordinal)
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) }))
            using (var client = new HttpClient(handler))
            using (var service = CreateService(client, signingKey.PublicKey, () => started = true))
            {
                UpdateApplyResult result = await service.ApplyUpdateAsync(
                    CreateUpdate(payload, sha256),
                    TestContext.Current.CancellationToken);

                Assert.Equal(UpdateApplyKind.Failed, result.Kind);
                Assert.Contains("署名ファイル", result.ErrorMessage);
                Assert.False(started);
                Assert.False(File.Exists(GetInstallerPath()));
            }
        }

        /// <summary>
        /// 更新サーバーが返すサイズ・ハッシュが正しくても、信頼する鍵の署名が無ければ起動しないことを検証する
        /// </summary>
        [Fact]
        public async Task ApplyUpdateAsync_SignedByUntrustedKey_FailsAndDeletesInstaller()
        {
            byte[] payload = Encoding.UTF8.GetBytes("setup-bytes");
            string sha256 = Convert.ToHexStringLower(SHA256.HashData(payload));

            bool started = false;
            using (var trusted = new UpdateSigningTestKey())
            using (var attacker = new UpdateSigningTestKey())
            using (var handler = new StubHandler((request, _) => CreateInstallerOrSignatureResponse(
                request,
                payload,
                attacker.Sign("v0.5.0", AssetName, payload.Length, sha256))))
            using (var client = new HttpClient(handler))
            using (var service = CreateService(client, trusted.PublicKey, () => started = true))
            {
                UpdateApplyResult result = await service.ApplyUpdateAsync(
                    CreateUpdate(payload, sha256),
                    TestContext.Current.CancellationToken);

                Assert.Equal(UpdateApplyKind.Failed, result.Kind);
                Assert.Contains("署名が一致しません", result.ErrorMessage);
                Assert.False(started);
                Assert.False(File.Exists(GetInstallerPath()));
            }
        }

        /// <summary>
        /// 別の版向けの署名を流用されても起動しないことを検証する
        /// </summary>
        [Fact]
        public async Task ApplyUpdateAsync_SignatureForOtherTag_FailsAndDeletesInstaller()
        {
            byte[] payload = Encoding.UTF8.GetBytes("setup-bytes");
            string sha256 = Convert.ToHexStringLower(SHA256.HashData(payload));

            bool started = false;
            using (var signingKey = new UpdateSigningTestKey())
            using (var handler = new StubHandler((request, _) => CreateInstallerOrSignatureResponse(
                request,
                payload,
                signingKey.Sign("v0.3.0", AssetName, payload.Length, sha256))))
            using (var client = new HttpClient(handler))
            using (var service = CreateService(client, signingKey.PublicKey, () => started = true))
            {
                UpdateApplyResult result = await service.ApplyUpdateAsync(
                    CreateUpdate(payload, sha256),
                    TestContext.Current.CancellationToken);

                Assert.Equal(UpdateApplyKind.Failed, result.Kind);
                Assert.False(started);
                Assert.False(File.Exists(GetInstallerPath()));
            }
        }

        /// <summary>
        /// 署名ファイルが上限を超えるときは起動しないことを検証する
        /// </summary>
        [Fact]
        public async Task ApplyUpdateAsync_OversizedSignature_FailsAndDeletesInstaller()
        {
            byte[] payload = Encoding.UTF8.GetBytes("setup-bytes");
            string sha256 = Convert.ToHexStringLower(SHA256.HashData(payload));

            bool started = false;
            using (var signingKey = new UpdateSigningTestKey())
            using (var handler = new StubHandler((request, _) => CreateInstallerOrSignatureResponse(
                request,
                payload,
                new string('A', UpdateSignatureVerifier.MaxSignatureFileBytes + 1))))
            using (var client = new HttpClient(handler))
            using (var service = CreateService(client, signingKey.PublicKey, () => started = true))
            {
                UpdateApplyResult result = await service.ApplyUpdateAsync(
                    CreateUpdate(payload, sha256),
                    TestContext.Current.CancellationToken);

                Assert.Equal(UpdateApplyKind.Failed, result.Kind);
                Assert.Contains("上限", result.ErrorMessage);
                Assert.False(started);
                Assert.False(File.Exists(GetInstallerPath()));
            }
        }

        /// <summary>
        /// 署名検証用の公開鍵が無いときは、ダウンロードせずに失敗することを検証する
        /// </summary>
        [Fact]
        public async Task ApplyUpdateAsync_PublicKeyMissing_FailsWithoutDownload()
        {
            bool requested = false;
            using (var handler = new StubHandler((_, __) =>
            {
                requested = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }))
            using (var client = new HttpClient(handler))
            using (var service = CreateService(client, signingPublicKey: string.Empty))
            {
                UpdateApplyResult result = await service.ApplyUpdateAsync(
                    CreateUpdate(new byte[1], new string('a', 64)),
                    TestContext.Current.CancellationToken);

                Assert.Equal(UpdateApplyKind.Failed, result.Kind);
                Assert.Contains("公開鍵", result.ErrorMessage);
                Assert.False(requested);
            }
        }

        [Fact]
        public async Task ApplyUpdateAsync_FailsWhenKeyMissing()
        {
            using (var handler = new StubHandler((_, __) => new HttpResponseMessage(HttpStatusCode.OK)))
            using (var client = new HttpClient(handler))
            using (var service = CreateService(client, releaseKey: string.Empty, platform: "x64"))
            {
                UpdateApplyResult result = await service.ApplyUpdateAsync(
                    new AvailableUpdate
                    {
                        LatestVersion = "0.5.0",
                        Tag = "v0.5.0",
                        AssetName = "PdfConverter-0.5.0-x64-setup.exe",
                        Size = 1,
                        Sha256 = new string('a', 64),
                    },
                    TestContext.Current.CancellationToken);

                Assert.Equal(UpdateApplyKind.Failed, result.Kind);
                Assert.Contains("リリースキー", result.ErrorMessage);
            }
        }

        [Theory]
        [InlineData("0.5.0", @"..\..\evil-setup.exe")]
        [InlineData("0.5.0", "../evil-setup.exe")]
        [InlineData("0.5.0", @"C:\evil-setup.exe")]
        [InlineData(@"..\..", "PdfConverter-0.5.0-x64-setup.exe")]
        [InlineData("..", "PdfConverter-0.5.0-x64-setup.exe")]
        public async Task ApplyUpdateAsync_FailsWhenPathEscapesWorkDirectory(string latestVersion, string assetName)
        {
            bool requested = false;
            bool started = false;
            using (var handler = new StubHandler((_, __) =>
            {
                requested = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }))
            using (var client = new HttpClient(handler))
            using (var service = new UpdateService(
                client,
                ownsHttpClient: false,
                () => "0.4.0",
                () => "x64",
                () => "secret-key",
                () => "unused-public-key",
                "https://bridge.test",
                (_, __) =>
                {
                    started = true;
                    return true;
                }))
            {
                UpdateApplyResult result = await service.ApplyUpdateAsync(
                    new AvailableUpdate
                    {
                        LatestVersion = latestVersion,
                        Tag = "v0.5.0",
                        AssetName = assetName,
                        Size = 1,
                        Sha256 = new string('a', 64),
                    },
                    TestContext.Current.CancellationToken);

                Assert.Equal(UpdateApplyKind.Failed, result.Kind);
                Assert.Contains("不正", result.ErrorMessage);
                Assert.False(requested);
                Assert.False(started);
            }
        }

        /// <summary>
        /// 本体のデータが途中で届かなくなったときは、待ち続けずに失敗し、インストーラーを削除することを検証する
        /// </summary>
        [Fact]
        public async Task ApplyUpdateAsync_BodyStalls_FailsAndDeletesInstaller()
        {
            byte[] payload = Encoding.UTF8.GetBytes("setup-bytes");
            string sha256 = Convert.ToHexStringLower(SHA256.HashData(payload));

            bool started = false;
            using (var handler = new StubHandler((_, __) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new StallingStream(payload)),
            }))
            using (var client = new HttpClient(handler))
            using (var service = new UpdateService(
                client,
                ownsHttpClient: false,
                () => "0.4.0",
                () => "x64",
                () => "secret-key",
                () => "unused-public-key",
                "https://bridge.test",
                (_, __) =>
                {
                    started = true;
                    return true;
                },
                downloadStallTimeout: TimeSpan.FromMilliseconds(200)))
            {
                UpdateApplyResult result = await service.ApplyUpdateAsync(
                        CreateUpdate(payload, sha256),
                        TestContext.Current.CancellationToken)
                    .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

                Assert.Equal(UpdateApplyKind.Failed, result.Kind);
                Assert.Contains("一定時間", result.ErrorMessage);
                Assert.False(started);
                Assert.False(File.Exists(GetInstallerPath()));
            }
        }

        /// <summary>
        /// データが届かない間に呼び出し元がキャンセルすると、失敗扱いにせずキャンセル例外を投げ、インストーラーを削除することを検証する
        /// </summary>
        [Fact]
        public async Task ApplyUpdateAsync_CancelledWhileBodyStalls_ThrowsAndDeletesInstaller()
        {
            byte[] payload = Encoding.UTF8.GetBytes("setup-bytes");
            string sha256 = Convert.ToHexStringLower(SHA256.HashData(payload));

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken))
            using (var handler = new StubHandler((_, __) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new StallingStream(payload)),
            }))
            using (var client = new HttpClient(handler))
            using (var service = CreateService(client))
            {
                Task<UpdateApplyResult> task = service.ApplyUpdateAsync(
                    CreateUpdate(payload, sha256),
                    cts.Token);
                cts.CancelAfter(TimeSpan.FromMilliseconds(200));

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
                Assert.False(File.Exists(GetInstallerPath()));
            }
        }

        private static UpdateService CreateService(
            HttpClient client,
            string signingPublicKey = "unused-public-key",
            Action onStarted = null,
            string releaseKey = "secret-key",
            string platform = "x64") =>
            new UpdateService(
                client,
                ownsHttpClient: false,
                () => "0.4.0",
                () => platform,
                () => releaseKey,
                () => signingPublicKey,
                "https://bridge.test",
                (_, __) =>
                {
                    onStarted?.Invoke();
                    return true;
                });

        private static AvailableUpdate CreateUpdate(byte[] payload, string sha256) =>
            new AvailableUpdate
            {
                CurrentVersion = "0.4.0",
                LatestVersion = "0.5.0",
                Tag = "v0.5.0",
                AssetName = AssetName,
                Size = payload.Length,
                Sha256 = sha256,
            };

        /// <summary>
        /// 署名ファイル (<c>.sig</c>) の要求には署名を、それ以外にはインストーラー本体を返す
        /// </summary>
        private static HttpResponseMessage CreateInstallerOrSignatureResponse(
            HttpRequestMessage request,
            byte[] payload,
            string signature) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = request.RequestUri.AbsolutePath.EndsWith(".sig", StringComparison.Ordinal)
                    ? new StringContent(signature)
                    : new ByteArrayContent(payload),
            };

        private string GetInstallerPath() =>
            Path.Combine(_testRoot, "updates", "0.5.0", AssetName);

        /// <summary>
        /// 先頭のデータを返したあと、キャンセルされるまで読み込みを返さないストリーム
        /// </summary>
        private sealed class StallingStream : Stream
        {
            private readonly byte[] _head;
            private bool _headSent;

            public StallingStream(byte[] head)
            {
                _head = head;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override async ValueTask<int> ReadAsync(
                Memory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                if (!_headSent)
                {
                    _headSent = true;
                    int count = Math.Min(_head.Length - 1, buffer.Length);
                    _head.AsMemory(0, count).CopyTo(buffer);
                    return count;
                }

                await Task.Delay(Timeout.Infinite, cancellationToken);
                return 0;
            }

            public override int Read(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException();

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler;

            public StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
                Task.FromResult(_handler(request, cancellationToken));
        }
    }
}
