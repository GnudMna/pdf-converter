using System.Windows;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using PdfConverter.Views;

namespace PdfConverter.Services
{
    /// <summary>
    /// WPF 標準ダイアログを使用する <see cref="IDialogService"/> の実装
    /// </summary>
    public class WpfDialogService : IDialogService
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public string ShowOpenDocumentFileDialog()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "対応ドキュメント (*.pdf;*.doc;*.docx)|*.pdf;*.doc;*.docx|PDF (*.pdf)|*.pdf|Word (*.doc;*.docx)|*.doc;*.docx",
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        /// <inheritdoc/>
        public string ShowFolderBrowserDialog()
        {
            var dialog = new VistaFolderBrowserDialog();
            return dialog.ShowDialog() == true ? dialog.SelectedPath : null;
        }

        /// <inheritdoc/>
        public string ShowSavePdfFileDialog(string suggestedFileName)
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
    }
}
