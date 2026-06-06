using System;
using Microsoft.Extensions.DependencyInjection;
using PdfConverter.Services;
using PdfConverter.ViewModels;
using PdfConverter.ViewModels.Coordinators;
using PdfConverter.Views;

namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// アプリケーションの依存関係を構成する
    /// </summary>
    public static class ServiceConfigurator
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// サービスを登録して<see cref="IServiceProvider"/>を構築する
        /// </summary>
        /// <returns><see cref="IServiceProvider"/></returns>
        public static IServiceProvider Configure()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IPdfConversionService, PdfConversionService>();
            services.AddSingleton<IDialogService, WpfDialogService>();
            services.AddSingleton<IClipboardService, WpfClipboardService>();
            services.AddTransient<IPdfPreviewCoordinator, PdfPreviewCoordinator>();
            services.AddTransient<IPdfSaveCoordinator, PdfSaveCoordinator>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<MainWindow>();
            return services.BuildServiceProvider();
        }
    }
}
