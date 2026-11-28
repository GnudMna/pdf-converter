using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
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
        /// <summary>更新サービス</summary>
        private readonly IUpdateService _updateService;

        /// <summary>ユーザー設定サービス</summary>
        private readonly IUserSettingsService _userSettingsService;

        /// <summary>ダイアログサービス</summary>
        private readonly IDialogService _dialogService;

        /// <summary>本体が処理中かどうかを取得する関数 (処理中は更新を適用しない)</summary>
        private readonly Func<bool> _isBusy;

        /// <summary>更新の確認が進行中かどうか</summary>
        private bool _updateInProgress;

        /// <summary>ダウンロード中だけ存在する、更新の適用を取り消すためのトークン源</summary>
        private CancellationTokenSource? _applyCts;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 更新・設定・ダイアログサービスを受け取り初期化する
        /// </summary>
        /// <param name="updateService">更新サービス</param>
        /// <param name="userSettingsService">ユーザー設定サービス</param>
        /// <param name="dialogService">ダイアログサービス</param>
        /// <param name="isBusy">本体が処理中かどうかを取得する関数</param>
        public AboutDialog(
            IUpdateService updateService,
            IUserSettingsService userSettingsService,
            IDialogService dialogService,
            Func<bool> isBusy)
        {
            _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
            _userSettingsService = userSettingsService ?? throw new ArgumentNullException(nameof(userSettingsService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _isBusy = isBusy ?? throw new ArgumentNullException(nameof(isBusy));
            InitializeComponent();
            BindMetadata();
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// アプリのバージョン・著作権・同梱 OSS の情報を画面に反映し、使用許諾とライセンス全文のボタンの有効状態を決める
        /// </summary>
        private void BindMetadata()
        {
            ProductNameText.Text = ApplicationMetadata.ProductName;
            VersionText.Text = $"バージョン {ApplicationMetadata.Version}";
            CopyrightText.Text = ApplicationMetadata.Copyright;
            LicenseText.Text =
                $"本アプリは {ApplicationMetadata.ApplicationLicenseName} の下で公開されています。";
            OpenLicenseButton.IsEnabled = ApplicationMetadata.IsLicenseAvailable;
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

        /// <inheritdoc/>
        protected override void OnClosed(EventArgs e)
        {
            // ダウンロード中にダイアログを閉じたときは、通信を取り消して待ち続けないようにする
            _applyCts?.Cancel();
            base.OnClosed(e);
        }

        /// <summary>
        /// 更新を確認する<br/>
        /// ダウンロード中はこのボタンが「キャンセル」になり、クリックするとダウンロードを取り消す
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            // ダウンロード中はこのボタンが「キャンセル」になる
            if (_applyCts != null)
            {
                _applyCts.Cancel();
                CheckUpdateButton.IsEnabled = false;
                UpdateStatusText.Text = "ダウンロードをキャンセルしています…";
                return;
            }

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

        /// <summary>
        /// 更新確認の結果を利用者へ通知し、更新がある場合は確認のうえダウンロードして適用する
        /// </summary>
        /// <param name="result">更新確認の結果</param>
        /// <returns>非同期操作のタスク</returns>
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
                    if (NotifyIfBusy())
                    {
                        return;
                    }

                    string package = result.Update?.PackageKind == UpdatePackageKind.Portable
                        ? "ポータブル版"
                        : "インストーラー";
                    bool apply = _dialogService.ShowYesNo(
                        $"新しいバージョン {result.LatestVersion} が見つかりました。\n\n{package}をダウンロードして更新しますか？\n(更新中はアプリを終了し、完了後に自動で起動します)",
                        "更新の確認",
                        DialogIcon.Information,
                        "更新する",
                        "後で");
                    if (!apply || NotifyIfBusy())
                    {
                        return;
                    }

                    UpdateStatusText.Text = "ダウンロードしています…";
                    UpdateApplyResult applyResult;
                    using (var applyCts = new CancellationTokenSource())
                    {
                        _applyCts = applyCts;
                        CheckUpdateButton.Content = "キャンセル";
                        CheckUpdateButton.IsEnabled = true;
                        AutomationProperties.SetName(CheckUpdateButton, "ダウンロードをキャンセル");
                        try
                        {
                            applyResult = await _updateService
                                .ApplyUpdateAsync(result.Update, applyCts.Token)
                                .ConfigureAwait(true);
                        }
                        catch (OperationCanceledException)
                        {
                            UpdateStatusText.Text = "ダウンロードをキャンセルしました。";
                            return;
                        }
                        finally
                        {
                            _applyCts = null;
                            CheckUpdateButton.Content = "更新を確認";
                            AutomationProperties.SetName(CheckUpdateButton, "更新を確認");
                        }
                    }

                    if (applyResult.Kind == UpdateApplyKind.SetupStarted)
                    {
                        UpdateStatusText.Text = "更新を適用しています。完了後に自動で起動します。";
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

        /// <summary>
        /// 本体が処理中なら、更新を適用できない旨を通知する
        /// </summary>
        /// <returns>処理中で更新を見送るとき <c>true</c></returns>
        private bool NotifyIfBusy()
        {
            if (!_isBusy())
            {
                return false;
            }

            UpdateStatusText.Text = "処理中のため更新を適用できません。";
            _dialogService.ShowMessage(
                "画像の保存などの処理中は、更新を適用できません (更新を開始するとアプリを終了するため)。\n\n処理が完了してから、もう一度お試しください。",
                "更新の適用",
                DialogIcon.Warning);
            return true;
        }

        /// <summary>
        /// 本体が処理中なら、設定をリセットできない旨を通知する
        /// </summary>
        /// <returns>処理中でリセットを見送るとき <c>true</c></returns>
        private bool NotifyIfBusyForReset()
        {
            if (!_isBusy())
            {
                return false;
            }

            UpdateStatusText.Text = "処理中のため設定をリセットできません。";
            _dialogService.ShowMessage(
                "画像の保存や文書の読み込みなどの処理中は、設定をリセットできません (処理中のデータが失われるため)。\n\n処理が完了してから、もう一度お試しください。",
                "設定のリセット",
                DialogIcon.Warning);
            return true;
        }

        /// <summary>
        /// 確認のうえ、保存済みの設定をすべて既定値に戻す
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private void ResetSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (NotifyIfBusyForReset())
            {
                return;
            }

            bool confirmed = _dialogService.ShowYesNo(
                "テーマ・画像出力・Word 変換などの保存済み設定をすべて既定値に戻します。\n\nよろしいですか？",
                "設定のリセット",
                DialogIcon.Warning,
                "リセット",
                "キャンセル");
            if (!confirmed || NotifyIfBusyForReset())
            {
                return;
            }

            try
            {
                _userSettingsService.ResetToDefaults();
                UpdateStatusText.Text = "設定を既定値に戻しました。";
                _dialogService.ShowMessage(
                    "設定を既定値に戻しました。",
                    "設定のリセット",
                    DialogIcon.Information);
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = string.Empty;
                _dialogService.ShowMessage(
                    $"設定のリセットに失敗しました。\n\n{ex.Message}",
                    "設定のリセット",
                    DialogIcon.Error);
            }
        }

        /// <summary>
        /// 本アプリの使用許諾ファイルを関連付けられたアプリで開く
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private void OpenLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenWithShell(ApplicationMetadata.LicenseFilePath);
        }

        /// <summary>
        /// 同梱 OSS のライセンス全文ファイルを関連付けられたアプリで開く
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private void OpenNoticesButton_Click(object sender, RoutedEventArgs e)
        {
            OpenWithShell(ApplicationMetadata.ThirdPartyNoticesFilePath);
        }

        /// <summary>
        /// ファイルをシェルの関連付けで開く
        /// </summary>
        /// <param name="path">開くファイルのパス</param>
        private static void OpenWithShell(string path)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }

        /// <summary>
        /// ダイアログを閉じる
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
