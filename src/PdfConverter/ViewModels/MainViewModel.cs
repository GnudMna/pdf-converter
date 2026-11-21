using System;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PdfConverter.Commands;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.ViewModels.Coordinators;

namespace PdfConverter.ViewModels
{
    /// <summary>
    /// メインウィンドウの ViewModel<br/>
    /// UI バインディングとコマンドを公開し、処理は Coordinator に委譲する
    /// </summary>
    public class MainViewModel : ViewModelBase, IMainViewModelHost, IMainWindowViewModel
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /* ----------------------------- 依存関係注入関連 ----------------------------- */
        /// <summary>ダイアログサービス</summary>
        private readonly IDialogService _dialogService;

        /// <summary>クリップボードサービス</summary>
        private readonly IClipboardService _clipboardService;

        /* --------------------------- コーディネーター関連 --------------------------- */
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
        private CancellationTokenSource _cancelTokenSource;

        /* ------------------------------- 入力情報関連 ------------------------------- */
        /// <summary>読み込むドキュメントのパス</summary>
        private string _filePath;

        /// <summary>読み込み済みドキュメントのパス</summary>
        private string _loadedFilePath;

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
            IImageExportSettings imageExportSettings)
        {
            _dialogService = dialogService;
            _clipboardService = clipboardService;
            _previewCoordinator = previewCoordinator;
            _saveCoordinator = saveCoordinator;

            Theme = new ThemeSettingsViewModel();
            Status = new StatusBarViewModel();
            Preview = new PreviewPanelViewModel(
                _previewCoordinator,
                () => this,
                () => IsBusy,
                OnAsyncCommandException);
            WordSettings = new WordConversionSettingsViewModel(wordToPdfSettings, ReloadWordDocumentIfLoaded);
            ExportSettings = new ImageExportSettingsViewModel(
                imageExportSettings,
                () => _previewCoordinator.RequestRefreshIfLoaded(this),
                RaiseActionCanExecuteChanged);

            BrowseCommand = new RelayCommand(OnBrowse, () => !IsBusy);
            SaveCommand = new AsyncRelayCommand(() => _saveCoordinator.SaveAsync(this), () => !string.IsNullOrEmpty(FilePath) && !IsBusy, OnAsyncCommandException);
            SavePdfCommand = new AsyncRelayCommand(() => _saveCoordinator.SavePdfAsync(this), CanSavePdf, OnAsyncCommandException);
            CancelCommand = new RelayCommand(CancelOperation, () => IsBusy);
            CopyToClipboardCommand = new RelayCommand(OnCopyToClipboard, CanCopyToClipboard);
            LoadPdfFromPathCommand = new RelayCommand(() => LoadPdfFromPath(), () => !IsBusy);
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
        public string FilePath
        {
            get => _filePath;
            set
            {
                if (!SetProperty(ref _filePath, value))
                {
                    return;
                }

                Preview.NotifyFilePathChanged();
                OnPropertyChanged(nameof(IsSavePdfVisible));
                RaiseCanExecuteChanged(SaveCommand);
            }
        }

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


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 指定したファイルパスの PDF を検証して読み込み、ページ数取得とプレビュー生成を開始する
        /// </summary>
        /// <param name="forceReload">強制的に再読み込みするかどうか</param>
        public void LoadPdfFromPath(bool forceReload = false) => _previewCoordinator.LoadFromPath(this, forceReload);

        /// <inheritdoc/>
        public void HandleDroppedDocument(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || IsBusy)
            {
                return;
            }

            FilePath = filePath;
            LoadPdfFromPath(forceReload: true);
        }

        /// <inheritdoc/>
        public void SetStatus(string message, StatusKind kind = StatusKind.Info) => Status.SetStatus(message, kind);


        /********************************************************************************/
        /*                           IMainViewModelHost 委譲                            */
        /********************************************************************************/
        string IPreviewCoordinatorHost.LoadedFilePath
        {
            get => _loadedFilePath;
            set => _loadedFilePath = value;
        }

        string IPreviewCoordinatorHost.PageNumber
        {
            get => Preview.PageNumber;
            set => Preview.PageNumber = value;
        }

        int IPreviewCoordinatorHost.PageCount
        {
            get => Preview.PageCount;
            set
            {
                Preview.PageCount = value;
                OnPropertyChanged(nameof(IsSavePdfVisible));
            }
        }

        BitmapSource IPreviewCoordinatorHost.PreviewImage
        {
            get => Preview.PreviewImage;
            set
            {
                Preview.PreviewImage = value;
                RaiseCanExecuteChanged(CopyToClipboardCommand);
            }
        }

        bool IPreviewCoordinatorHost.PreserveTransparency => ExportSettings.PreserveTransparency;

        int IPreviewCoordinatorHost.CurrentPreviewPage => Preview.CurrentPreviewPage;

        string IPreviewCoordinatorHost.PageNumberValidationMessage
        {
            get => Preview.PageNumberValidationMessage;
            set => Preview.PageNumberValidationMessage = value;
        }

        int ISaveCoordinatorHost.PageCount => Preview.PageCount;

        ResolutionMode IResolutionInputHost.ResolutionMode => ExportSettings.ResolutionMode;

        string IResolutionInputHost.ResolutionValue => ExportSettings.ResolutionValue;

        string IResolutionInputHost.ResolutionValidationMessage
        {
            get => ExportSettings.ResolutionValidationMessage;
            set => ExportSettings.ResolutionValidationMessage = value;
        }

        string ISaveCoordinatorHost.PageRange => ExportSettings.PageRange;

        bool ISaveCoordinatorHost.IsAllPagesSelected => ExportSettings.IsAllPagesSelected;

        OutputImageFormat ISaveCoordinatorHost.OutputImageFormat => ExportSettings.OutputImageFormat;

        bool ISaveCoordinatorHost.PreserveTransparency => ExportSettings.PreserveTransparency;

        string ISaveCoordinatorHost.PageRangeValidationMessage
        {
            get => ExportSettings.PageRangeValidationMessage;
            set => ExportSettings.PageRangeValidationMessage = value;
        }

        void ICoordinatorSession.PrepareCancellation() => PrepareCancellation();

        CancellationToken ICoordinatorSession.GetCancellationToken() => _cancelTokenSource.Token;

        void ICoordinatorSession.DisposeCancellation()
        {
            _cancelTokenSource?.Dispose();
            _cancelTokenSource = null;
        }

        void ICoordinatorSession.RaiseNavigationCanExecuteChanged() => Preview.RaiseNavigationCanExecuteChanged();

        void ICoordinatorSession.RaiseActionCanExecuteChanged() => RaiseActionCanExecuteChanged();

        void ICoordinatorSession.ClearFieldValidationMessages()
        {
            ExportSettings.ClearValidationMessages();
            Preview.PageNumberValidationMessage = null;
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>Word ファイルが読み込み済みの場合にプレビューを再生成する</summary>
        private void ReloadWordDocumentIfLoaded()
        {
            if (string.IsNullOrEmpty(FilePath) || !DocumentFileHelper.IsWordFile(FilePath) || IsBusy)
            {
                return;
            }

            _loadedFilePath = null;
            LoadPdfFromPath(forceReload: true);
        }

        /// <summary>ファイルを選択する</summary>
        private void OnBrowse()
        {
            string selectedPath = _dialogService.ShowOpenDocumentFileDialog();
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
            SetStatus($"処理中にエラーが発生しました: {ex.Message}", StatusKind.Error);
        }

        /// <summary>プレビュー画像をクリップボードにコピーする</summary>
        private void OnCopyToClipboard()
        {
            if (Preview.PreviewImage == null)
            {
                SetStatus("コピーするプレビュー画像がありません。", StatusKind.Warning);
                return;
            }

            try
            {
                _clipboardService.CopyImage(Preview.PreviewImage, ExportSettings.PreserveTransparency);
                SetStatus("プレビュー画像をクリップボードにコピーしました。", StatusKind.Success);
            }
            catch (Exception ex)
            {
                SetStatus($"クリップボードへのコピーに失敗しました: {ex.Message}", StatusKind.Error);
            }
        }
    }
}
