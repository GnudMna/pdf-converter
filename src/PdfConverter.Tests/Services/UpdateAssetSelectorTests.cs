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
        public void SelectInstaller_SelectsMatchingPlatform()
        {
            var assets = new[]
            {
                Asset("PdfConverter-0.5.0-x86-setup.exe"),
                Asset("PdfConverter-0.5.0-x64-setup.exe"),
            };

            VersionApiAsset selected = UpdateAssetSelector.SelectInstaller(assets, "x64");

            Assert.NotNull(selected);
            Assert.Equal("PdfConverter-0.5.0-x64-setup.exe", selected.Name);
        }

        [Fact]
        public void SelectInstaller_ReturnsNullWhenArchMissing()
        {
            var assets = new[]
            {
                Asset("PdfConverter-0.5.0-x86-setup.exe"),
            };

            Assert.Null(UpdateAssetSelector.SelectInstaller(assets, "x64"));
        }

        [Fact]
        public void HasAnyInstaller_DetectsSetupAssets()
        {
            Assert.True(UpdateAssetSelector.HasAnyInstaller(new[]
            {
                Asset("PdfConverter-0.5.0-x86-setup.exe"),
            }));
            Assert.False(UpdateAssetSelector.HasAnyInstaller(new[]
            {
                Asset("notes.txt"),
            }));
        }

        [Theory]
        [InlineData(@"PdfConverter-..\..\evil-x64-setup.exe")]
        [InlineData("PdfConverter-../../evil-x64-setup.exe")]
        [InlineData(@"PdfConverter-0.5.0-x64-setup.exe:stream-x64-setup.exe")]
        public void SelectInstaller_SkipsUnsafeNames(string unsafeName)
        {
            var assets = new[]
            {
                Asset(unsafeName),
            };

            Assert.Null(UpdateAssetSelector.SelectInstaller(assets, "x64"));
            Assert.False(UpdateAssetSelector.HasAnyInstaller(assets));
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

        private static VersionApiAsset Asset(string name) =>
            new VersionApiAsset
            {
                Name = name,
                Size = 10,
                Digest = "sha256:" + new string('a', 64),
            };
    }
}
