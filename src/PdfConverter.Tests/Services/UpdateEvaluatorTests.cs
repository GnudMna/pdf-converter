using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="UpdateEvaluator"/> の単体テスト
    /// </summary>
    public class UpdateEvaluatorTests
    {
        [Fact]
        public void Evaluate_MarksEqualVersionUpToDate()
        {
            var response = new VersionApiResponse
            {
                Tag = "v0.4.0",
                Assets = new[] { Asset("PdfConverter-0.4.0-x64-setup.exe", 100) },
            };

            UpdateCheckResult result = UpdateEvaluator.Evaluate("0.4.0", response, "x64");

            Assert.Equal(UpdateCheckKind.UpToDate, result.Kind);
            Assert.Equal("0.4.0", result.LatestVersion);
        }

        [Fact]
        public void Evaluate_ReturnsAvailableWhenNewer()
        {
            string digest = "sha256:" + new string('b', 64);
            var response = new VersionApiResponse
            {
                Tag = "v0.5.0",
                Assets = new[]
                {
                    new VersionApiAsset
                    {
                        Name = "PdfConverter-0.5.0-x64-setup.exe",
                        Size = 42,
                        Digest = digest,
                    },
                },
            };

            UpdateCheckResult result = UpdateEvaluator.Evaluate("0.4.0", response, "x64");

            Assert.Equal(UpdateCheckKind.Available, result.Kind);
            Assert.Equal("0.5.0", result.Update.LatestVersion);
            Assert.Equal("v0.5.0", result.Update.Tag);
            Assert.Equal("PdfConverter-0.5.0-x64-setup.exe", result.Update.AssetName);
            Assert.Equal(42, result.Update.Size);
            Assert.Equal(new string('b', 64), result.Update.Sha256);
        }

        [Fact]
        public void Evaluate_FailsWhenDigestMissing()
        {
            var response = new VersionApiResponse
            {
                Tag = "v0.5.0",
                Assets = new[]
                {
                    new VersionApiAsset
                    {
                        Name = "PdfConverter-0.5.0-x64-setup.exe",
                        Size = 42,
                        Digest = null,
                    },
                },
            };

            UpdateCheckResult result = UpdateEvaluator.Evaluate("0.4.0", response, "x64");

            Assert.Equal(UpdateCheckKind.Failed, result.Kind);
            Assert.Contains("digest", result.ErrorMessage);
        }

        [Fact]
        public void Evaluate_FailsWhenArchMissing()
        {
            var response = new VersionApiResponse
            {
                Tag = "v0.5.0",
                Assets = new[] { Asset("PdfConverter-0.5.0-x86-setup.exe", 10) },
            };

            UpdateCheckResult result = UpdateEvaluator.Evaluate("0.4.0", response, "x64");

            Assert.Equal(UpdateCheckKind.Failed, result.Kind);
            Assert.Contains("x64", result.ErrorMessage);
        }

        [Fact]
        public void Evaluate_FailsOnInvalidTag()
        {
            var response = new VersionApiResponse
            {
                Tag = "not-a-version",
                Assets = new[] { Asset("PdfConverter-0.5.0-x64-setup.exe", 10) },
            };

            UpdateCheckResult result = UpdateEvaluator.Evaluate("0.4.0", response, "x64");

            Assert.Equal(UpdateCheckKind.Failed, result.Kind);
        }

        private static VersionApiAsset Asset(string name, long size) =>
            new VersionApiAsset
            {
                Name = name,
                Size = size,
                Digest = "sha256:" + new string('a', 64),
            };
    }
}
