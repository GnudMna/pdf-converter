using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.ViewModels;

namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// アプリケーション全体の未処理例外を捕捉し、ログ記録とユーザー通知を行う
    /// </summary>
    public static class GlobalExceptionHandler
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>ハンドラ登録済みかどうか</summary>
        private static bool _isRegistered;

        /// <summary>ログ出力先ディレクトリ(テスト時に上書き可能)</summary>
        internal static string LogDirectory { get; set; }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 未処理例外ハンドラを登録する
        /// </summary>
        public static void Register()
        {
            if (_isRegistered)
            {
                return;
            }

            if (CurrentDispatcher != null)
            {
                CurrentDispatcher.UnhandledException += OnDispatcherUnhandledException;
            }

            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            _isRegistered = true;
        }

        /// <summary>
        /// 未処理例外ハンドラを解除する
        /// </summary>
        public static void Unregister()
        {
            if (!_isRegistered)
            {
                return;
            }

            if (CurrentDispatcher != null)
            {
                CurrentDispatcher.UnhandledException -= OnDispatcherUnhandledException;
            }

            AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            _isRegistered = false;
        }

        /// <summary>
        /// 未処理例外を報告する
        /// </summary>
        /// <param name="exception">報告する例外</param>
        /// <param name="source">発生元の識別子</param>
        /// <param name="kind">通知の重要度</param>
        public static void Report(Exception exception, string source, ExceptionReportKind kind = ExceptionReportKind.Background)
        {
            if (exception == null || CancellationExceptionHelper.IsOrContainsCancellation(exception))
            {
                return;
            }

            Exception root = Unwrap(exception);
            WriteLog(root, source, kind);
            NotifyUser(root, source, kind);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>現在のアプリケーションのDispatcherを取得する</summary>
        private static Dispatcher CurrentDispatcher => Application.Current?.Dispatcher;

        /// <summary>UIスレッド上で未処理例外が発生したときのハンドラ</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Report(e.Exception, "UI", ExceptionReportKind.UserInterface);
            e.Handled = true;
        }

        /// <summary>バックグラウンドタスクの未観測例外が発生したときのハンドラ</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Report(e.Exception, "Task", ExceptionReportKind.Background);
            e.SetObserved();
        }

        /// <summary>致命的な未処理例外が発生したときのハンドラ</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                Report(exception, "AppDomain", ExceptionReportKind.Fatal);
            }
        }

        /// <summary>AggregateExceptionを単一の根本原因例外に展開する</summary>
        /// <param name="exception">展開する例外</param>
        /// <returns>展開後の例外</returns>
        private static Exception Unwrap(Exception exception)
        {
            if (exception is AggregateException aggregate)
            {
                return aggregate.Flatten().InnerException ?? exception;
            }

            return exception;
        }

        /// <summary>例外情報をログファイルへ追記する</summary>
        /// <param name="exception">ログに記録する例外</param>
        /// <param name="source">例外の発生元</param>
        /// <param name="kind">例外の種類</param>
        private static void WriteLog(Exception exception, string source, ExceptionReportKind kind)
        {
            try
            {
                string directory = LogDirectory ?? GetDefaultLogDirectory();
                Directory.CreateDirectory(directory);
                string logPath = Path.Combine(directory, "error.log");
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{kind}] [{source}] {exception.Message}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";
                File.AppendAllText(logPath, entry);
            }
            catch
            {
            }
        }

        /// <summary>ユーザーへ通知する</summary>
        /// <param name="exception">通知する例外</param>
        /// <param name="source">例外の発生元</param>
        /// <param name="kind">例外の種類</param>
        private static void NotifyUser(Exception exception, string source, ExceptionReportKind kind)
        {
            var dispatcher = CurrentDispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() => NotifyUserCore(exception, source, kind)));
                return;
            }

            NotifyUserCore(exception, source, kind);
        }

        /// <summary>UI スレッド上でユーザーへ通知する</summary>
        /// <param name="exception">通知する例外</param>
        /// <param name="source">例外の発生元</param>
        /// <param name="kind">例外の種類</param>
        private static void NotifyUserCore(Exception exception, string source, ExceptionReportKind kind)
        {
            string message = $"予期しないエラーが発生しました。{Environment.NewLine}{Environment.NewLine}{exception.Message}";

            if (TryUpdateMainViewModelStatus(message))
            {
                if (kind == ExceptionReportKind.Background)
                {
                    return;
                }
            }

            if (kind == ExceptionReportKind.Background)
            {
                return;
            }

            MessageBox.Show(
                message,
                "PDF Converter - エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        /// <summary>メインウィンドウのViewModelのステータスバーへエラーを表示する</summary>
        /// <param name="message">メッセージ</param>
        /// <returns>true: 更新できた / false: 更新できなかった</returns>
        private static bool TryUpdateMainViewModelStatus(string message)
        {
            if (!(Application.Current?.MainWindow?.DataContext is IMainWindowViewModel viewModel))
            {
                return false;
            }

            viewModel.IsBusy = false;
            viewModel.SetStatus(message, StatusKind.Error);
            return true;
        }

        /// <summary>既定のログ出力先ディレクトリを返す</summary>
        private static string GetDefaultLogDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "PDF Converter", "logs");
        }
    }

    /// <summary>
    /// 例外報告時の通知重要度
    /// </summary>
    public enum ExceptionReportKind
    {
        /// <summary>ログとステータスバーのみ</summary>
        Background,

        /// <summary>ログ・ステータスバー・ダイアログ</summary>
        UserInterface,

        /// <summary>致命的エラー(ログとダイアログ)</summary>
        Fatal,
    }
}
