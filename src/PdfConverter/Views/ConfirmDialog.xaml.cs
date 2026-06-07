using System.Windows;
using System.Windows.Media;
using PdfConverter.Services;

namespace PdfConverter.Views
{
    /// <summary>
    /// アプリのテーマに合わせた確認ダイアログ
    /// </summary>
    public partial class ConfirmDialog : Window
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>ダイアログの結果</summary>
        public bool Result { get; private set; }


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定したメッセージ、タイトル、アイコン、肯定ボタンのラベル、否定ボタンのラベルを使用して確認ダイアログを初期化する
        /// </summary>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="title">ダイアログのタイトル</param>
        /// <param name="icon">表示するアイコン</param>
        /// <param name="yesText">肯定ボタンのラベル</param>
        /// <param name="noText">否定ボタンのラベル</param>
        public ConfirmDialog(string message, string title, DialogIcon icon, string yesText, string noText)
        {
            InitializeComponent();
            Title = title;
            TitleText.Text = title;
            MessageText.Text = GetSummaryMessage(message);
            YesButton.Content = yesText;
            NoButton.Content = noText;
            ApplyIcon(icon);

            string detail = GetDetailMessage(message);
            if (!string.IsNullOrEmpty(detail))
            {
                DetailText.Text = detail;
                DetailScroll.Visibility = Visibility.Visible;
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 肯定ボタンがクリックされたときの処理
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 否定ボタンがクリックされたときの処理
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// メッセージの要約を取得する
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <returns>要約</returns>
        private static string GetSummaryMessage(string message)
        {
            int separator = message.IndexOf("\n\n");
            return separator >= 0 ? message.Substring(0, separator) : message;
        }

        /// <summary>
        /// メッセージの詳細を取得する
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <returns>詳細</returns>
        private static string GetDetailMessage(string message)
        {
            int separator = message.IndexOf("\n\n");
            return separator >= 0 ? message.Substring(separator + 2) : null;
        }

        /// <summary>
        /// アイコンを適用する
        /// </summary>
        /// <param name="icon">表示するアイコン</param>
        private void ApplyIcon(DialogIcon icon)
        {
            switch (icon)
            {
                case DialogIcon.Error:
                    SetIcon(
                        (Brush)FindResource("StatusErrorBackgroundBrush"),
                        (Brush)FindResource("StatusErrorForegroundBrush"),
                        "M12 2 A10 10 0 1 0 12 22 A10 10 0 1 0 12 2 M8 8 L16 16 M16 8 L8 16");
                    break;
                case DialogIcon.Information:
                    SetIcon(
                        (Brush)FindResource("StatusInfoBackgroundBrush"),
                        (Brush)FindResource("StatusInfoForegroundBrush"),
                        "M12 2 A10 10 0 1 0 12 22 A10 10 0 1 0 12 2 M12 10 L12 11 M12 14 L12 16");
                    break;
                case DialogIcon.Warning:
                default:
                    SetIcon(
                        (Brush)FindResource("StatusWarningBackgroundBrush"),
                        (Brush)FindResource("StatusWarningForegroundBrush"),
                        "M12 2 L22 20 L2 20 Z M12 9 L12 14 M12 17 L12 17");
                    break;
            }
        }

        /// <summary>
        /// アイコンを設定する
        /// </summary>
        /// <param name="background">背景</param>
        /// <param name="foreground">前景</param>
        /// <param name="geometry">ジオメトリ</param>
        private void SetIcon(Brush background, Brush foreground, string geometry)
        {
            IconBorder.Background = background;
            IconPath.Fill = foreground;
            IconPath.Data = Geometry.Parse(geometry);
        }
    }
}
