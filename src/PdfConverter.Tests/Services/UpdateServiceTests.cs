using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Infrastructure;
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

        private const string PortableAssetName = "PdfConverter-0.5.0-x64-portable.zip";


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
  ""tag_name"": ""v0.5.0"",
  ""assets"": [{{
    ""name"": ""PdfConverter-0.5.0-x64-setup.exe"",
    ""size"": 10,
    ""digest"": ""{digest}""
  }}]
}}";

            using (var handler = new StubHandler((request, _) =>
            {
                Assert.Equal(
                    "https://api.test/repos/GnudMna/pdf-converter/releases/latest",
                    request.RequestUri.AbsoluteUri);
                Assert.Contains(request.Headers.Accept, h => h.MediaType == "application/vnd.github+json");
                Assert.True(request.Headers.Contains("X-GitHub-Api-Version"));
                Assert.Null(request.Headers.Authorization);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
            }))
            using (var client = new HttpClient(handler))
            using (var service = CreateService(client, platform: "x64"))
            {
                UpdateCheckResult result = await service.CheckForUpdateAsync(
                    TestContext.Current.CancellationToken);

                Assert.Equal(UpdateCheckKind.Available, result.Kind);
                Assert.Equal("0.5.0", result.LatestVersion);
            }
        }

        /// <summary>
        /// 公開されたリリースが無い (HTTP 404) ときは、その旨を示して失敗することを検証する
        /// </summary>
        [Fact]
        public async Task CheckForUpdateAsync_NoRelease_FailsWithMessage()
        {
            using (var handler = new StubHandler((_, __) => new HttpResponseMessage(HttpStatusCode.NotFound)))
            using (var client = new HttpClient(handler))
            using (var service = CreateService(client))
            {
                UpdateCheckResult result = await service.CheckForUpdateAsync(
                    TestContext.Current.CancellationToken);

                Assert.Equal(UpdateCheckKind.Failed, result.Kind);
                Assert.Contains("リリースが見つかりません", result.ErrorMessage);
            }
        }

        /// <summary>
        /// GitHub API の回数制限に達したときは、その旨を示して失敗することを検証する
        /// </summary>
        [Fact]
        public async Task CheckForUpdateAsync_RateLimited_FailsWithMessage()
        {
            using (var handler = new StubHandler((_, __) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
                response.Headers.Add("X-RateLimit-Remaining", "0");
                return response;
            }))
            using (var client = new HttpClient(handler))
            using (var service = CreateService(client))
            {
                UpdateCheckResult result = await service.CheckForUpdateAsync(
                    TestContext.Current.CancellationToken);

                Assert.Equal(UpdateCheckKind.Failed, result.Kind);
                Assert.Contains("上限", result.ErrorMessage);
            }
        }

        [Fact]
        public async Task ApplyUpdateAsync_DownloadsFromGitHubAndStartsSetup()
        {
            byte[] payload = Encoding.UTF8.GetBytes("setup-bytes");
            string sha256 = Convert.ToHexStringLower(SHA256.HashData(payload));

            string startedPath = null;
            using (var signingKey = new UpdateSigningTestKey())
            using (var handler = new StubHandler((request, _) =>
            {
                Assert.StartsWith(
                    $"https://download.test/GnudMna/pdf-converter/releases/download/v0.5.0/{AssetName}",
                    request.RequestUri.AbsoluteUri);
                Assert.Null(request.Headers.Authorization);
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
                () => signingKey.PublicKey,
                "https://api.test",
                "https://download.test",
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
        /// インストーラーの引数が、ウィザードのページを出さずに進捗とメッセージは表示し、完了後にアプリを起動し直す指定になることを検証する
        /// </summary>
        [Fact]
        public void BuildSetupArguments_ReturnsSilentArgumentsWithRelaunchAndLog()
        {
            string workDir = Path.Combine(_testRoot, "updates", "0.5.0");

            var arguments = UpdateService.BuildSetupArguments(workDir);

            Assert.Contains("/SILENT", arguments);
            Assert.Contains("/NORESTART", arguments);
            Assert.Contains("/CLOSEAPPLICATIONS", arguments);
            Assert.Contains("/RELAUNCH=1", arguments);
            Assert.Contains("/LOG=" + Path.Combine(workDir, UpdateService.SetupLogFileName), arguments);
            Assert.DoesNotContain("/VERYSILENT", arguments);
            Assert.DoesNotContain("/SUPPRESSMSGBOXES", arguments);
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
                () => "unused-public-key",
                "https://api.test",
                "https://download.test",
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
                () => "unused-public-key",
                "https://api.test",
                "https://download.test",
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
            string platform = "x64") =>
            new UpdateService(
                client,
                ownsHttpClient: false,
                () => "0.4.0",
                () => platform,
                () => signingPublicKey,
                "https://api.test",
                "https://download.test",
                (_, __) =>
                {
                    onStarted?.Invoke();
                    return true;
                });

        /// <summary>
        /// ポータブル版は署名を検証した zip を展開し、展開した新しい版を更新先とプロセス ID を指定して起動することを検証する
        /// </summary>
        [Fact]
        public async Task ApplyUpdateAsync_Portable_ExtractsZipAndStartsUpdater()
        {
            byte[] payload = PortablePackageTestHelper.BuildZip(PortablePackageTestHelper.CreateAppFiles("0.5.0"));
            string sha256 = Convert.ToHexStringLower(SHA256.HashData(payload));
            string appDirectory = Path.Combine(_testRoot, "app");

            string startedPath = null;
            IReadOnlyList<string> startedArguments = null;
            using (var signingKey = new UpdateSigningTestKey())
            using (var handler = new StubHandler((request, _) => CreateInstallerOrSignatureResponse(
                request,
                payload,
                signingKey.Sign("v0.5.0", PortableAssetName, payload.Length, sha256))))
            using (var client = new HttpClient(handler))
            using (var service = CreatePortableService(
                client,
                signingKey.PublicKey,
                appDirectory,
                (path, arguments) =>
                {
                    startedPath = path;
                    startedArguments = arguments;
                    return true;
                }))
            {
                UpdateApplyResult result = await service.ApplyUpdateAsync(
                    CreatePortableUpdate(payload, sha256),
                    TestContext.Current.CancellationToken);

                string stagingDir = Path.Combine(_testRoot, "updates", "0.5.0", UpdateService.PortableStagingDirectoryName);
                Assert.Equal(UpdateApplyKind.SetupStarted, result.Kind);
                Assert.Equal(Path.Combine(stagingDir, PortableUpdatePackage.AppExecutableFileName), startedPath);
                Assert.Equal("exe 0.5.0", File.ReadAllText(startedPath));
                Assert.Equal(
                    PortableUpdateCommand.BuildArguments(appDirectory, Environment.ProcessId),
                    startedArguments);
                Assert.False(File.Exists(Path.Combine(_testRoot, "updates", "0.5.0", PortableAssetName)));
            }
        }

        /// <summary>
        /// 署名済みでもポータブル版として不正な zip は、更新処理を起動せずに失敗することを検証する
        /// </summary>
        [Fact]
        public async Task ApplyUpdateAsync_PortableZipWithoutMarker_FailsWithoutStarting()
        {
            var files = PortablePackageTestHelper.CreateAppFiles("0.5.0");
            files.Remove(PortableMode.MarkerFileName);
            byte[] payload = PortablePackageTestHelper.BuildZip(files);
            string sha256 = Convert.ToHexStringLower(SHA256.HashData(payload));

            bool started = false;
            using (var signingKey = new UpdateSigningTestKey())
            using (var handler = new StubHandler((request, _) => CreateInstallerOrSignatureResponse(
                request,
                payload,
                signingKey.Sign("v0.5.0", PortableAssetName, payload.Length, sha256))))
            using (var client = new HttpClient(handler))
            using (var service = CreatePortableService(
                client,
                signingKey.PublicKey,
                Path.Combine(_testRoot, "app"),
                (_, __) =>
                {
                    started = true;
                    return true;
                }))
            {
                UpdateApplyResult result = await service.ApplyUpdateAsync(
                    CreatePortableUpdate(payload, sha256),
                    TestContext.Current.CancellationToken);

                Assert.Equal(UpdateApplyKind.Failed, result.Kind);
                Assert.Contains(PortableMode.MarkerFileName, result.ErrorMessage);
                Assert.False(started);
                Assert.False(Directory.Exists(
                    Path.Combine(_testRoot, "updates", "0.5.0", UpdateService.PortableStagingDirectoryName)));
            }
        }

        /// <summary>
        /// ポータブル版の更新を確認すると、インストーラーではなく zip を選ぶことを検証する
        /// </summary>
        [Fact]
        public async Task CheckForUpdateAsync_Portable_SelectsZip()
        {
            string digest = "sha256:" + new string('d', 64);
            string json = $@"{{
  ""tag_name"": ""v0.5.0"",
  ""assets"": [
    {{ ""name"": ""{AssetName}"", ""size"": 10, ""digest"": ""{digest}"" }},
    {{ ""name"": ""{PortableAssetName}"", ""size"": 20, ""digest"": ""{digest}"" }}
  ]
}}";

            using (var handler = new StubHandler((_, __) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            }))
            using (var client = new HttpClient(handler))
            using (var service = CreatePortableService(client, "unused-public-key", Path.Combine(_testRoot, "app"), (_, __) => true))
            {
                UpdateCheckResult result = await service.CheckForUpdateAsync(
                    TestContext.Current.CancellationToken);

                Assert.Equal(UpdateCheckKind.Available, result.Kind);
                Assert.Equal(PortableAssetName, result.Update.AssetName);
                Assert.Equal(UpdatePackageKind.Portable, result.Update.PackageKind);
            }
        }

        private static UpdateService CreatePortableService(
            HttpClient client,
            string signingPublicKey,
            string appDirectory,
            Func<string, IReadOnlyList<string>, bool> portableUpdaterStarter) =>
            new UpdateService(
                client,
                ownsHttpClient: false,
                () => "0.4.0",
                () => "x64",
                () => signingPublicKey,
                "https://api.test",
                "https://download.test",
                (_, __) => throw new InvalidOperationException("ポータブル版でインストーラーを起動した"),
                packageKindProvider: () => UpdatePackageKind.Portable,
                appDirectoryProvider: () => appDirectory,
                portableUpdaterStarter: portableUpdaterStarter);

        private static AvailableUpdate CreatePortableUpdate(byte[] payload, string sha256) =>
            new AvailableUpdate
            {
                CurrentVersion = "0.4.0",
                LatestVersion = "0.5.0",
                Tag = "v0.5.0",
                AssetName = PortableAssetName,
                PackageKind = UpdatePackageKind.Portable,
                Size = payload.Length,
                Sha256 = sha256,
            };

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
