using FluentAssertions;
using PdfConverter.Infrastructure;
using PdfConverter.Services;
using PdfConverter.ViewModels;
using Xunit;

namespace PdfConverter.Tests.Infrastructure
{
    /// <summary>
    /// <see cref="ServiceConfigurator"/> の DI コンテナ登録を検証する
    /// </summary>
    public class ServiceConfiguratorTests
    {
        /// <summary>
        /// 主要サービス（PDF 変換・ダイアログ・クリップボード）が DI コンテナに登録されることを検証する
        /// </summary>
        [Fact]
        public void Configure_RegistersCoreServices()
        {
            var provider = ServiceConfigurator.Configure();

            provider.GetService(typeof(IPdfConversionService)).Should().NotBeNull();
            provider.GetService(typeof(IDialogService)).Should().NotBeNull();
            provider.GetService(typeof(IClipboardService)).Should().NotBeNull();
        }

        /// <summary>
        /// MainViewModel が Transient 登録され、取得のたびに別インスタンスが生成されることを検証する
        /// </summary>
        [Fact]
        public void Configure_RegistersTransientViewModel()
        {
            var provider = ServiceConfigurator.Configure();
            var viewModel1 = (MainViewModel)provider.GetService(typeof(MainViewModel));
            var viewModel2 = (MainViewModel)provider.GetService(typeof(MainViewModel));

            viewModel1.Should().NotBeNull();
            viewModel2.Should().NotBeNull();
            viewModel1.Should().NotBeSameAs(viewModel2);
        }

        /// <summary>
        /// IPdfConversionService が Singleton 登録され、同一インスタンスが返されることを検証する
        /// </summary>
        [Fact]
        public void Configure_PdfConversionService_IsSingleton()
        {
            var provider = ServiceConfigurator.Configure();
            var service1 = provider.GetService(typeof(IPdfConversionService));
            var service2 = provider.GetService(typeof(IPdfConversionService));

            service1.Should().BeSameAs(service2);
        }
    }
}
