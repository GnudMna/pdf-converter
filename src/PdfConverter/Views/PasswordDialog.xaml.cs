using System.Windows;

namespace PdfConverter.Views
{
    /// <summary>
    /// アプリのテーマに合わせたパスワード入力ダイアログ
    /// </summary>
    public partial class PasswordDialog : Window
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>入力されたパスワード (OK で閉じるまでは空文字)</summary>
        public string Password { get; private set; } = string.Empty;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定したメッセージとタイトルでパスワード入力ダイアログを初期化する
        /// </summary>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="title">ダイアログのタイトル</param>
        public PasswordDialog(string message, string title)
        {
            InitializeComponent();
            Title = title;
            TitleText.Text = title;
            MessageText.Text = message;
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// OK ボタンがクリックされたときの処理
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Password = PasswordInput.Password;
            DialogResult = true;
        }
    }
}
