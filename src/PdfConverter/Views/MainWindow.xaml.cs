using System.IO;
using System.Linq;
using System.Windows;
using PdfConverter.ViewModels;

namespace PdfConverter.Views
{
    /// <summary>
    /// アプリケーションのメインウィンドウ
    /// UI・ロジックを最小限に保ち、操作はすべて<see cref="MainViewModel"/>へ委譲する
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
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// パス入力欄からフォーカスが外れたときにPDFの読み込みを試行する
        /// </summary>
        private void FilePathTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.LoadPdfFromPath();
            }
        }

        /// <summary>
        /// ウィンドウへのファイルドロップを処理する<br/>
        /// 複数ファイルがドロップされた場合は先頭の<c>.pdf</c>ファイルのみを<see cref="MainViewModel"/>に渡す
        /// </summary>
        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string filePath = files?
                    .FirstOrDefault(f => Path.GetExtension(f).Equals(".pdf", System.StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(filePath) && DataContext is MainViewModel viewModel)
                {
                    viewModel.FilePath = filePath;
                    viewModel.LoadPdfFromPath(forceReload: true);
                }
            }
        }
    }
}
