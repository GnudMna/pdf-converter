using System;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="SemVerComparer"/> の単体テスト
    /// </summary>
    public class SemVerComparerTests
    {
        [Theory]
        [InlineData("v0.4.0", "0.4.0")]
        [InlineData("V1.2.3", "1.2.3")]
        [InlineData("0.4.0", "0.4.0")]
        public void StripVersionPrefix_RemovesLeadingV(string input, string expected)
        {
            Assert.Equal(expected, SemVerComparer.StripVersionPrefix(input));
        }

        [Theory]
        [InlineData("0.4.0", true)]
        [InlineData("v0.4.0", true)]
        [InlineData("1.2", true)]
        [InlineData("1.2.3.4", true)]
        [InlineData("not-a-version", false)]
        [InlineData("1.0.0-beta", false)]
        [InlineData("", false)]
        public void TryParse_ParsesOrRejects(string input, bool expected)
        {
            bool ok = SemVerComparer.TryParse(input, out Version version);
            Assert.Equal(expected, ok);
            if (expected)
            {
                Assert.NotNull(version);
            }
        }

        [Fact]
        public void IsNewer_ComparesVersions()
        {
            Assert.True(SemVerComparer.TryParse("0.4.1", out Version newer));
            Assert.True(SemVerComparer.TryParse("v0.4.0", out Version older));
            Assert.True(SemVerComparer.IsNewer(newer, older));
            Assert.False(SemVerComparer.IsNewer(older, newer));
            Assert.False(SemVerComparer.IsNewer(older, older));
        }
    }
}
