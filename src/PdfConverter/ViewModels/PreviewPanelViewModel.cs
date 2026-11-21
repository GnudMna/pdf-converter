using System;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PdfConverter.Commands;
using PdfConverter.Services;
using PdfConverter.ViewModels.Coordinators;

namespace PdfConverter.ViewModels
{
    /// <summary>
    /// PDF プレビュー表示とページナビゲーションの UI 状態を管理する ViewModel
    /// </summary>
    public sealed class PreviewPanelViewModel : ViewModelBase
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>PDF プレビュー コーディネーター</summary>
        private readonly IPdfPreviewCoordinator _previewCoordinator;

        /// <summary>プレビュー操作のホスト ViewModel を取得する関数</summary>
        private readonly Func<IPreviewCoordinatorHost> _getHost;

        /// <summary>処理中かどうかを取得する関数</summary>
        private readonly Func<bool> _getIsBusy;

        /// <summary>非同期コマンドの未処理例外を受け取るコールバック</summary>
        private readonly Action<Exception> _onAsyncCommandException;

        /// <summary>プレビュー画像</summary>
        private BitmapSource? _previewImage;

        /// <summary>PDF のページ数</summary>
        private int _pageCount;

        /// <summary>表示ページ番号の入力値</summary>
        private string _pageNumber = "1";

        /// <summary>プレビューの描画に成功したページ番号 (未描画の場合は 0)</summary>
        private int _displayedPageNumber;

        /// <summary>表示ページ番号の入力エラーメッセージ</summary>
        private string? _pageNumberValidationMessage;

        /// <summary>処理中かどうか</summary>
        private bool _isBusy;

        /// <summary>プレビュー画像を表示する領域の幅 (デバイス px)</summary>
        private int _previewViewportPixelWidth;

        /// <summary>プレビュー画像を表示する領域の高さ (デバイス px)</summary>
        private int _previewViewportPixelHeight;

        /// <summary>ページ番号 1 桁あたりの表示幅 (px)</summary>
        private const double PageNavigationCharWidth = 9;

        /// <summary>ページ番号入力欄の左右余白 (px)</summary>
        private const double PageNavigationInputPadding = 18;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// プレビューパネル ViewModel を初期化する
        /// </summary>
        /// <param name="previewCoordinator">PDF プレビュー コーディネーター</param>
        /// <param name="getHost">プレビュー操作のホスト ViewModel を取得する関数</param>
        /// <param name="getIsBusy">処理中かどうかを取得する関数</param>
        /// <param name="onAsyncCommandException">非同期コマンドの未処理例外を受け取るコールバック</param>
        public PreviewPanelViewModel(
            IPdfPreviewCoordinator previewCoordinator,
            Func<IPreviewCoordinatorHost> getHost,
            Func<bool> getIsBusy,
            Action<Exception> onAsyncCommandException)
        {
            _previewCoordinator = previewCoordinator;
            _getHost = getHost;
            _getIsBusy = getIsBusy;
            _onAsyncCommandException = onAsyncCommandException;

            GoToPageCommand = new AsyncRelayCommand(
                () => _previewCoordinator.GoToPageAsync(_getHost()),
                CanGoToPage,
                _onAsyncCommandException);
            PreviousPageCommand = new AsyncRelayCommand(
                () => _previewCoordinator.GoToPreviousPageAsync(_getHost()),
                CanGoPrevious,
                _onAsyncCommandException);
            NextPageCommand = new AsyncRelayCommand(
                () => _previewCoordinator.GoToNextPageAsync(_getHost()),
                CanGoNext,
                _onAsyncCommandException);
        }


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>プレビュー画像</summary>
        public BitmapSource? PreviewImage
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
                OnPropertyChanged(nameof(IsPageNavigationVisible));
                OnPropertyChanged(nameof(PageCountDisplayMinWidth));
                OnPropertyChanged(nameof(PageNumberInputMinWidth));
                RaiseNavigationCanExecuteChanged();
                RaiseActionCanExecuteChanged();
            }
        }

        /// <summary>ページ移動 UI を表示するかどうか</summary>
        public bool IsPageNavigationVisible => PageCount > 0;

        /// <summary>総ページ数表示の最小幅</summary>
        public double PageCountDisplayMinWidth => GetPageDigitCount(PageCount) * PageNavigationCharWidth;

        /// <summary>ページ番号入力欄の最小幅</summary>
        public double PageNumberInputMinWidth
        {
            get
            {
                int pageNumberDigits = string.IsNullOrWhiteSpace(PageNumber)
                    ? 1
                    : PageNumber.Trim().Length;
                int digitCount = Math.Max(GetPageDigitCount(PageCount), pageNumberDigits);
                return digitCount * PageNavigationCharWidth + PageNavigationInputPadding;
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
                OnPropertyChanged(nameof(PageNumberInputMinWidth));
                RaiseNavigationCanExecuteChanged();
                RaiseActionCanExecuteChanged();
            }
        }

        /// <summary>表示ページ番号の入力エラーメッセージ</summary>
        public string? PageNumberValidationMessage
        {
            get => _pageNumberValidationMessage;
            set => SetProperty(ref _pageNumberValidationMessage, value);
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

                RaiseNavigationCanExecuteChanged();
            }
        }

        /// <summary>現在表示中のページ番号の表示文字列</summary>
        public string PageIndicator
        {
            get
            {
                string? filePath = _getHost().FilePath;
                if (string.IsNullOrEmpty(filePath))
                {
                    return "ファイルを選択してください";
                }

                return PageCount > 0
                    ? $"{CurrentPreviewPage}/{PageCount} ページ"
                    : "ページ数を読み込んでいます...";
            }
        }

        /// <summary>プレビューの描画に成功したページ番号 (1 始まり、未描画の場合は 0)</summary>
        public int DisplayedPageNumber
        {
            get => _displayedPageNumber;
            set
            {
                if (!SetProperty(ref _displayedPageNumber, value))
                {
                    return;
                }

                OnPropertyChanged(nameof(PageIndicator));
                OnPropertyChanged(nameof(CurrentPreviewPage));
                RaiseNavigationCanExecuteChanged();
            }
        }

        /// <summary>現在表示中のページ番号 (未描画の間は入力値から求める)</summary>
        public int CurrentPreviewPage
        {
            get
            {
                if (DisplayedPageNumber > 0)
                {
                    return DisplayedPageNumber;
                }

                return int.TryParse(PageNumber, out int value)
                    ? (value < 1 ? 1 : (value > Math.Max(1, PageCount) ? Math.Max(1, PageCount) : value))
                    : 1;
            }
        }

        /// <summary>プレビュー画像を表示する領域の幅 (デバイス px、未確定の場合は 0)</summary>
        public int PreviewViewportPixelWidth => _previewViewportPixelWidth;

        /// <summary>プレビュー画像を表示する領域の高さ (デバイス px、未確定の場合は 0)</summary>
        public int PreviewViewportPixelHeight => _previewViewportPixelHeight;


        /********************************************************************************/
        /*                                   コマンド                                   */
        /********************************************************************************/
        /// <summary>前のページに移動するコマンド</summary>
        public ICommand PreviousPageCommand { get; }

        /// <summary>次のページに移動するコマンド</summary>
        public ICommand NextPageCommand { get; }

        /// <summary>指定したページに移動するコマンド</summary>
        public ICommand GoToPageCommand { get; }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// プレビュー画像を表示する領域のサイズを更新し、プレビューの描画サイズに反映させる
        /// </summary>
        /// <param name="pixelWidth">表示領域の幅 (デバイス px)</param>
        /// <param name="pixelHeight">表示領域の高さ (デバイス px)</param>
        public void UpdatePreviewViewportPixelSize(int pixelWidth, int pixelHeight)
        {
            pixelWidth = Math.Max(0, pixelWidth);
            pixelHeight = Math.Max(0, pixelHeight);
            if (pixelWidth == _previewViewportPixelWidth && pixelHeight == _previewViewportPixelHeight)
            {
                return;
            }

            _previewViewportPixelWidth = pixelWidth;
            _previewViewportPixelHeight = pixelHeight;
            _previewCoordinator.NotifyPreviewViewportChanged(_getHost());
        }

        /// <summary>入力ファイルパスが変わったときに PageIndicator を更新する</summary>
        public void NotifyFilePathChanged()
        {
            OnPropertyChanged(nameof(PageIndicator));
        }

        /// <summary>ナビゲーション可能なコマンドの実行可能状態を更新する</summary>
        public void RaiseNavigationCanExecuteChanged()
        {
            RaiseCanExecuteChanged(PreviousPageCommand);
            RaiseCanExecuteChanged(NextPageCommand);
            RaiseCanExecuteChanged(GoToPageCommand);
        }

        /// <summary>プレビュー画像やページ数の変化に応じて、指定ページへ移動するコマンドの実行可能状態を更新する</summary>
        public void RaiseActionCanExecuteChanged()
        {
            RaiseCanExecuteChanged(GoToPageCommand);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>前のページに移動可能かどうか</summary>
        private bool CanGoPrevious() => !_getIsBusy() && PageCount > 0 && CurrentPreviewPage > 1;

        /// <summary>次のページに移動可能かどうか</summary>
        private bool CanGoNext() => !_getIsBusy() && PageCount > 0 && CurrentPreviewPage < PageCount;

        /// <summary>指定したページに移動可能かどうか</summary>
        private bool CanGoToPage() =>
            !_getIsBusy()
            && PageCount > 0
            && int.TryParse(PageNumber, out int pageNumber)
            && pageNumber >= 1
            && pageNumber <= PageCount;

        /// <summary>ページ番号の桁数を返す</summary>
        private static int GetPageDigitCount(int pageValue) =>
            pageValue > 0 ? (int)Math.Floor(Math.Log10(pageValue)) + 1 : 1;

        /// <summary>コマンドの実行可能状態を更新する</summary>
        private static void RaiseCanExecuteChanged(ICommand command)
        {
            if (command is IRelayCommand relayCommand)
            {
                relayCommand.RaiseCanExecuteChanged();
            }
        }
    }
}
