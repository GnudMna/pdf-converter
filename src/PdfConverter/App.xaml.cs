using System;
using System.Runtime.InteropServices;
using System.Threading;
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
    /// 起動時にネイティブ DLL の検索パスを設定し、依存関係を組み立てる
    /// </summary>
    public partial class App : Application
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>アプリケーション全体で共有するサービスプロバイダー</summary>
        private IServiceProvider _serviceProvider;


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 指定したディレクトリをプロセスの DLL 検索パスに追加する Win32 API<br/>
        /// Docnet.Core が依存する PDFium ネイティブ DLL を <c>lib\</c> サブフォルダーから読み込めるようにするために <c>P/Invoke</c> で呼び出す
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        /// <inheritdoc/>
        protected override void OnStartup(StartupEventArgs e)
        {
            ToolTipService.InitialShowDelayProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(200));

            GlobalExceptionHandler.Register();

            UserSettingsBootstrapResult settingsBootstrap = UserSettingsInitializer.Initialize();

            PdfConverterTempDirectory.CleanupStaleEntries(PdfConverterTempDirectory.DefaultStaleAge);

            SetDllDirectory(ApplicationStartupHelper.GetNativeDllSearchPath(
                System.Reflection.Assembly.GetExecutingAssembly().Location));

            if (TryRunBatchMode(e.Args, settingsBootstrap))
            {
                return;
            }

            base.OnStartup(e);

            ThemeManager.Initialize();

            _serviceProvider = ServiceConfigurator.Configure(settingsBootstrap);
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            if (ApplicationStartupHelper.TryGetInitialDocumentPath(e.Args, out string initialFilePath)
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

            PdfConverterTempDirectory.TryCleanupAll();

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
            if (!BatchCliParser.TryParse(args, out BatchCliOptions options, out string parseError))
            {
                return false;
            }

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
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
                    exitCode = batchExportService.RunAsync(options, CancellationToken.None).GetAwaiter().GetResult();
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
                if (_serviceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                    _serviceProvider = null;
                }

                PdfConverterTempDirectory.TryCleanupAll();
                GlobalExceptionHandler.Unregister();
                Shutdown(exitCode);
            }

            return true;
        }
    }
}
