using System.Threading;
using System.Threading.Tasks;
using Moq;
using PdfConverter.Services;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// <see cref="IDocumentPdfSourceService"/> のテスト用モック生成ヘルパー
    /// </summary>
    internal static class DocumentPdfSourceTestHelper
    {
        /// <summary>
        /// 入力パスをそのまま PDF パスとして返すパススルーモックを生成する
        /// </summary>
        public static Mock<IDocumentPdfSourceService> CreatePassthrough()
        {
            var mock = new Mock<IDocumentPdfSourceService>();
            mock.Setup(d => d.IsSupportedDocument(It.IsAny<string>()))
                .Returns<string>(path => DocumentFileHelper.IsSupportedDocument(path));
            mock.Setup(d => d.GetPdfPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((path, _) => Task.FromResult(path));
            return mock;
        }
    }
}
