using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PdfConverter.Commands;
using PdfConverter.Infrastructure;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.ViewModels.Coordinators;

namespace PdfConverter.ViewModels
{
    /// <summary>
    /// メインウィンドウの ViewModel<br/>
    /// UI バインディングとコマンドを公開し、処理は Coordinator に委譲する
    /// </summary>
    public class MainViewModel : ViewModelBase, IMainWindowViewModel, IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /* ----------------------------- 依存関係注入関連 ----------------------------- */
        /// <summary>ダイアログサービス</summary>
        private readonly IDialogService _dialogService;

        /// <summary>クリップボードサービス</summary>
        private readonly IClipboardService _clipboardService;

        /// <summary>ユーザー設定サービス</summary>
        private readonly IUserSettingsService _userSettingsService;

        /* --------------------------- コーディネーター関連 --------------------------- */
        /// <summary>Coordinator 向けホストアダプター</summary>
        private readonly IMainViewModelHost _coordinatorHost;

        /// <summary>PDF プレビュー コーディネーター</summary>
        private readonly IPdfPreviewCoordinator _previewCoordinator;

        /// <summary>PDF 保存 コーディネーター</summary>
        private readonly IPdfSaveCoordinator _saveCoordinator;

        /* ------------------------------- 状態管理関連 ------------------------------- */
        /// <summary>処理中かどうか</summary>
        private bool _isBusy;

        /// <summary>保存中かどうか</summary>
        private bool _isSaving;

        /// <summary>キャンセルトークンソース</summary>
        private CancellationTokenSource? _cancelTokenSource;

        /* ------------------------------- 入力情報関連 ------------------------------- */
        /// <summary>読み込むドキュメントのパス</summary>
        private string? _filePath;

        /// <summary>読み込み済みドキュメントのパス</summary>
        private string? _loadedFilePath;

        /// <summary>入力途中の値を反映中かどうか (反映中は設定変更による再読み込み・再描画を行わない)</summary>
        private bool _isCommittingPendingInput;

        /* ----------------------------- オーバーレイ関連 ----------------------------- */
        /// <summary>ドラッグ&amp;ドロップオーバーレイを表示するかどうか</summary>
        private bool _isDropOverlayVisible;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        public MainViewModel(
            IDialogService dialogService,
            IClipboardService clipboardService,
            IPdfPreviewCoordinator previewCoordinator,
            IPdfSaveCoordinator saveCoordinator,
            IWordToPdfConversionSettings wordToPdfSettings,
            IImageExportSettings imageExportSettings,
            IUserSettingsService userSettingsService)
        {
            _dialogService = dialogService;
            _clipboardService = clipboardService;
            _previewCoordinator = previewCoordinator;
            _saveCoordinator = saveCoordinator;
            _userSettingsService = userSettingsService ?? throw new ArgumentNullException(nameof(userSettingsService));
            _coordinatorHost = new MainViewModelCoordinatorHost(this);

            Theme = new ThemeSettingsViewModel();
            Status = new StatusBarViewModel();
            Preview = new PreviewPanelViewModel(
                _previewCoordinator,
                () => _coordinatorHost,
                () => IsBusy,
                OnAsyncCommandException);
            WordSettings = new WordConversionSettingsViewModel(wordToPdfSettings, ReloadWordDocumentIfLoaded);
            ExportSettings = new ImageExportSettingsViewModel(
                imageExportSettings,
                RefreshPreviewIfLoaded,
                RaiseActionCanExecuteChanged);

            BrowseCommand = new RelayCommand(OnBrowse, () => !IsBusy);
            SaveCommand = new AsyncRelayCommand(() => _saveCoordinator.SaveAsync(_coordinatorHost), () => !string.IsNullOrEmpty(FilePath) && !IsBusy, OnAsyncCommandException);
            SavePdfCommand = new AsyncRelayCommand(() => _saveCoordinator.SavePdfAsync(_coordinatorHost), CanSavePdf, OnAsyncCommandException);
            CancelCommand = new RelayCommand(CancelOperation, () => IsBusy);
            CopyToClipboardCommand = new AsyncRelayCommand(OnCopyToClipboardAsync, CanCopyToClipboard, OnAsyncCommandException);
            LoadPdfFromPathCommand = new RelayCommand(() => LoadPdfFromPath(), () => !IsBusy);
            ShowAboutCommand = new RelayCommand(() => _dialogService.ShowAbout());
            OpenUserDocsCommand = new RelayCommand(() => _dialogService.OpenUserDocs());

            _userSettingsService.SettingsReset += OnUserSettingsReset;
        }


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>外観テーマ設定</summary>
        public ThemeSettingsViewModel Theme { get; }

        /// <summary>ステータスバー表示</summary>
        public StatusBarViewModel Status { get; }

        /// <summary>プレビューパネル</summary>
        public PreviewPanelViewModel Preview { get; }

        /// <summary>Word → PDF 変換設定</summary>
        public WordConversionSettingsViewModel WordSettings { get; }

        /// <summary>画像出力設定</summary>
        public ImageExportSettingsViewModel ExportSettings { get; }

        /* ------------------------------- 状態管理関連 ------------------------------- */
        /// <summary>処理中かどうか</summary>
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (!SetProperty(ref _isBusy, value))
                {
                    return;
                }

                Preview.IsBusy = value;
                Status.NotifyBusyStateChanged(IsSaving);
                RaiseCanExecuteChanged(BrowseCommand);
                RaiseCanExecuteChanged(SaveCommand);
                RaiseCanExecuteChanged(CancelCommand);
                RaiseCanExecuteChanged(LoadPdfFromPathCommand);
                Preview.RaiseNavigationCanExecuteChanged();
                RaiseActionCanExecuteChanged();
            }
        }

        /// <summary>保存中かどうか</summary>
        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                if (!SetProperty(ref _isSaving, value))
                {
                    return;
                }

                Status.NotifyBusyStateChanged(value);
            }
        }

        /// <summary>Word から変換した PDF の保存ボタンを表示するかどうか</summary>
        public bool IsSavePdfVisible =>
            !string.IsNullOrEmpty(FilePath)
            && DocumentFileHelper.IsWordFile(FilePath)
            && Preview.PageCount > 0;

        /* ------------------------------- 入力情報関連 ------------------------------- */
        /// <summary>読み込むドキュメントのパス</summary>
        public string? FilePath
        {
            get => _filePath;
            set
            {
                if (!SetProperty(ref _filePath, value))
                {
                    return;
                }

                Preview.NotifyFilePathChanged();
                OnPropertyChanged(nameof(HasFilePath));
                OnPropertyChanged(nameof(FileDisplayName));
                OnPropertyChanged(nameof(IsSavePdfVisible));
                RaiseCanExecuteChanged(SaveCommand);
            }
        }

        /// <summary>ドキュメントのパスが入力されているかどうか</summary>
        public bool HasFilePath => !string.IsNullOrWhiteSpace(FilePath);

        /// <summary>ヘッダーに表示するファイル名 (未入力のときは案内文)</summary>
        public string FileDisplayName => HasFilePath
            ? Path.GetFileName(FilePath!.Trim())
            : "ファイルが選択されていません";

        /* ------------------------------- 進捗表示関連 (Status 委譲) ------------------------------- */
        /// <summary>進捗値</summary>
        public double ProgressValue
        {
            get => Status.ProgressValue;
            set => Status.ProgressValue = value;
        }

        /// <summary>プログレスバーを表示するかどうか</summary>
        public bool IsProgressBarVisible
        {
            get => Status.IsProgressBarVisible;
            set => Status.IsProgressBarVisible = value;
        }

        /* ---------------------------- ステータス表示関連 (Status 委譲) ---------------------------- */
        /// <summary>ステータスメッセージ</summary>
        public string StatusMessage
        {
            get => Status.StatusMessage;
            set => Status.StatusMessage = value;
        }

        /// <summary>ステータスバーの表示種類</summary>
        public StatusKind StatusKind
        {
            get => Status.StatusKind;
            set => Status.StatusKind = value;
        }

        /* ----------------------------- オーバーレイ関連 ----------------------------- */
        /// <summary>ドラッグ&amp;ドロップオーバーレイを表示するかどうか</summary>
        public bool IsDropOverlayVisible
        {
            get => _isDropOverlayVisible;
            set => SetProperty(ref _isDropOverlayVisible, value);
        }


        /********************************************************************************/
        /*                                   コマンド                                   */
        /********************************************************************************/
        /// <summary>ファイルを選択するコマンド</summary>
        public ICommand BrowseCommand { get; }

        /// <summary>前のページに移動するコマンド</summary>
        public ICommand PreviousPageCommand => Preview.PreviousPageCommand;

        /// <summary>次のページに移動するコマンド</summary>
        public ICommand NextPageCommand => Preview.NextPageCommand;

        /// <summary>指定したページに移動するコマンド</summary>
        public ICommand GoToPageCommand => Preview.GoToPageCommand;

        /// <summary>保存するコマンド</summary>
        public ICommand SaveCommand { get; }

        /// <summary>Word から変換した PDF を保存するコマンド</summary>
        public ICommand SavePdfCommand { get; }

        /// <summary>プレビュー画像をクリップボードにコピーするコマンド</summary>
        public ICommand CopyToClipboardCommand { get; }

        /// <summary>キャンセルするコマンド</summary>
        public ICommand CancelCommand { get; }

        /// <summary>パス入力欄のフォーカス喪失時に PDF を読み込むコマンド</summary>
        public ICommand LoadPdfFromPathCommand { get; }

        /// <summary>バージョン情報を表示するコマンド</summary>
        public ICommand ShowAboutCommand { get; }

        /// <summary>利用者向けドキュメントを開くコマンド</summary>
        public ICommand OpenUserDocsCommand { get; }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 指定したファイルパスの PDF を検証して読み込み、ページ数取得とプレビュー生成を開始する
        /// </summary>
        /// <param name="forceReload">強制的に再読み込みするかどうか</param>
        public void LoadPdfFromPath(bool forceReload = false) => _previewCoordinator.LoadFromPath(_coordinatorHost, forceReload);

        /// <inheritdoc/>
        public void HandleDroppedDocument(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            if (IsBusy)
            {
                SetStatus(DocumentFileHelper.BusyLoadRejectedMessage, StatusKind.Warning);
                return;
            }

            FilePath = filePath;
            LoadPdfFromPath(forceReload: true);
        }

        /// <inheritdoc/>
        public void CommitPendingInput(Action commitInput)
        {
            _isCommittingPendingInput = true;
            try
            {
                commitInput();
            }
            finally
            {
                _isCommittingPendingInput = false;
            }
        }

        /// <inheritdoc/>
        public void NotifyMultipleFilesDropped(int droppedCount, string loadedFilePath)
        {
            string message =
                $"{droppedCount} 件のファイルがドロップされました。" + Environment.NewLine +
                "読み込めるのは 1 ファイルのみのため、先頭の対応ファイルだけを読み込みました。" + Environment.NewLine +
                Environment.NewLine +
                $"読み込んだファイル: {Path.GetFileName(loadedFilePath)}";

            void Show() => _dialogService.ShowMessage(message, "複数ファイルのドロップ", DialogIcon.Information);

            // ドラッグ元 (エクスプローラーなど) をドロップ処理中に止めないよう、イベント完了後に表示する
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                Show();
                return;
            }

            dispatcher.BeginInvoke(new Action(Show));
        }

        /// <inheritdoc/>
        public void SetStatus(string message, StatusKind kind = StatusKind.Info) => Status.SetStatus(message, kind);

        /// <summary>
        /// 実行中の処理をキャンセルし、キャンセルトークンを破棄する
        /// </summary>
        public void Dispose()
        {
            _cancelTokenSource?.Cancel();
            _cancelTokenSource?.Dispose();
            _cancelTokenSource = null;
            GC.SuppressFinalize(this);
        }


        /********************************************************************************/
        /*                        Coordinator ホスト向け内部 API                        */
        /********************************************************************************/
        /// <summary>読み込み済みドキュメントのパス (Coordinator ホスト専用)</summary>
        internal string? LoadedFilePath
        {
            get => _loadedFilePath;
            set => _loadedFilePath = value;
        }

        /// <summary><see cref="IsSavePdfVisible"/> の変更を通知する (Coordinator ホスト専用)</summary>
        internal void NotifyIsSavePdfVisibleChanged() => OnPropertyChanged(nameof(IsSavePdfVisible));

        /// <summary>クリップボードコピーコマンドの実行可能状態を更新する (Coordinator ホスト専用)</summary>
        internal void NotifyCopyToClipboardCanExecuteChanged() => RaiseCanExecuteChanged(CopyToClipboardCommand);

        /// <summary>キャンセル処理を準備する (Coordinator ホスト専用)</summary>
        internal void PrepareCoordinatorCancellation() => PrepareCancellation();

        /// <summary>キャンセルトークンを取得する (Coordinator ホスト専用)</summary>
        internal CancellationToken GetCoordinatorCancellationToken() =>
            _cancelTokenSource?.Token ?? throw new InvalidOperationException("キャンセル処理が準備されていません。");

        /// <summary>キャンセルトークンを破棄する (Coordinator ホスト専用)</summary>
        internal void DisposeCoordinatorCancellation()
        {
            _cancelTokenSource?.Dispose();
            _cancelTokenSource = null;
        }

        /// <summary>アクション可能なコマンドの実行可能状態を更新する (Coordinator ホスト専用)</summary>
        internal void RaiseCoordinatorActionCanExecuteChanged() => RaiseActionCanExecuteChanged();

        /// <summary>パスワードで保護された PDF のパスワードを入力させる (Coordinator ホスト専用)</summary>
        /// <param name="filePath">開こうとしているドキュメントのパス</param>
        /// <param name="previousPasswordWasIncorrect">直前に指定したパスワードが正しくなかったかどうか</param>
        /// <returns>入力されたパスワード。キャンセルされた場合は <c>null</c></returns>
        internal string? RequestPdfPassword(string filePath, bool previousPasswordWasIncorrect)
        {
            string fileName = Path.GetFileName(filePath);
            string message = previousPasswordWasIncorrect
                ? $"パスワードが正しくありません。もう一度入力してください。\n{fileName}"
                : $"{fileName} はパスワードで保護されています。パスワードを入力してください。";
            return _dialogService.ShowPasswordDialog(message, "パスワードの入力");
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>ユーザー設定リセット後に UI 状態を同期する</summary>
        private void OnUserSettingsReset(object? sender, EventArgs e)
        {
            Theme.ReloadFromStore();
            WordSettings.ReloadFromStore(reloadDocument: true);
            ExportSettings.ReloadFromStore();
            if (!string.IsNullOrEmpty(FilePath) && DocumentFileHelper.IsPdfFile(FilePath) && !IsBusy)
            {
                _previewCoordinator.RequestRefreshIfLoaded(_coordinatorHost);
            }

            SetStatus("設定を既定値に戻しました。", StatusKind.Success);
        }

        /// <summary>Word ファイルが読み込み済みの場合にプレビューを再生成する</summary>
        private void ReloadWordDocumentIfLoaded()
        {
            if (_isCommittingPendingInput
                || string.IsNullOrEmpty(FilePath)
                || !DocumentFileHelper.IsWordFile(FilePath)
                || IsBusy)
            {
                return;
            }

            LoadedFilePath = null;
            LoadPdfFromPath(forceReload: true);
        }

        /// <summary>ドキュメントが読み込み済みの場合に現在の画像出力設定でプレビューを再生成する</summary>
        private void RefreshPreviewIfLoaded()
        {
            if (_isCommittingPendingInput)
            {
                return;
            }

            _previewCoordinator.RequestRefreshIfLoaded(_coordinatorHost);
        }

        /// <summary>ファイルを選択する</summary>
        private void OnBrowse()
        {
            string? selectedPath = _dialogService.ShowOpenDocumentFileDialog();
            if (selectedPath != null)
            {
                FilePath = selectedPath;
                LoadPdfFromPath(forceReload: true);
            }
        }

        /// <summary>プレビュー画像をクリップボードにコピー可能かどうか</summary>
        private bool CanCopyToClipboard() => !IsBusy && Preview.PreviewImage != null;

        /// <summary>Word から変換した PDF を保存できるかどうか</summary>
        private bool CanSavePdf() =>
            !IsBusy
            && !string.IsNullOrEmpty(FilePath)
            && DocumentFileHelper.IsWordFile(FilePath)
            && Preview.PageCount > 0;

        /// <summary>アクション可能なコマンドの実行可能状態を更新する</summary>
        private void RaiseActionCanExecuteChanged()
        {
            RaiseCanExecuteChanged(SaveCommand);
            RaiseCanExecuteChanged(SavePdfCommand);
            Preview.RaiseActionCanExecuteChanged();
            RaiseCanExecuteChanged(CancelCommand);
            RaiseCanExecuteChanged(CopyToClipboardCommand);
            OnPropertyChanged(nameof(IsSavePdfVisible));
        }

        /// <summary>コマンドの実行可能状態を更新する</summary>
        private static void RaiseCanExecuteChanged(ICommand command)
        {
            if (command is IRelayCommand relayCommand)
            {
                relayCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>キャンセル処理を準備する</summary>
        private void PrepareCancellation()
        {
            _cancelTokenSource?.Cancel();
            _cancelTokenSource?.Dispose();
            _cancelTokenSource = new CancellationTokenSource();
        }

        /// <summary>キャンセル操作を実行する</summary>
        private void CancelOperation()
        {
            if (_cancelTokenSource?.IsCancellationRequested == false)
            {
                _cancelTokenSource.Cancel();
                SetStatus("処理をキャンセルしています...", StatusKind.Progress);
            }
        }

        /// <summary>非同期コマンドで捕捉した未処理例外をステータスに反映する</summary>
        private void OnAsyncCommandException(Exception ex)
        {
            IsBusy = false;
            IsProgressBarVisible = false;
            ApplicationLogger.Error("AsyncCommand", "Command execution failed", ex);
            SetStatus($"処理中にエラーが発生しました: {ExceptionFormattingHelper.GetUserMessage(ex)}", StatusKind.Error);
        }

        /// <summary>
        /// 表示中のプレビューと同じページ・設定の画像を、出力解像度でクリップボードにコピーする
        /// </summary>
        /// <remarks>プレビューが表示サイズに縮小されている場合は、出力解像度で描画し直してからコピーする</remarks>
        /// <returns>非同期操作のタスク</returns>
        private async Task OnCopyToClipboardAsync()
        {
            if (Preview.PreviewImage == null)
            {
                SetStatus("コピーするプレビュー画像がありません。", StatusKind.Warning);
                return;
            }

            BitmapSource? image = await _previewCoordinator.GetOutputResolutionPreviewImageAsync(_coordinatorHost);
            if (image == null)
            {
                return;
            }

            try
            {
                _clipboardService.CopyImage(image, ExportSettings.PreserveTransparency);
                SetStatus("プレビュー画像をクリップボードにコピーしました。", StatusKind.Success);
            }
            catch (Exception ex)
            {
                ApplicationLogger.Error("Clipboard", "Failed to copy preview image", ex);
                SetStatus($"クリップボードへのコピーに失敗しました: {ExceptionFormattingHelper.GetUserMessage(ex)}", StatusKind.Error);
            }
        }
    }
}
