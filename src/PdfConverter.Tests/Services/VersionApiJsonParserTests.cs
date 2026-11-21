using System.IO;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="VersionApiJsonParser"/> の単体テスト
    /// </summary>
    public class VersionApiJsonParserTests
    {
        [Fact]
        public void Parse_ReadsTagAndAssets()
        {
            const string json = @"{
  ""owner"": ""GnudMna"",
  ""repo"": ""pdf-converter"",
  ""tag"": ""v0.5.0"",
  ""assets"": [
    {
      ""name"": ""PdfConverter-0.5.0-x64-setup.exe"",
      ""size"": 123,
      ""content_type"": ""application/x-msdownload"",
      ""digest"": ""sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa""
    }
  ]
}";

            VersionApiResponse response = VersionApiJsonParser.Parse(json);

            Assert.Equal("v0.5.0", response.Tag);
            Assert.Single(response.Assets);
            Assert.Equal("PdfConverter-0.5.0-x64-setup.exe", response.Assets[0].Name);
            Assert.Equal(123, response.Assets[0].Size);
            Assert.StartsWith("sha256:", response.Assets[0].Digest);
        }

        [Fact]
        public void Parse_ThrowsOnEmpty()
        {
            Assert.Throws<InvalidDataException>(() => VersionApiJsonParser.Parse(""));
        }
    }
}
