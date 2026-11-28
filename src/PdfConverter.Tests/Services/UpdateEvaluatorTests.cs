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
            var response = new GitHubRelease
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
            var response = new GitHubRelease
            {
                Tag = "v0.5.0",
                Assets = new[]
                {
                    new GitHubReleaseAsset
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
            var response = new GitHubRelease
            {
                Tag = "v0.5.0",
                Assets = new[]
                {
                    new GitHubReleaseAsset
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
            var response = new GitHubRelease
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
            var response = new GitHubRelease
            {
                Tag = "not-a-version",
                Assets = new[] { Asset("PdfConverter-0.5.0-x64-setup.exe", 10) },
            };

            UpdateCheckResult result = UpdateEvaluator.Evaluate("0.4.0", response, "x64");

            Assert.Equal(UpdateCheckKind.Failed, result.Kind);
        }

        /// <summary>
        /// ポータブル版はプラットフォームに合う zip を選び、更新情報にポータブル版であることを記録することを検証する
        /// </summary>
        [Fact]
        public void Evaluate_Portable_SelectsZip()
        {
            var response = new GitHubRelease
            {
                Tag = "v0.5.0",
                Assets = new[]
                {
                    Asset("PdfConverter-0.5.0-x64-setup.exe", 10),
                    Asset("PdfConverter-0.5.0-x64-portable.zip", 20),
                },
            };

            UpdateCheckResult result = UpdateEvaluator.Evaluate("0.4.0", response, "x64", UpdatePackageKind.Portable);

            Assert.Equal(UpdateCheckKind.Available, result.Kind);
            Assert.Equal("PdfConverter-0.5.0-x64-portable.zip", result.Update.AssetName);
            Assert.Equal(20, result.Update.Size);
            Assert.Equal(UpdatePackageKind.Portable, result.Update.PackageKind);
        }

        /// <summary>
        /// ポータブル版の zip が公開されていないときは、ポータブル版が見つからない旨で失敗することを検証する
        /// </summary>
        [Fact]
        public void Evaluate_PortableZipMissing_Fails()
        {
            var response = new GitHubRelease
            {
                Tag = "v0.5.0",
                Assets = new[] { Asset("PdfConverter-0.5.0-x64-setup.exe", 10) },
            };

            UpdateCheckResult result = UpdateEvaluator.Evaluate("0.4.0", response, "x64", UpdatePackageKind.Portable);

            Assert.Equal(UpdateCheckKind.Failed, result.Kind);
            Assert.Contains("ポータブル版", result.ErrorMessage);
        }

        private static GitHubReleaseAsset Asset(string name, long size) =>
            new GitHubReleaseAsset
            {
                Name = name,
                Size = size,
                Digest = "sha256:" + new string('a', 64),
            };
    }
}
