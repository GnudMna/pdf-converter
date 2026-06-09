using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="WpfDialogService"/> の契約と生成を検証する
    /// </summary>
    public class WpfDialogServiceTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// <see cref="IDialogService"/> を実装していることを検証する
        /// </summary>
        [Fact]
        public void WpfDialogService_ImplementsIDialogService()
        {
            Assert.True(typeof(IDialogService).IsAssignableFrom(typeof(WpfDialogService)));
        }

        /// <summary>
        /// インスタンスを生成できることを検証する
        /// </summary>
        [Fact]
        public void Constructor_CreatesInstance()
        {
            var service = new WpfDialogService();

            Assert.NotNull(service);
            Assert.IsAssignableFrom<IDialogService>(service);
        }
    }
}
