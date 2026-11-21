using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="LibreOfficePathHelper"/> の動作を検証する
    /// </summary>
    public class LibreOfficePathHelperTests
    {
        /// <summary>
        /// ユーザー指定パスが存在する場合はそのパスを返すことを検証する
        /// </summary>
        [Fact]
        public void Resolve_ConfiguredPath_ReturnsConfiguredPath()
        {
            string path = Path.Combine(Path.GetTempPath(), $"soffice-{Guid.NewGuid():N}.exe");
            File.WriteAllText(path, "placeholder");

            try
            {
                LibreOfficePathHelper.Resolve(path).Should().Be(path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// 存在しないユーザー指定パスでは FileNotFoundException がスローされることを検証する
        /// </summary>
        [Fact]
        public void Resolve_MissingConfiguredPath_ThrowsFileNotFoundException()
        {
            string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.exe");

            Action act = () => LibreOfficePathHelper.Resolve(path);

            act.Should().Throw<FileNotFoundException>();
        }

        /// <summary>
        /// 既定インストール先候補に重複パスが含まれないことを検証する
        /// </summary>
        [Fact]
        public void GetDefaultCandidates_ReturnsDistinctPaths()
        {
            var candidates = LibreOfficePathHelper.GetDefaultCandidates().ToList();

            candidates.Should().NotBeEmpty();
            candidates.Distinct(StringComparer.OrdinalIgnoreCase).Should().HaveSameCount(candidates);
        }
    }
}
