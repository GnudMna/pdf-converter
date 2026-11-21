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
        /// サービスを登録して <see cref="IServiceProvider"/> を構築する
        /// </summary>
        /// <param name="bootstrapResult">起動時のユーザー設定初期化結果</param>
        /// <returns><see cref="IServiceProvider"/></returns>
        public static IServiceProvider Configure(UserSettingsBootstrapResult bootstrapResult)
        {
            ArgumentNullException.ThrowIfNull(bootstrapResult);

            var services = new ServiceCollection();
            services.AddSingleton(bootstrapResult);
            services.AddSingleton<IPdfConversionService, PdfConversionService>();
            services.AddSingleton<IWordToPdfConversionSettings, WordToPdfConversionSettings>();
            services.AddSingleton<IImageExportSettings, ImageExportSettings>();
            services.AddSingleton<IUserSettingsService, UserSettingsService>();
            services.AddSingleton<MicrosoftWordToPdfConversionService>();
            services.AddSingleton<LibreOfficeToPdfConversionService>();
            services.AddSingleton<IWordToPdfConversionService>(provider =>
                new SelectableWordToPdfConversionService(
                    provider.GetRequiredService<MicrosoftWordToPdfConversionService>(),
                    provider.GetRequiredService<LibreOfficeToPdfConversionService>(),
                    provider.GetRequiredService<IWordToPdfConversionSettings>()));
            services.AddSingleton<IDocumentPdfSourceService, DocumentPdfSourceService>();
            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<IDialogService, WpfDialogService>();
            services.AddSingleton<IClipboardService, WpfClipboardService>();
            services.AddSingleton<IBatchExportService, BatchExportService>();
            services.AddTransient<IPdfPreviewCoordinator, PdfPreviewCoordinator>();
            services.AddTransient<IPdfSaveCoordinator, PdfSaveCoordinator>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<IMainWindowViewModel, MainViewModel>();
            services.AddTransient<MainWindow>();
            return services.BuildServiceProvider();
        }
    }
}
