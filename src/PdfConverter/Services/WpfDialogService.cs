using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using PdfConverter.Infrastructure;
using PdfConverter.Views;

namespace PdfConverter.Services
{
    /// <summary>
    /// WPF 標準ダイアログを使用する <see cref="IDialogService"/> の実装
    /// </summary>
    public class WpfDialogService : IDialogService
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>更新サービス (バージョン情報ダイアログに渡す)</summary>
        private readonly IUpdateService _updateService;

        /// <summary>ユーザー設定サービス (バージョン情報ダイアログに渡す)</summary>
        private readonly IUserSettingsService _userSettingsService;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 更新・ユーザー設定サービスを受け取り初期化する
        /// </summary>
        /// <param name="updateService">更新サービス</param>
        /// <param name="userSettingsService">ユーザー設定サービス</param>
        public WpfDialogService(IUpdateService updateService, IUserSettingsService userSettingsService)
        {
            _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
            _userSettingsService = userSettingsService ?? throw new ArgumentNullException(nameof(userSettingsService));
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public string? ShowOpenDocumentFileDialog()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "対応ドキュメント (*.pdf;*.doc;*.docx)|*.pdf;*.doc;*.docx|PDF (*.pdf)|*.pdf|Word (*.doc;*.docx)|*.doc;*.docx",
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        /// <inheritdoc/>
        public string? ShowFolderBrowserDialog()
        {
            var dialog = new VistaFolderBrowserDialog();
            return dialog.ShowDialog() == true ? dialog.SelectedPath : null;
        }

        /// <inheritdoc/>
        public string? ShowSavePdfFileDialog(string suggestedFileName)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PDF ファイル (*.pdf)|*.pdf",
                FileName = suggestedFileName,
                DefaultExt = ".pdf",
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        /// <inheritdoc/>
        public bool ShowYesNo(
            string message,
            string title,
            DialogIcon icon = DialogIcon.None,
            string yesText = "はい",
            string noText = "いいえ")
        {
            var owner = Application.Current?.MainWindow;
            var confirmDialog = new ConfirmDialog(message, title, icon, yesText, noText)
            {
                Owner = owner,
            };

            return confirmDialog.ShowDialog() == true;
        }

        /// <inheritdoc/>
        public void ShowMessage(
            string message,
            string title,
            DialogIcon icon = DialogIcon.Information,
            string okText = "OK")
        {
            var owner = Application.Current?.MainWindow;
            var confirmDialog = new ConfirmDialog(message, title, icon, okText)
            {
                Owner = owner,
            };

            confirmDialog.ShowDialog();
        }

        /// <inheritdoc/>
        public string? ShowPasswordDialog(string message, string title)
        {
            var owner = Application.Current?.MainWindow;
            var passwordDialog = new PasswordDialog(message, title)
            {
                Owner = owner,
            };

            return passwordDialog.ShowDialog() == true ? passwordDialog.Password : null;
        }

        /// <inheritdoc/>
        public void ShowAbout(Func<bool> isBusy)
        {
            var owner = Application.Current?.MainWindow;
            var aboutDialog = new AboutDialog(_updateService, _userSettingsService, this, isBusy)
            {
                Owner = owner,
            };

            aboutDialog.ShowDialog();
        }

        /// <inheritdoc/>
        public void OpenUserDocs()
        {
            if (!ApplicationMetadata.IsUserDocsAvailable)
            {
                ShowMessage(
                    "ドキュメントはインストール版に同梱されます。",
                    "ドキュメント",
                    DialogIcon.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ApplicationMetadata.UserDocsIndexFilePath,
                    UseShellExecute = true,
                });
            }
            catch (Win32Exception ex)
            {
                ShowMessage(
                    $"ドキュメントを開けませんでした。\n\n{ex.Message}",
                    "ドキュメント",
                    DialogIcon.Error);
            }
        }
    }
}
