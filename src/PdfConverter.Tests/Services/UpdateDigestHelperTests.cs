using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="UpdateDigestHelper"/> の単体テスト
    /// </summary>
    public class UpdateDigestHelperTests
    {
        [Fact]
        public void TryParseDigest_AcceptsSha256Prefix()
        {
            string hex = new string('c', 64);
            bool ok = UpdateDigestHelper.TryParseDigest("sha256:" + hex, out string parsed, out string error);

            Assert.True(ok);
            Assert.Null(error);
            Assert.Equal(hex, parsed);
        }

        [Fact]
        public void TryValidateDeclaredSize_RejectsZeroAndTooLarge()
        {
            Assert.False(UpdateDigestHelper.TryValidateDeclaredSize(0, out _));
            Assert.False(UpdateDigestHelper.TryValidateDeclaredSize(
                UpdateDigestHelper.MaxUpdateDownloadBytes + 1,
                out string error));
            Assert.Contains("上限", error);
            Assert.True(UpdateDigestHelper.TryValidateDeclaredSize(1, out _));
        }
    }
}
