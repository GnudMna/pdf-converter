using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
    public class MainViewModel : ViewModelBase, IMainViewModelHost
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private readonly IDialogService _dialogService;
        private readonly IClipboardService _clipboardService;
        private readonly IPdfPreviewCoordinator _previewCoordinator;
        private readonly IPdfSaveCoordinator _saveCoordinator;
        private CancellationTokenSource _cancelTokenSource;

        private string _filePath;
        private string _loadedFilePath;
        private string _pageNumber = "1";
        private string _pageRange = "1";
        private bool _isAllPagesSelected;
        private BitmapSource _previewImage;
        private ResolutionMode _resolutionMode = ResolutionMode.Width;
        private string _resolutionValue = ResolutionValueParser.GetDefaultValue(ResolutionMode.Width);
        private bool _isBusy;
        private bool _isSaving;
        private double _progressValue;
        private int _pageCount;
        private string _statusMessage = "ファイルを選択して開始してください。";
        private ThemeMode _themeMode = ThemeManager.ParseThemeMode(Properties.Settings.Default.ThemeMode);
        private OutputImageFormat _outputImageFormat = OutputImageFormat.Png;
        private bool _preserveTransparency = false;

        private static readonly IReadOnlyList<ResolutionModeOption> ResolutionModeOptionsList =
            new List<ResolutionModeOption>
            {
                new ResolutionModeOption(ResolutionMode.Width, "幅 (px)"),
                new ResolutionModeOption(ResolutionMode.Height, "高さ (px)"),
                new ResolutionModeOption(ResolutionMode.Dpi, "DPI"),
            };

        private static readonly IReadOnlyList<ThemeModeOption> ThemeModeOptionsList =
            new List<ThemeModeOption>
            {
                new ThemeModeOption(ThemeMode.Light, "ライト"),
                new ThemeModeOption(ThemeMode.Dark, "ダーク"),
                new ThemeModeOption(ThemeMode.System, "システム"),
            };

        private static readonly IReadOnlyList<OutputImageFormatOption> OutputImageFormatOptionsList =
            new List<OutputImageFormatOption>
            {
                new OutputImageFormatOption(OutputImageFormat.Png, "PNG"),
                new OutputImageFormatOption(OutputImageFormat.Jpeg, "JPEG"),
                new OutputImageFormatOption(OutputImageFormat.Bmp, "BMP"),
            };


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>読み込んだPDFのパス</summary>
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
                RaiseCanExecuteChanged(SaveCommand);
            }
        }

        /// <summary>現在表示中のページ番号</summary>
        public string PageNumber
        {
            get => _pageNumber;
            set
            {
                if (!SetProperty(ref _pageNumber, value))
                {
                    return;
                }

                OnPropertyChanged(nameof(PageIndicator));
                RaiseNavigationCanExecuteChanged();
                RaiseActionCanExecuteChanged();
            }
        }

        /// <summary>全ページを保存するかどうか</summary>
        public bool IsAllPagesSelected
        {
            get => _isAllPagesSelected;
            set => SetProperty(ref _isAllPagesSelected, value);
        }

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

        /// <summary>解像度の指定方法</summary>
        public ResolutionMode ResolutionMode
        {
            get => _resolutionMode;
            set
            {
                if (!SetProperty(ref _resolutionMode, value))
                {
                    return;
                }

                _resolutionValue = ResolutionValueParser.GetDefaultValue(value);
                OnPropertyChanged(nameof(ResolutionValue));
                _previewCoordinator.RequestRefreshIfLoaded(this);
            }
        }

        /// <summary>解像度の値</summary>
        public string ResolutionValue
        {
            get => _resolutionValue;
            set
            {
                if (!SetProperty(ref _resolutionValue, value))
                {
                    return;
                }

                _previewCoordinator.RequestRefreshIfLoaded(this);
            }
        }

        /// <summary>保存するページの範囲</summary>
        public string PageRange
        {
            get => _pageRange;
            set
            {
                if (!SetProperty(ref _pageRange, value))
                {
                    return;
                }

                RaiseActionCanExecuteChanged();
            }
        }

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
                RaiseNavigationCanExecuteChanged();
                RaiseActionCanExecuteChanged();
                OnPropertyChanged(nameof(IsProgressIndeterminate));
            }
        }

        /// <summary>ステータスメッセージ</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
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

        /// <summary>
        /// 進捗を不確定（マーキー）表示にするかどうか<br/>
        /// 保存以外の処理（プレビュー生成・読み込み・ページ移動）中は具体的な進捗値を持たないため不確定表示にする
        /// </summary>
        public bool IsProgressIndeterminate => IsBusy && !IsSaving;

        /// <summary>進捗値</summary>
        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        /// <summary>PDFのページ数</summary>
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
                RaiseNavigationCanExecuteChanged();
                RaiseActionCanExecuteChanged();
            }
        }

        /// <summary>現在表示中のページ番号</summary>
        public string PageIndicator
        {
            get
            {
                if (string.IsNullOrEmpty(FilePath))
                {
                    return "PDFを選択してください";
                }

                return PageCount > 0
                    ? $"{CurrentPreviewPage}/{PageCount} ページ"
                    : "ページ数を読み込んでいます...";
            }
        }

        /// <summary>出力画像形式</summary>
        public OutputImageFormat OutputImageFormat
        {
            get => _outputImageFormat;
            set
            {
                if (!SetProperty(ref _outputImageFormat, value))
                {
                    return;
                }

                if (!ImageBitmapHelper.SupportsTransparency(value))
                {
                    PreserveTransparency = false;
                }

                OnPropertyChanged(nameof(IsTransparencySelectable));
            }
        }

        /// <summary>透明度を保持するかどうか</summary>
        public bool PreserveTransparency
        {
            get => _preserveTransparency;
            set
            {
                if (!SetProperty(ref _preserveTransparency, value))
                {
                    return;
                }

                _previewCoordinator.RequestRefreshIfLoaded(this);
            }
        }

        /// <summary>透明度を選択可能かどうか</summary>
        public bool IsTransparencySelectable => ImageBitmapHelper.SupportsTransparency(OutputImageFormat);

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


        /********************************************************************************/
        /*                                   コマンド                                   */
        /********************************************************************************/
        /// <summary>前のページに移動するコマンド</summary>
        public ICommand PreviousPageCommand { get; }

        /// <summary>次のページに移動するコマンド</summary>
        public ICommand NextPageCommand { get; }

        /// <summary>指定したページに移動するコマンド</summary>
        public ICommand GoToPageCommand { get; }

        /// <summary>解像度の選択肢</summary>
        public IReadOnlyList<ResolutionModeOption> ResolutionModeOptions => ResolutionModeOptionsList;

        /// <summary>テーマの選択肢</summary>
        public IReadOnlyList<ThemeModeOption> ThemeModeOptions => ThemeModeOptionsList;

        /// <summary>出力画像形式の選択肢</summary>
        public IReadOnlyList<OutputImageFormatOption> OutputImageFormatOptions => OutputImageFormatOptionsList;

        /// <summary>ファイルを選択するコマンド</summary>
        public ICommand BrowseCommand { get; }

        /// <summary>保存するコマンド</summary>
        public ICommand SaveCommand { get; }

        /// <summary>キャンセルするコマンド</summary>
        public ICommand CancelCommand { get; }

        /// <summary>プレビュー画像をクリップボードにコピーするコマンド</summary>
        public ICommand CopyToClipboardCommand { get; }

        /// <summary>読み込んだPDFのパス</summary>
        string IMainViewModelHost.LoadedFilePath
        {
            get => _loadedFilePath;
            set => _loadedFilePath = value;
        }

        /// <summary>現在表示中のページ番号</summary>
        int IMainViewModelHost.CurrentPreviewPage => CurrentPreviewPage;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        public MainViewModel(
            IDialogService dialogService,
            IClipboardService clipboardService,
            IPdfPreviewCoordinator previewCoordinator,
            IPdfSaveCoordinator saveCoordinator)
        {
            _dialogService = dialogService;
            _clipboardService = clipboardService;
            _previewCoordinator = previewCoordinator;
            _saveCoordinator = saveCoordinator;

            // コマンド初期化
            BrowseCommand = new RelayCommand(OnBrowse, () => !IsBusy);
            SaveCommand = new AsyncRelayCommand(() => _saveCoordinator.SaveAsync(this), () => !string.IsNullOrEmpty(FilePath) && !IsBusy, OnAsyncCommandException);
            CancelCommand = new RelayCommand(CancelOperation, () => IsBusy);
            CopyToClipboardCommand = new RelayCommand(OnCopyToClipboard, CanCopyToClipboard);
            GoToPageCommand = new AsyncRelayCommand(() => _previewCoordinator.GoToPageAsync(this), CanGoToPage, OnAsyncCommandException);
            PreviousPageCommand = new AsyncRelayCommand(() => _previewCoordinator.GoToPreviousPageAsync(this), CanGoPrevious, OnAsyncCommandException);
            NextPageCommand = new AsyncRelayCommand(() => _previewCoordinator.GoToNextPageAsync(this), CanGoNext, OnAsyncCommandException);
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 指定したファイルパスのPDFを検証して読み込み、ページ数取得とプレビュー生成を開始する
        /// </summary>
        /// <param name="forceReload">強制的に再読み込みするかどうか</param>
        /// <see cref="FilePath"/> の PDF を検証して読み込み、ページ数取得とプレビュー生成を開始する
        /// </summary>
        public void LoadPdfFromPath(bool forceReload = false) => _previewCoordinator.LoadFromPath(this, forceReload);


        /********************************************************************************/
        /*                                 実装メソッド                                 */
        /********************************************************************************/
        /// <summary>キャンセル処理を準備する</summary>
        void IMainViewModelHost.PrepareCancellation() => PrepareCancellation();

        /// <summary>キャンセルトークンを取得する</summary>
        CancellationToken IMainViewModelHost.GetCancellationToken() => _cancelTokenSource.Token;

        /// <summary>キャンセルトークンを破棄する</summary>
        void IMainViewModelHost.DisposeCancellation()
        {
            _cancelTokenSource?.Dispose();
            _cancelTokenSource = null;
        }

        /// <summary>ナビゲーション可能なコマンドの実行可能状態を更新する</summary>
        void IMainViewModelHost.RaiseNavigationCanExecuteChanged() => RaiseNavigationCanExecuteChanged();

        /// <summary>アクション可能なコマンドの実行可能状態を更新する</summary>
        void IMainViewModelHost.RaiseActionCanExecuteChanged() => RaiseActionCanExecuteChanged();


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>現在表示中のページ番号</summary>
        private int CurrentPreviewPage => int.TryParse(PageNumber, out int value)
            ? (value < 1 ? 1 : (value > Math.Max(1, PageCount) ? Math.Max(1, PageCount) : value))
            : 1;

        /// <summary>ファイルを選択する</summary>
        private void OnBrowse()
        {
            string selectedPath = _dialogService.ShowOpenPdfFileDialog();
            if (selectedPath != null)
            {
                FilePath = selectedPath;
                LoadPdfFromPath(forceReload: true);
            }
        }

        /// <summary>前のページに移動可能かどうか</summary>
        /// <returns>true: 移動可能, false: 移動不可能</returns>
        private bool CanGoPrevious() => !IsBusy && PageCount > 0 && CurrentPreviewPage > 1;

        /// <summary>次のページに移動可能かどうか</summary>
        /// <returns>true: 移動可能, false: 移動不可能</returns>
        private bool CanGoNext() => !IsBusy && PageCount > 0 && CurrentPreviewPage < PageCount;

        /// <summary>指定したページに移動可能かどうか</summary>
        /// <returns>true: 移動可能, false: 移動不可能</returns>
        private bool CanGoToPage() => !IsBusy && PageCount > 0 && int.TryParse(PageNumber, out int pageNumber) && pageNumber >= 1 && pageNumber <= PageCount;

        /// <summary>プレビュー画像をクリップボードにコピー可能かどうか</summary>
        /// <returns>true: コピー可能, false: コピー不可能</returns>
        private bool CanCopyToClipboard() => !IsBusy && PreviewImage != null;

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
                StatusMessage = "処理をキャンセルしています...";
            }
        }

        /// <summary>非同期コマンドで捕捉した未処理例外をステータスに反映する</summary>
        private void OnAsyncCommandException(Exception ex)
        {
            IsBusy = false;
            StatusMessage = $"処理中にエラーが発生しました: {ex.Message}";
        }

        /// <summary>プレビュー画像をクリップボードにコピーする</summary>
        private void OnCopyToClipboard()
        {
            if (PreviewImage == null)
            {
                StatusMessage = "コピーするプレビュー画像がありません。";
                return;
            }

            try
            {
                _clipboardService.CopyImage(PreviewImage, PreserveTransparency);
                StatusMessage = "プレビュー画像をクリップボードにコピーしました。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"クリップボードへのコピーに失敗しました: {ex.Message}";
            }
        }
    }
}
