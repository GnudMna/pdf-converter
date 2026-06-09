using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PdfConverter.Infrastructure;
using PdfConverter.Themes;
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

            var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                         ?? AppDomain.CurrentDomain.BaseDirectory;
            SetDllDirectory(Path.Combine(exeDir, "lib"));

            base.OnStartup(e);

            ThemeManager.Initialize();

            _serviceProvider = ServiceConfigurator.Configure();
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
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

            base.OnExit(e);
        }
    }
}
