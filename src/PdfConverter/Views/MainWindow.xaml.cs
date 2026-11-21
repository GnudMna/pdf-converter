using System;
using System.ComponentModel;
using System.Windows;
using PdfConverter.Services;
using PdfConverter.Themes;
using PdfConverter.ViewModels;

namespace PdfConverter.Views
{
    /// <summary>
    /// アプリケーションのメインウィンドウ
    /// UI・ロジックを最小限に保ち、操作はすべて ViewModel へ委譲する
    /// </summary>
    public partial class MainWindow : Window
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>バインドしている ViewModel</summary>
        private readonly IMainWindowViewModel _viewModel;

        /// <summary>ダイアログサービス</summary>
        private readonly IDialogService _dialogService;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// ViewModel を受け取り、DataContext に設定してウィンドウを初期化する
        /// </summary>
        /// <param name="viewModel">バインドする ViewModel</param>
        /// <param name="dialogService">ダイアログサービス</param>
        public MainWindow(IMainWindowViewModel viewModel, IDialogService dialogService)
        {
            InitializeComponent();
            DataContext = viewModel;
            _viewModel = viewModel;
            _dialogService = dialogService;

            SourceInitialized += OnSourceInitialized;
            Closing += OnClosing;
            Closed += OnClosed;
        }


        /********************************************************************************/
        /*                               イベントハンドラ                               */
        /********************************************************************************/
        /// <summary>
        /// ウィンドウのハンドル作成後に、タイトルバーの色をテーマへ合わせ、テーマ切り替えへの追従を始める
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            WindowTitleBarHelper.Apply(this);
            ThemeManager.ThemeApplied += OnThemeApplied;
        }

        /// <summary>
        /// テーマの切り替えに合わせてタイトルバーの色を更新する
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private void OnThemeApplied(object? sender, EventArgs e)
        {
            WindowTitleBarHelper.Apply(this);
        }

        /// <summary>
        /// 処理中にウィンドウを閉じようとしたとき、確認のうえ処理をキャンセルして終了する<br/>
        /// 終了後の Word・LibreOffice の後片付けは <see cref="App.OnExit"/> が待つ
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (!_viewModel.IsBusy)
            {
                return;
            }

            bool confirmed = _dialogService.ShowYesNo(
                "処理中です。処理を中止してアプリを終了しますか？",
                "終了の確認",
                DialogIcon.Warning,
                "中止して終了",
                "続行");

            if (!confirmed)
            {
                e.Cancel = true;
                return;
            }

            _viewModel.CancelCurrentOperation();
        }

        /// <summary>
        /// ウィンドウを閉じるときに、テーマ切り替えの購読を解除する
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private void OnClosed(object? sender, EventArgs e)
        {
            ThemeManager.ThemeApplied -= OnThemeApplied;
        }
    }
}
