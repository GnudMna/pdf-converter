using FluentAssertions;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="WpfDialogService"/> の契約と生成を検証する
    /// </summary>
    public class WpfDialogServiceTests
    {
        /// <summary>
        /// <see cref="IDialogService"/> を実装していることを検証する
        /// </summary>
        [Fact]
        public void WpfDialogService_ImplementsIDialogService()
        {
            typeof(WpfDialogService).Should().Implement<IDialogService>();
        }

        /// <summary>
        /// インスタンスを生成できることを検証する
        /// </summary>
        [Fact]
        public void Constructor_CreatesInstance()
        {
            var service = new WpfDialogService();

            service.Should().NotBeNull();
            service.Should().BeAssignableTo<IDialogService>();
        }
    }
}
