using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using PdfConverter.Infrastructure;
using PdfConverter.Services;
using PdfConverter.Themes;
using PdfConverter.ViewModels;
using PdfConverter.Views;

namespace PdfConverter
{
    /// <summary>
    /// アプリケーションのエントリーポイント<br/>
    /// 起動時にユーザー設定を初期化し、依存関係を組み立てる
    /// </summary>
    public partial class App : Application
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>バッチ終了時に Word の終了処理を待つ時間のうち、正常終了の待機上限を超えて許容する分 (強制終了の完了待ち)</summary>
        private static readonly TimeSpan PendingCleanupMargin = TimeSpan.FromSeconds(5);


        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>アプリケーション全体で共有するサービスプロバイダー</summary>
        private IServiceProvider? _serviceProvider;


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <inheritdoc/>
        protected override void OnStartup(StartupEventArgs e)
        {
            ToolTipService.InitialShowDelayProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(200));

            GlobalExceptionHandler.Register();

            UserSettingsBootstrapResult settingsBootstrap = UserSettingsInitializer.Initialize();

            PdfConverterTempDirectory.CleanupStaleEntries(PdfConverterTempDirectory.DefaultStaleAge);

            if (TryRunBatchMode(e.Args, settingsBootstrap))
            {
                return;
            }

            base.OnStartup(e);

            ThemeManager.Initialize();

            _serviceProvider = ServiceConfigurator.Configure(settingsBootstrap);
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            if (ApplicationStartupHelper.TryGetInitialDocumentPath(e.Args, out string? initialFilePath)
                && mainWindow.DataContext is IMainWindowViewModel viewModel)
            {
                mainWindow.Dispatcher.BeginInvoke(
                    new Action(() => viewModel.HandleDroppedDocument(initialFilePath)),
                    DispatcherPriority.Loaded);
            }

            if (settingsBootstrap.RecoveredFromCorruption)
            {
                var dialogService = _serviceProvider.GetRequiredService<IDialogService>();
                mainWindow.Dispatcher.BeginInvoke(
                    new Action(() => dialogService.ShowMessage(
                        "ユーザー設定ファイルが破損していたため、既定値に戻しました。",
                        "設定の初期化",
                        DialogIcon.Warning)),
                    DispatcherPriority.Loaded);
            }
        }

        /// <inheritdoc/>
        protected override void OnExit(ExitEventArgs e)
        {
            GlobalExceptionHandler.Unregister();
            ThemeManager.Shutdown();

            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            PdfConverterTempDirectory.CleanupInstanceDirectory();

            base.OnExit(e);
        }

        /// <summary>
        /// バッチ / ヘッドレスモードで起動された場合に変換を実行する
        /// </summary>
        /// <param name="args">起動引数</param>
        /// <param name="settingsBootstrap">起動時のユーザー設定初期化結果</param>
        /// <returns>true: バッチモードとして処理済み / false: GUI 起動を続行</returns>
        private bool TryRunBatchMode(string[] args, UserSettingsBootstrapResult settingsBootstrap)
        {
            if (!BatchCliParser.TryParse(args, out BatchCliOptions? options, out string? parseError))
            {
                return false;
            }

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            GlobalExceptionHandler.IsHeadless = true;
            int exitCode = BatchCliParser.InvalidArgumentsExitCode;

            try
            {
                if (options == null)
                {
                    BatchConsoleHelper.WriteErrorLine(parseError);
                    BatchConsoleHelper.WriteLine(string.Empty);
                    BatchConsoleHelper.WriteLine(BatchCliParser.GetHelpText());
                }
                else
                {
                    _serviceProvider = ServiceConfigurator.Configure(settingsBootstrap);
                    var batchExportService = _serviceProvider.GetRequiredService<IBatchExportService>();

                    using (var cancellationScope = new BatchCancellationScope())
                    {
                        exitCode = batchExportService.RunAsync(options, cancellationScope.Token).GetAwaiter().GetResult();
                    }
                }
            }
            catch (Exception ex)
            {
                ApplicationLogger.Error("Batch", "Batch mode failed", ex);
                BatchConsoleHelper.WriteErrorLine(ExceptionFormattingHelper.GetUserMessage(ex));
                exitCode = BatchCliParser.RuntimeErrorExitCode;
            }
            finally
            {
                // キャンセル直後は Word の終了処理が裏で続いているため、終わる前にプロセスを閉じて Word を残さないよう待つ
                MicrosoftWordToPdfConversionService.WaitForPendingCleanup(
                    MicrosoftWordToPdfConversionService.CleanupGracePeriod + PendingCleanupMargin);

                if (_serviceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                    _serviceProvider = null;
                }

                PdfConverterTempDirectory.CleanupInstanceDirectory();
                GlobalExceptionHandler.Unregister();
                GlobalExceptionHandler.IsHeadless = false;
                Shutdown(exitCode);
            }

            return true;
        }
    }
}
