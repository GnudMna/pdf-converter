using PdfConverter.Models;

namespace PdfConverter.ViewModels
{
    /// <summary>
    /// ステータスバー (メッセージ・進捗表示) の UI 状態を管理する ViewModel
    /// </summary>
    public sealed class StatusBarViewModel : ViewModelBase
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>進捗値</summary>
        private double _progressValue;

        /// <summary>プログレスバーを表示するかどうか</summary>
        private bool _isProgressBarVisible;

        /// <summary>進捗を不確定表示にするかどうか</summary>
        private bool _isProgressIndeterminate;

        /// <summary>保存中かどうか</summary>
        private bool _isSaving;

        /// <summary>ステータスメッセージ</summary>
        private string _statusMessage = "ファイルを選択して開始してください。";

        /// <summary>ステータスバーの表示種類</summary>
        private StatusKind _statusKind = StatusKind.Info;


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>進捗値</summary>
        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        /// <summary>プログレスバーを表示するかどうか</summary>
        public bool IsProgressBarVisible
        {
            get => _isProgressBarVisible;
            set
            {
                if (!SetProperty(ref _isProgressBarVisible, value))
                {
                    return;
                }

                UpdateProgressIndeterminate();
            }
        }

        /// <summary>
        /// 進捗を不確定 (マーキー) 表示にするかどうか<br/>
        /// ファイル読み込み中は具体的な進捗値を持たないため不確定表示にする
        /// </summary>
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            private set => SetProperty(ref _isProgressIndeterminate, value);
        }

        /// <summary>ステータスメッセージ</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>ステータスバーの表示種類</summary>
        public StatusKind StatusKind
        {
            get => _statusKind;
            set => SetProperty(ref _statusKind, value);
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>ステータスメッセージと表示種類をまとめて設定する</summary>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="kind">表示種類</param>
        public void SetStatus(string message, StatusKind kind = StatusKind.Info)
        {
            StatusMessage = message;
            StatusKind = kind;
        }

        /// <summary>処理中状態の変化に応じて不確定進捗表示を更新する</summary>
        /// <param name="isSaving">保存中かどうか</param>
        public void NotifyBusyStateChanged(bool isSaving)
        {
            _isSaving = isSaving;
            UpdateProgressIndeterminate();
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>不確定進捗表示フラグを再計算する</summary>
        private void UpdateProgressIndeterminate()
        {
            IsProgressIndeterminate = IsProgressBarVisible && !_isSaving;
        }
    }
}
