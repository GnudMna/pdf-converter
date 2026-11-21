using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using PdfConverter.Infrastructure;
using PdfConverter.Services;

namespace PdfConverter.Views
{
    /// <summary>
    /// バージョン情報とオープンソースクレジットを表示するダイアログ
    /// </summary>
    public partial class AboutDialog : Window
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private readonly IUpdateService _updateService;
        private readonly IDialogService _dialogService;
        private bool _updateInProgress;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 更新・ダイアログサービスを受け取り初期化する
        /// </summary>
        /// <param name="updateService">更新サービス</param>
        /// <param name="dialogService">ダイアログサービス</param>
        public AboutDialog(IUpdateService updateService, IDialogService dialogService)
        {
            _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
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

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_updateInProgress)
            {
                return;
            }

            _updateInProgress = true;
            CheckUpdateButton.IsEnabled = false;
            UpdateStatusText.Text = "更新を確認しています…";

            try
            {
                UpdateCheckResult result = await _updateService.CheckForUpdateAsync().ConfigureAwait(true);
                await HandleCheckResultAsync(result).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                UpdateStatusText.Text = "更新確認をキャンセルしました。";
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = string.Empty;
                _dialogService.ShowMessage(
                    $"更新の確認に失敗しました。\n\n{ex.Message}",
                    "更新の確認",
                    DialogIcon.Error);
            }
            finally
            {
                _updateInProgress = false;
                CheckUpdateButton.IsEnabled = true;
            }
        }

        private async Task HandleCheckResultAsync(UpdateCheckResult result)
        {
            switch (result.Kind)
            {
                case UpdateCheckKind.UpToDate:
                    UpdateStatusText.Text = "最新バージョンです。";
                    _dialogService.ShowMessage(
                        $"お使いのバージョンは最新です。\n\n現在: {result.CurrentVersion}\n最新: {result.LatestVersion}",
                        "更新の確認",
                        DialogIcon.Information);
                    break;

                case UpdateCheckKind.Available:
                    UpdateStatusText.Text = $"更新があります: {result.LatestVersion}";
                    bool apply = _dialogService.ShowYesNo(
                        $"新しいバージョン {result.LatestVersion} が見つかりました。\n\nインストーラーをダウンロードして更新しますか？\n（更新を開始するとアプリを終了します）",
                        "更新の確認",
                        DialogIcon.Information,
                        "更新する",
                        "後で");
                    if (!apply)
                    {
                        return;
                    }

                    UpdateStatusText.Text = "ダウンロードしています…";
                    CheckUpdateButton.IsEnabled = false;
                    UpdateApplyResult applyResult =
                        await _updateService.ApplyUpdateAsync(result.Update).ConfigureAwait(true);
                    if (applyResult.Kind == UpdateApplyKind.SetupStarted)
                    {
                        UpdateStatusText.Text = "インストーラーを起動しました。";
                        Application.Current?.Shutdown();
                        return;
                    }

                    UpdateStatusText.Text = string.Empty;
                    _dialogService.ShowMessage(
                        applyResult.ErrorMessage ?? "更新の適用に失敗しました。",
                        "更新の適用",
                        DialogIcon.Error);
                    break;

                default:
                    UpdateStatusText.Text = string.Empty;
                    _dialogService.ShowMessage(
                        result.ErrorMessage ?? "更新の確認に失敗しました。",
                        "更新の確認",
                        DialogIcon.Error);
                    break;
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
