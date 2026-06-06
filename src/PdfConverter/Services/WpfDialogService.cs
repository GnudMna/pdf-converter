using System.Windows;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;

namespace PdfConverter.Services
{
    /// <summary>
    /// WPF 標準ダイアログを使用する<see cref="IDialogService"/>の実装
    /// </summary>
    public class WpfDialogService : IDialogService
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public string ShowOpenPdfFileDialog()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDFファイル (*.pdf)|*.pdf",
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
        public void ShowMessage(string message, string title = "PDF Converter")
        {
            MessageBox.Show(message, title);
        }

        /// <inheritdoc/>
        public bool ShowYesNo(string message, string title, DialogIcon icon = DialogIcon.None)
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, ToMessageBoxImage(icon)) == MessageBoxResult.Yes;
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// <see cref="DialogIcon"/>をWPFの<see cref="MessageBoxImage"/>に変換する
        /// </summary>
        private static MessageBoxImage ToMessageBoxImage(DialogIcon icon)
        {
            switch (icon)
            {
                case DialogIcon.Warning:
                    return MessageBoxImage.Warning;
                case DialogIcon.Error:
                    return MessageBoxImage.Error;
                case DialogIcon.Information:
                    return MessageBoxImage.Information;
                default:
                    return MessageBoxImage.None;
            }
        }
    }
}
