using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PdfConverter.Commands;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.Themes;
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

        /* -------------------------------- テーマ関連 -------------------------------- */
        /// <summary>テーマ</summary>
        private ThemeMode _themeMode = ThemeManager.ParseThemeMode(Properties.Settings.Default.ThemeMode);

        /// <summary>テーマの選択肢</summary>
        private static readonly IReadOnlyList<ThemeModeOption> ThemeModeOptionsList =
            new List<ThemeModeOption>
            {
                new ThemeModeOption(ThemeMode.Light, "ライト"),
                new ThemeModeOption(ThemeMode.Dark, "ダーク"),
                new ThemeModeOption(ThemeMode.System, "システム"),
            };

        /* ------------------------------- 入力情報関連 ------------------------------- */
        /// <summary>読み込むドキュメントのパス</summary>
        private string _filePath;

        /// <summary>読み込み済みドキュメントのパス</summary>
        private string _loadedFilePath;

        /* ------------------------------ プレビュー関連 ------------------------------ */
        /// <summary>プレビュー画像</summary>
        private BitmapSource _previewImage;

        /// <summary>PDF のページ数</summary>
        private int _pageCount;

        /// <summary>表示ページ番号の入力値</summary>
        private string _pageNumber = "1";

        /* ------------------------------- 進捗表示関連 ------------------------------- */
        /// <summary>進捗値</summary>
        private double _progressValue;

        /* ---------------------------- ステータス表示関連 ---------------------------- */
        /// <summary>ステータスメッセージ</summary>
        private string _statusMessage = "ファイルを選択して開始してください。";

        /// <summary>ステータスバーの表示種類</summary>
        private StatusKind _statusKind = StatusKind.Info;

        /// <summary>表示ページ番号の入力エラーメッセージ</summary>
        private string _pageNumberValidationMessage;

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
            IWordToPdfConversionSettings wordToPdfSettings)
        {
            _dialogService = dialogService;
            _clipboardService = clipboardService;
            _previewCoordinator = previewCoordinator;
            _saveCoordinator = saveCoordinator;

            WordSettings = new WordConversionSettingsViewModel(wordToPdfSettings, ReloadWordDocumentIfLoaded);
            ExportSettings = new ImageExportSettingsViewModel(
                () => _previewCoordinator.RequestRefreshIfLoaded(this),
                RaiseActionCanExecuteChanged);

            BrowseCommand = new RelayCommand(OnBrowse, () => !IsBusy);
            SaveCommand = new AsyncRelayCommand(() => _saveCoordinator.SaveAsync(this), () => !string.IsNullOrEmpty(FilePath) && !IsBusy, OnAsyncCommandException);
            SavePdfCommand = new AsyncRelayCommand(() => _saveCoordinator.SavePdfAsync(this), CanSavePdf, OnAsyncCommandException);
            CancelCommand = new RelayCommand(CancelOperation, () => IsBusy);
            CopyToClipboardCommand = new RelayCommand(OnCopyToClipboard, CanCopyToClipboard);
            GoToPageCommand = new AsyncRelayCommand(() => _previewCoordinator.GoToPageAsync(this), CanGoToPage, OnAsyncCommandException);
            PreviousPageCommand = new AsyncRelayCommand(() => _previewCoordinator.GoToPreviousPageAsync(this), CanGoPrevious, OnAsyncCommandException);
            NextPageCommand = new AsyncRelayCommand(() => _previewCoordinator.GoToNextPageAsync(this), CanGoNext, OnAsyncCommandException);
            LoadPdfFromPathCommand = new RelayCommand(() => LoadPdfFromPath(), () => !IsBusy);
        }


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /* ------------------------------- 状態管理関連 ------------------------------- */
        /// <summary>Word → PDF 変換設定</summary>
        public WordConversionSettingsViewModel WordSettings { get; }

        /// <summary>画像出力設定</summary>
        public ImageExportSettingsViewModel ExportSettings { get; }

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

                RaiseCanExecuteChanged(BrowseCommand);
                RaiseCanExecuteChanged(SaveCommand);
                RaiseCanExecuteChanged(CancelCommand);
                RaiseCanExecuteChanged(LoadPdfFromPathCommand);
                RaiseNavigationCanExecuteChanged();
                RaiseActionCanExecuteChanged();
                OnPropertyChanged(nameof(IsProgressIndeterminate));
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

                OnPropertyChanged(nameof(IsProgressIndeterminate));
            }
        }

        /// <summary>Word から変換した PDF の保存ボタンを表示するかどうか</summary>
        public bool IsSavePdfVisible =>
            !string.IsNullOrEmpty(FilePath)
            && DocumentFileHelper.IsWordFile(FilePath)
            && PageCount > 0;

        /* -------------------------------- テーマ関連 -------------------------------- */
        /// <summary>テーマ</summary>
        public ThemeMode ThemeMode
        {
            get => _themeMode;
            set
            {
                if (!SetProperty(ref _themeMode, value))
                {
                    return;
                }

                ThemeManager.Apply(value);
            }
        }

        /// <summary>テーマの選択肢</summary>
        public IReadOnlyList<ThemeModeOption> ThemeModeOptions => ThemeModeOptionsList;

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

                OnPropertyChanged(nameof(PageIndicator));
                OnPropertyChanged(nameof(IsSavePdfVisible));
                RaiseCanExecuteChanged(SaveCommand);
            }
        }

        /* ------------------------------ プレビュー関連 ------------------------------ */
        /// <summary>プレビュー画像</summary>
        public BitmapSource PreviewImage
        {
            get => _previewImage;
            set
            {
                if (!SetProperty(ref _previewImage, value))
                {
                    return;
                }

                RaiseActionCanExecuteChanged();
            }
        }

        /// <summary>PDF のページ数</summary>
        public int PageCount
        {
            get => _pageCount;
            set
            {
                if (!SetProperty(ref _pageCount, value))
                {
                    return;
                }

                OnPropertyChanged(nameof(PageIndicator));
                OnPropertyChanged(nameof(IsSavePdfVisible));
                RaiseNavigationCanExecuteChanged();
                RaiseActionCanExecuteChanged();
            }
        }

        /// <summary>表示ページ番号の入力値</summary>
        public string PageNumber
        {
            get => _pageNumber;
            set
            {
                if (!SetProperty(ref _pageNumber, value))
                {
                    return;
                }

                PageNumberValidationMessage = null;
                OnPropertyChanged(nameof(PageIndicator));
                RaiseNavigationCanExecuteChanged();
                RaiseActionCanExecuteChanged();
            }
        }

        /* ------------------------------- 進捗表示関連 ------------------------------- */
        /// <summary>進捗値</summary>
        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        /// <summary>
        /// 進捗を不確定 (マーキー) 表示にするかどうか<br/>
        /// 保存以外の処理 (プレビュー生成・読み込み・ページ移動) 中は具体的な進捗値を持たないため不確定表示にする
        /// </summary>
        public bool IsProgressIndeterminate => IsBusy && !IsSaving;

        /* ---------------------------- ステータス表示関連 ---------------------------- */
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

        /// <summary>表示ページ番号の入力エラーメッセージ</summary>
        public string PageNumberValidationMessage
        {
            get => _pageNumberValidationMessage;
            set => SetProperty(ref _pageNumberValidationMessage, value);
        }

        /// <summary>現在表示中のページ番号の表示文字列</summary>
        public string PageIndicator
        {
            get
            {
                if (string.IsNullOrEmpty(FilePath))
                {
                    return "ファイルを選択してください";
                }

                return PageCount > 0
                    ? $"{CurrentPreviewPage}/{PageCount} ページ"
                    : "ページ数を読み込んでいます...";
            }
        }

        /* ----------------------------- オーバーレイ関連 ----------------------------- */
        /// <summary>ドラッグ&amp;ドロップオーバーレイを表示するかどうか</summary>
        public bool IsDropOverlayVisible
        {
            get => _isDropOverlayVisible;
            set => SetProperty(ref _isDropOverlayVisible, value);
        }

        /// <summary>現在表示中のページ番号</summary>
        private int CurrentPreviewPage => int.TryParse(PageNumber, out int value)
            ? (value < 1 ? 1 : (value > Math.Max(1, PageCount) ? Math.Max(1, PageCount) : value))
            : 1;


        /********************************************************************************/
        /*                                   コマンド                                   */
        /********************************************************************************/
        /// <summary>ファイルを選択するコマンド</summary>
        public ICommand BrowseCommand { get; }

        /// <summary>前のページに移動するコマンド</summary>
        public ICommand PreviousPageCommand { get; }

        /// <summary>次のページに移動するコマンド</summary>
        public ICommand NextPageCommand { get; }

        /// <summary>指定したページに移動するコマンド</summary>
        public ICommand GoToPageCommand { get; }

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
        public void SetStatus(string message, StatusKind kind = StatusKind.Info)
        {
            StatusMessage = message;
            StatusKind = kind;
        }


        /********************************************************************************/
        /*                          IMainViewModelHost 委譲                            */
        /********************************************************************************/
        string IPreviewCoordinatorHost.LoadedFilePath
        {
            get => _loadedFilePath;
            set => _loadedFilePath = value;
        }

        ResolutionMode IResolutionInputHost.ResolutionMode => ExportSettings.ResolutionMode;

        string IResolutionInputHost.ResolutionValue => ExportSettings.ResolutionValue;

        string IResolutionInputHost.ResolutionValidationMessage
        {
            get => ExportSettings.ResolutionValidationMessage;
            set => ExportSettings.ResolutionValidationMessage = value;
        }

        bool IPreviewCoordinatorHost.PreserveTransparency => ExportSettings.PreserveTransparency;

        int IPreviewCoordinatorHost.CurrentPreviewPage => CurrentPreviewPage;

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

        void ICoordinatorSession.RaiseNavigationCanExecuteChanged() => RaiseNavigationCanExecuteChanged();

        void ICoordinatorSession.RaiseActionCanExecuteChanged() => RaiseActionCanExecuteChanged();

        void ICoordinatorSession.ClearFieldValidationMessages()
        {
            ExportSettings.ClearValidationMessages();
            PageNumberValidationMessage = null;
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

        /// <summary>前のページに移動可能かどうか</summary>
        /// <returns>true: 移動可能 / false: 移動不可能</returns>
        private bool CanGoPrevious() => !IsBusy && PageCount > 0 && CurrentPreviewPage > 1;

        /// <summary>次のページに移動可能かどうか</summary>
        /// <returns>true: 移動可能 / false: 移動不可能</returns>
        private bool CanGoNext() => !IsBusy && PageCount > 0 && CurrentPreviewPage < PageCount;

        /// <summary>指定したページに移動可能かどうか</summary>
        /// <returns>true: 移動可能 / false: 移動不可能</returns>
        private bool CanGoToPage() => !IsBusy && PageCount > 0 && int.TryParse(PageNumber, out int pageNumber) && pageNumber >= 1 && pageNumber <= PageCount;

        /// <summary>プレビュー画像をクリップボードにコピー可能かどうか</summary>
        /// <returns>true: コピー可能 / false: コピー不可能</returns>
        private bool CanCopyToClipboard() => !IsBusy && PreviewImage != null;

        /// <summary>Word から変換した PDF を保存できるかどうか</summary>
        /// <returns>true: 保存可能 / false: 保存不可能</returns>
        private bool CanSavePdf() =>
            !IsBusy
            && !string.IsNullOrEmpty(FilePath)
            && DocumentFileHelper.IsWordFile(FilePath)
            && PageCount > 0;

        /// <summary>ナビゲーション可能なコマンドの実行可能状態を更新する</summary>
        private void RaiseNavigationCanExecuteChanged()
        {
            RaiseCanExecuteChanged(PreviousPageCommand);
            RaiseCanExecuteChanged(NextPageCommand);
            RaiseCanExecuteChanged(GoToPageCommand);
        }

        /// <summary>アクション可能なコマンドの実行可能状態を更新する</summary>
        private void RaiseActionCanExecuteChanged()
        {
            RaiseCanExecuteChanged(SaveCommand);
            RaiseCanExecuteChanged(SavePdfCommand);
            RaiseCanExecuteChanged(GoToPageCommand);
            RaiseCanExecuteChanged(CancelCommand);
            RaiseCanExecuteChanged(CopyToClipboardCommand);
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
            SetStatus($"処理中にエラーが発生しました: {ex.Message}", StatusKind.Error);
        }

        /// <summary>プレビュー画像をクリップボードにコピーする</summary>
        private void OnCopyToClipboard()
        {
            if (PreviewImage == null)
            {
                SetStatus("コピーするプレビュー画像がありません。", StatusKind.Warning);
                return;
            }

            try
            {
                _clipboardService.CopyImage(PreviewImage, ExportSettings.PreserveTransparency);
                SetStatus("プレビュー画像をクリップボードにコピーしました。", StatusKind.Success);
            }
            catch (Exception ex)
            {
                SetStatus($"クリップボードへのコピーに失敗しました: {ex.Message}", StatusKind.Error);
            }
        }
    }
}
