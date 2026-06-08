using System;
using FluentAssertions;
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

            provider.GetService(typeof(IPdfConversionService)).Should().NotBeNull();
            provider.GetService(typeof(IWordToPdfConversionService)).Should().NotBeNull();
            provider.GetService(typeof(IDocumentPdfSourceService)).Should().NotBeNull();
            provider.GetService(typeof(IDialogService)).Should().NotBeNull();
            provider.GetService(typeof(IClipboardService)).Should().NotBeNull();
            provider.GetService(typeof(IImageExportSettings)).Should().NotBeNull();
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

            previewCoordinator1.Should().NotBeNull();
            saveCoordinator1.Should().NotBeNull();
            previewCoordinator1.Should().NotBeSameAs(previewCoordinator2);
            saveCoordinator1.Should().NotBeSameAs(saveCoordinator2);
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

        /// <summary>
        /// IMainWindowViewModel が解決できることを検証する
        /// </summary>
        [Fact]
        public void Configure_ResolvesIMainWindowViewModel()
        {
            var provider = ServiceConfigurator.Configure();

            provider.GetService(typeof(IMainWindowViewModel)).Should().NotBeNull();
        }

        /// <summary>
        /// サービスプロバイダー破棄時に DocumentPdfSourceService が IDisposable として破棄されることを検証する
        /// </summary>
        [Fact]
        public void Configure_DisposeDocumentPdfSourceService()
        {
            var provider = ServiceConfigurator.Configure();

            var service = provider.GetRequiredService<IDocumentPdfSourceService>();
            service.Should().BeAssignableTo<IDisposable>();

            ((IDisposable)provider).Dispose();
        }
    }
}
