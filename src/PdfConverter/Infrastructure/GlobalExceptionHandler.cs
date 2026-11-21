using System;
using System.Runtime.InteropServices;
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
        /*                                    定数                                      */
        /********************************************************************************/
        /// <summary>致命的な UI 例外で終了するときの終了コード</summary>
        internal const int FatalUiExceptionExitCode = 1;


        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>ハンドラ登録済みかどうか</summary>
        private static bool _isRegistered;

        /// <summary>ログ出力先ディレクトリ (テスト時に上書き可能)</summary>
        internal static string? LogDirectory
        {
            get => ApplicationLogger.LogDirectory;
            set => ApplicationLogger.LogDirectory = value;
        }

        /// <summary>致命的例外後の終了処理 (テスト時に差し替え可能)</summary>
        internal static Action ShutdownAfterFatalException { get; set; } = ShutdownApplicationAfterFatalException;


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
        /// <summary>
        /// プロセス状態の継続が危険な例外かどうかを判定する
        /// </summary>
        /// <param name="exception">判定する例外</param>
        /// <returns>true: 致命的 / false: 継続可能とみなす</returns>
        internal static bool IsFatalException(Exception exception)
        {
            if (exception == null)
            {
                return false;
            }

            Exception root = Unwrap(exception);
            return root is OutOfMemoryException
                || root is InsufficientMemoryException
                || root is AccessViolationException
                || root is SEHException
                || root is BadImageFormatException
                || root is TypeLoadException
                || root is AppDomainUnloadedException
                || root is StackOverflowException
                || root is NullReferenceException
                || root is InvalidCastException
                || root is IndexOutOfRangeException
                || root is RankException
                || root is ArrayTypeMismatchException;
        }

        /// <summary>現在のアプリケーションの Dispatcher を取得する</summary>
        private static Dispatcher? CurrentDispatcher => Application.Current?.Dispatcher;

        /// <summary>UI スレッド上で未処理例外が発生したときのハンドラ</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Exception exception = e.Exception;
            if (CancellationExceptionHelper.IsOrContainsCancellation(exception))
            {
                // キャンセルは正常系として握りつぶし、アプリは継続する
                e.Handled = true;
                return;
            }

            bool isFatal = IsFatalException(exception);
            Report(
                exception,
                "UI",
                isFatal ? ExceptionReportKind.Fatal : ExceptionReportKind.UserInterface);

            // 既定の WPF クラッシュダイアログと二重表示しないよう、常に Handled にする
            e.Handled = true;

            if (isFatal)
            {
                ShutdownAfterFatalException?.Invoke();
            }
        }

        /// <summary>バックグラウンドタスクの未観測例外が発生したときのハンドラ</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Report(e.Exception, "Task", ExceptionReportKind.Background);
            e.SetObserved();
        }

        /// <summary>致命的な未処理例外が発生したときのハンドラ</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                Report(exception, "AppDomain", ExceptionReportKind.Fatal);
            }
        }

        /// <summary>AggregateException を単一の根本原因例外に展開する</summary>
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
            ApplicationLogger.Error(source, $"[{kind}] {exception.Message}", exception);
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
            string detail = ExceptionFormattingHelper.GetUserMessage(exception);
            string message = kind == ExceptionReportKind.Fatal
                ? $"致命的なエラーが発生したため、アプリケーションを終了します。{Environment.NewLine}{Environment.NewLine}{detail}"
                : $"予期しないエラーが発生しました。{Environment.NewLine}{Environment.NewLine}{detail}";

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

        /// <summary>メインウィンドウの ViewModel のステータスバーへエラーを表示する</summary>
        /// <param name="message">メッセージ</param>
        /// <returns>true: 更新できた / false: 更新できなかった</returns>
        private static bool TryUpdateMainViewModelStatus(string message)
        {
            if (!(Application.Current?.MainWindow?.DataContext is IMainWindowViewModel viewModel))
            {
                return false;
            }

            viewModel.IsBusy = false;
            viewModel.IsProgressBarVisible = false;
            viewModel.SetStatus(message, StatusKind.Error);
            return true;
        }

        /// <summary>致命的な UI 例外の後にアプリケーションを終了する</summary>
        private static void ShutdownApplicationAfterFatalException()
        {
            try
            {
                Application.Current?.Shutdown(FatalUiExceptionExitCode);
            }
            catch (Exception ex)
            {
                ApplicationLogger.Error("UI", "致命的例外後の終了処理に失敗しました。", ex);
            }
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

        /// <summary>致命的エラー (ログ・ダイアログ・必要に応じて終了)</summary>
        Fatal,
    }
}
