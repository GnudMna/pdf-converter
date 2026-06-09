using System.Windows.Controls;

namespace PdfConverter.Views
{
    /// <summary>
    /// 処理中にプレビュー領域へ表示するオーバーレイ
    /// </summary>
    public partial class BusyOverlay : UserControl
    {
        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public BusyOverlay()
        {
            InitializeComponent();
        }
    }
}
