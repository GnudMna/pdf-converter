using System;
using System.IO;
using System.Linq;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="LibreOfficePathHelper"/> の動作を検証する
    /// </summary>
    public class LibreOfficePathHelperTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
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
                Assert.Equal(path, LibreOfficePathHelper.Resolve(path));
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

            Assert.Throws<FileNotFoundException>(act);
        }

        /// <summary>
        /// 既定インストール先候補に重複パスが含まれないことを検証する
        /// </summary>
        [Fact]
        public void GetDefaultCandidates_ReturnsDistinctPaths()
        {
            var candidates = LibreOfficePathHelper.GetDefaultCandidates().ToList();

            Assert.NotEmpty(candidates);
            Assert.Equal(candidates.Count, candidates.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }
}
