using System;
using System.Windows;
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
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// ViewModel を受け取り、DataContext に設定してウィンドウを初期化する
        /// </summary>
        /// <param name="viewModel">バインドする ViewModel</param>
        public MainWindow(IMainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            SourceInitialized += OnSourceInitialized;
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
