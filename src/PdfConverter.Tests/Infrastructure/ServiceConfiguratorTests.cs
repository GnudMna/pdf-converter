using System;
using Microsoft.Extensions.DependencyInjection;
using PdfConverter.Infrastructure;
using PdfConverter.Services;
using PdfConverter.ViewModels;
using PdfConverter.ViewModels.Coordinators;
using Xunit;

namespace PdfConverter.Tests.Infrastructure
{
    /// <summary>
    /// <see cref="ServiceConfigurator"/> の DI コンテナ登録を検証する
    /// </summary>
    public class ServiceConfiguratorTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 主要サービス (PDF 変換・ダイアログ・クリップボード) が DI コンテナに登録されることを検証する
        /// </summary>
        [Fact]
        public void Configure_RegistersCoreServices()
        {
            var provider = ServiceConfigurator.Configure();

            Assert.NotNull(provider.GetService(typeof(IPdfConversionService)));
            Assert.NotNull(provider.GetService(typeof(IWordToPdfConversionService)));
            Assert.NotNull(provider.GetService(typeof(IDocumentPdfSourceService)));
            Assert.NotNull(provider.GetService(typeof(IDialogService)));
            Assert.NotNull(provider.GetService(typeof(IClipboardService)));
            Assert.NotNull(provider.GetService(typeof(IImageExportSettings)));
        }

        /// <summary>
        /// Coordinator が DI コンテナに Transient 登録されることを検証する
        /// </summary>
        [Fact]
        public void Configure_RegistersTransientCoordinators()
        {
            var provider = ServiceConfigurator.Configure();

            var previewCoordinator1 = provider.GetService(typeof(IPdfPreviewCoordinator));
            var previewCoordinator2 = provider.GetService(typeof(IPdfPreviewCoordinator));
            var saveCoordinator1 = provider.GetService(typeof(IPdfSaveCoordinator));
            var saveCoordinator2 = provider.GetService(typeof(IPdfSaveCoordinator));

            Assert.NotNull(previewCoordinator1);
            Assert.NotNull(saveCoordinator1);
            Assert.NotSame(previewCoordinator2, previewCoordinator1);
            Assert.NotSame(saveCoordinator2, saveCoordinator1);
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

            Assert.NotNull(viewModel1);
            Assert.NotNull(viewModel2);
            Assert.NotSame(viewModel2, viewModel1);
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

            Assert.Same(service2, service1);
        }

        /// <summary>
        /// IMainWindowViewModel が解決できることを検証する
        /// </summary>
        [Fact]
        public void Configure_ResolvesIMainWindowViewModel()
        {
            var provider = ServiceConfigurator.Configure();

            Assert.NotNull(provider.GetService(typeof(IMainWindowViewModel)));
        }

        /// <summary>
        /// サービスプロバイダー破棄時に DocumentPdfSourceService が IDisposable として破棄されることを検証する
        /// </summary>
        [Fact]
        public void Configure_DisposeDocumentPdfSourceService()
        {
            var provider = ServiceConfigurator.Configure();

            var service = provider.GetRequiredService<IDocumentPdfSourceService>();
            Assert.IsAssignableFrom<IDisposable>(service);

            ((IDisposable)provider).Dispose();
        }
    }
}
