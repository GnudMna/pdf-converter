using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="UpdateAssetSelector"/> の単体テスト
    /// </summary>
    public class UpdateAssetSelectorTests
    {
        [Fact]
        public void SelectPackage_SelectsMatchingPlatform()
        {
            var assets = new[]
            {
                Asset("PdfConverter-0.5.0-x86-setup.exe"),
                Asset("PdfConverter-0.5.0-x64-setup.exe"),
            };

            GitHubReleaseAsset selected = UpdateAssetSelector.SelectPackage(assets, "x64", UpdatePackageKind.Installer);

            Assert.NotNull(selected);
            Assert.Equal("PdfConverter-0.5.0-x64-setup.exe", selected.Name);
        }

        [Fact]
        public void SelectPackage_ReturnsNullWhenArchMissing()
        {
            var assets = new[]
            {
                Asset("PdfConverter-0.5.0-x86-setup.exe"),
            };

            Assert.Null(UpdateAssetSelector.SelectPackage(assets, "x64", UpdatePackageKind.Installer));
        }

        [Fact]
        public void HasAnyPackage_DetectsSetupAssets()
        {
            Assert.True(UpdateAssetSelector.HasAnyPackage(
                new[]
                {
                    Asset("PdfConverter-0.5.0-x86-setup.exe"),
                },
                UpdatePackageKind.Installer));
            Assert.False(UpdateAssetSelector.HasAnyPackage(
                new[]
                {
                    Asset("notes.txt"),
                },
                UpdatePackageKind.Installer));
        }

        /// <summary>
        /// ポータブル版はインストーラーではなく、プラットフォームに合う zip を選ぶことを検証する
        /// </summary>
        [Fact]
        public void SelectPackage_Portable_SelectsMatchingZip()
        {
            var assets = new[]
            {
                Asset("PdfConverter-0.5.0-x64-setup.exe"),
                Asset("PdfConverter-0.5.0-x86-portable.zip"),
                Asset("PdfConverter-0.5.0-x64-portable.zip"),
            };

            GitHubReleaseAsset selected = UpdateAssetSelector.SelectPackage(assets, "x64", UpdatePackageKind.Portable);

            Assert.NotNull(selected);
            Assert.Equal("PdfConverter-0.5.0-x64-portable.zip", selected.Name);
        }

        /// <summary>
        /// インストール版はポータブル版の zip を選ばず、ポータブル版はインストーラーを選ばないことを検証する
        /// </summary>
        [Fact]
        public void SelectPackage_OtherKindOnly_ReturnsNull()
        {
            var installerOnly = new[] { Asset("PdfConverter-0.5.0-x64-setup.exe") };
            var portableOnly = new[] { Asset("PdfConverter-0.5.0-x64-portable.zip") };

            Assert.Null(UpdateAssetSelector.SelectPackage(installerOnly, "x64", UpdatePackageKind.Portable));
            Assert.False(UpdateAssetSelector.HasAnyPackage(installerOnly, UpdatePackageKind.Portable));
            Assert.Null(UpdateAssetSelector.SelectPackage(portableOnly, "x64", UpdatePackageKind.Installer));
            Assert.False(UpdateAssetSelector.HasAnyPackage(portableOnly, UpdatePackageKind.Installer));
        }

        [Theory]
        [InlineData(@"PdfConverter-..\..\evil-x64-setup.exe")]
        [InlineData("PdfConverter-../../evil-x64-setup.exe")]
        [InlineData(@"PdfConverter-0.5.0-x64-setup.exe:stream-x64-setup.exe")]
        public void SelectPackage_SkipsUnsafeNames(string unsafeName)
        {
            var assets = new[]
            {
                Asset(unsafeName),
            };

            Assert.Null(UpdateAssetSelector.SelectPackage(assets, "x64", UpdatePackageKind.Installer));
            Assert.False(UpdateAssetSelector.HasAnyPackage(assets, UpdatePackageKind.Installer));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(".")]
        [InlineData("..")]
        [InlineData(@"..\setup.exe")]
        [InlineData("../setup.exe")]
        [InlineData(@"C:\setup.exe")]
        [InlineData("setup.exe:stream")]
        [InlineData("setup.exe.")]
        [InlineData("setup.exe ")]
        [InlineData("setup\0.exe")]
        public void IsSafeFileName_RejectsUnsafeNames(string name)
        {
            Assert.False(UpdateAssetSelector.IsSafeFileName(name));
        }

        [Theory]
        [InlineData("PdfConverter-0.5.0-x64-setup.exe")]
        [InlineData("0.5.0")]
        [InlineData("0.5.0-beta.1")]
        public void IsSafeFileName_AcceptsPlainNames(string name)
        {
            Assert.True(UpdateAssetSelector.IsSafeFileName(name));
        }

        private static GitHubReleaseAsset Asset(string name) =>
            new GitHubReleaseAsset
            {
                Name = name,
                Size = 10,
                Digest = "sha256:" + new string('a', 64),
            };
    }
}
