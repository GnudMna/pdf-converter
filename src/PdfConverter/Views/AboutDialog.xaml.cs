using System.Diagnostics;
using System.Windows;
using PdfConverter.Infrastructure;

namespace PdfConverter.Views
{
    /// <summary>
    /// バージョン情報とオープンソースクレジットを表示するダイアログ
    /// </summary>
    public partial class AboutDialog : Window
    {
        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        public AboutDialog()
        {
            InitializeComponent();
            BindMetadata();
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        private void BindMetadata()
        {
            ProductNameText.Text = ApplicationMetadata.ProductName;
            VersionText.Text = $"バージョン {ApplicationMetadata.Version}";
            CopyrightText.Text = ApplicationMetadata.Copyright;
            LicenseText.Text =
                $"本アプリは {ApplicationMetadata.ApplicationLicenseName} の下で公開されています。";
            BundledComponentsText.Text = ApplicationMetadata.BundledComponentsText;
            ExternalDependenciesText.Text = ApplicationMetadata.ExternalDependenciesText;

            if (ApplicationMetadata.IsThirdPartyNoticesAvailable)
            {
                NoticesHintText.Text =
                    $"{ApplicationMetadata.ThirdPartyNoticesFileName} に各コンポーネントのライセンス全文が記載されています。";
                OpenNoticesButton.IsEnabled = true;
            }
            else
            {
                NoticesHintText.Text =
                    $"ライセンス全文 ({ApplicationMetadata.ThirdPartyNoticesFileName}) はインストール版に同梱されます。";
                OpenNoticesButton.IsEnabled = false;
            }
        }

        private void OpenNoticesButton_Click(object sender, RoutedEventArgs e)
        {
            string path = ApplicationMetadata.ThirdPartyNoticesFilePath;
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
