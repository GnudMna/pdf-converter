using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="UpdateService"/> の HTTP モックテスト
    /// </summary>
    public class UpdateServiceTests
    {
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
            string sha256;
            using (var sha = SHA256.Create())
            {
                sha256 = BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", string.Empty).ToLowerInvariant();
            }

            string startedPath = null;
            using (var handler = new StubHandler((request, _) =>
            {
                Assert.Contains("/GnudMna/pdf-converter/v0.5.0/", request.RequestUri.AbsoluteUri);
                Assert.NotNull(request.Headers.Authorization);
                Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
                Assert.Equal("secret-key", request.Headers.Authorization.Parameter);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(payload),
                };
            }))
            using (var client = new HttpClient(handler))
            using (var service = new UpdateService(
                client,
                ownsHttpClient: false,
                () => "0.4.0",
                () => "x64",
                () => "secret-key",
                "https://bridge.test",
                (path, _) =>
                {
                    startedPath = path;
                    return true;
                }))
            {
                var update = new AvailableUpdate
                {
                    CurrentVersion = "0.4.0",
                    LatestVersion = "0.5.0",
                    Tag = "v0.5.0",
                    AssetName = "PdfConverter-0.5.0-x64-setup.exe",
                    Size = payload.Length,
                    Sha256 = sha256,
                };

                UpdateApplyResult result = await service.ApplyUpdateAsync(
                    update,
                    TestContext.Current.CancellationToken);

                Assert.Equal(UpdateApplyKind.SetupStarted, result.Kind);
                Assert.NotNull(startedPath);
                Assert.EndsWith("PdfConverter-0.5.0-x64-setup.exe", startedPath);
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

        private static UpdateService CreateService(HttpClient client, string releaseKey, string platform) =>
            new UpdateService(
                client,
                ownsHttpClient: false,
                () => "0.4.0",
                () => platform,
                () => releaseKey,
                "https://bridge.test",
                (_, __) => true);

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
