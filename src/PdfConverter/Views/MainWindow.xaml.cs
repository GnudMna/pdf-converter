using System.Windows;
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
        }
    }
}
