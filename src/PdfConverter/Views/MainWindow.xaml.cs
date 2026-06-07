using System.Windows;
using PdfConverter.ViewModels;

namespace PdfConverter.Views
{
    /// <summary>
    /// アプリケーションのメインウィンドウ
    /// UI・ロジックを最小限に保ち、操作はすべてViewModelへ委譲する
    /// </summary>
    public partial class MainWindow : Window
    {
        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// ViewModelを受け取り、DataContextに設定してウィンドウを初期化する
        /// </summary>
        /// <param name="viewModel">バインドするViewModel</param>
        public MainWindow(IMainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
