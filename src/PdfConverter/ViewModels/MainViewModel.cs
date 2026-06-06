using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PdfConverter.Commands;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.Themes;

namespace PdfConverter.ViewModels
{
    /// <summary>
    /// メインウィンドウの ViewModel<br/>
    /// MVVM パターンに基づき、<br/>
    /// PDF読み込み・プレビュー・保存に関わる全UIロジックを担う
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>PDF変換サービス</summary>
        private readonly IPdfConversionService _pdfService;
        /// <summary>ダイアログ表示サービス</summary>
        private readonly IDialogService _dialogService;
        /// <summary>クリップボード操作サービス</summary>
        private readonly IClipboardService _clipboardService;
        /// <summary>読み込むPDFファイルの絶対パス</summary>
        private string _filePath;
        /// <summary>最後に正常読み込みしたPDFファイルの絶対パス</summary>
        private string _loadedFilePath;
        /// <summary>プレビュー表示するページ番号(1 始まりの文字列)</summary>
        private string _pageNumber = "1";
        /// <summary>保存対象ページの指定文字列(例: "1,3-5")</summary>
        private string _pageRange = "1";
        /// <summary>全ページを保存対象とするかどうかを表すフラグ</summary>
        private bool _isAllPagesSelected;
        /// <summary>プレビューエリアに表示する変換済みのビットマップ</summary>
        private BitmapSource _previewImage;
        /// <summary>画像変換時の解像度指定方法</summary>
        private ResolutionMode _resolutionMode = ResolutionMode.Width;
        /// <summary><see cref="ResolutionMode"/> に対応する数値(幅・高さ・DPI)の文字列表現</summary>
        private string _resolutionValue = ResolutionValueParser.GetDefaultValue(ResolutionMode.Width);
        /// <summary>非同期処理(プレビュー生成・保存)が実行中かどうかを示すフラグ</summary>
        private bool _isBusy;
        /// <summary>保存処理が実行中かどうかを示すフラグ</summary>
        private bool _isSaving;
        /// <summary>保存処理の完了率(0.0 ～ 100.0)</summary>
        private double _progressValue;
        /// <summary>読み込んだPDFの総ページ数</summary>
        private int _pageCount;
        /// <summary>現在実行中の非同期処理をキャンセルするためのトークンソース</summary>
        private CancellationTokenSource _cancelTokenSource;
        /// <summary>UI・フッターに表示するステータスメッセージ</summary>
        private string _statusMessage = "ファイルを選択して開始してください。";
        /// <summary>UI・ComboBoxに表示する解像度モードの選択肢</summary>
        private static readonly IReadOnlyList<ResolutionModeOption> _resolutionModeOptions =
            new List<ResolutionModeOption>
            {
                new ResolutionModeOption(ResolutionMode.Width, "幅 (px)"),
                new ResolutionModeOption(ResolutionMode.Height, "高さ (px)"),
                new ResolutionModeOption(ResolutionMode.Dpi, "DPI"),
            };
        /// <summary>UI・ComboBoxに表示するテーマの選択肢</summary>
        private static readonly IReadOnlyList<ThemeModeOption> _themeModeOptions =
            new List<ThemeModeOption>
            {
                new ThemeModeOption(ThemeMode.Light, "ライト"),
                new ThemeModeOption(ThemeMode.Dark, "ダーク"),
                new ThemeModeOption(ThemeMode.System, "システム設定に従う"),
            };
        /// <summary>選択中の外観テーマ</summary>
        private ThemeMode _themeMode = ThemeManager.ParseThemeMode(Properties.Settings.Default.ThemeMode);


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>
        /// 読み込む PDF ファイルの絶対パス<br/>
        /// 読み込みは <see cref="LoadPdfFromPath"/> または参照・ドロップ操作で開始する
        /// </summary>
        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageIndicator));
                RaiseCanExecuteChanged(SaveCommand);
            }
        }

        /// <summary>
        /// プレビュー表示するページ番号(1 始まりの文字列)
        /// </summary>
        public string PageNumber
        {
            get => _pageNumber;
            set
            {
                _pageNumber = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageIndicator));
                RaiseNavigationCanExecuteChanged();
                RaiseActionCanExecuteChanged();
            }
        }

        /// <summary>
        /// 全ページを保存対象とするかどうかを表すフラグ<br/>
        /// <c>true</c>の場合<see cref="PageRange"/>の内容は無視される
        /// </summary>
        public bool IsAllPagesSelected
        {
            get => _isAllPagesSelected;
            set
            {
                _isAllPagesSelected = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// プレビューエリアに表示する変換済みのビットマップ<br/>
        /// <c>null</c>の場合はプレースホルダーが表示される
        /// </summary>
        public BitmapSource PreviewImage
        {
            get => _previewImage;
            set
            {
                _previewImage = value;
                OnPropertyChanged();
                RaiseActionCanExecuteChanged();
            }
        }

        /// <summary>
        /// 画像変換時の解像度指定方法<br/>
        /// <see cref="ResolutionModeOptions"/>のいずれかを選択する
        /// </summary>
        public ResolutionMode ResolutionMode
        {
            get => _resolutionMode;
            set
            {
                if (_resolutionMode == value)
                {
                    return;
                }

                _resolutionMode = value;
                _resolutionValue = ResolutionValueParser.GetDefaultValue(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(ResolutionValue));
                _ = RefreshPreviewIfLoadedAsync();
            }
        }

        /// <summary>
        /// <see cref="ResolutionMode"/>に対応する数値(幅・高さ・DPI)の文字列表現
        /// </summary>
        public string ResolutionValue
        {
            get => _resolutionValue;
            set
            {
                _resolutionValue = value;
                OnPropertyChanged();
                _ = RefreshPreviewIfLoadedAsync();
            }
        }

        /// <summary>
        /// 保存対象ページの指定文字列(例: "1,3-5")<br/>
        /// <see cref="IsAllPagesSelected"/>が<c>false</c>のときのみ参照される
        /// </summary>
        public string PageRange
        {
            get => _pageRange;
            set
            {
                _pageRange = value;
                OnPropertyChanged();
                RaiseActionCanExecuteChanged();
            }
        }

        /// <summary>
        /// 非同期処理(プレビュー生成・保存)が実行中かどうかを示すフラグ<br/>
        /// <c>true</c>の間は大半のコマンドが無効化される
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                RaiseCanExecuteChanged(SaveCommand);
                RaiseCanExecuteChanged(CancelCommand);
                RaiseNavigationCanExecuteChanged();
                RaiseActionCanExecuteChanged();
            }
        }

        /// <summary>
        /// UI・フッターに表示するステータスメッセージ<br/>
        /// 処理の進行状況やエラー内容を示す
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 保存処理が実行中かどうかを示すフラグ<br/>
        /// プログレスバーの表示切替に使用する
        /// </summary>
        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                _isSaving = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 保存処理の完了率(0.0 ～ 100.0)<br/>
        /// プログレスバーにバインドされる
        /// </summary>
        public double ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 読み込んだPDFの総ページ数<br/>
        /// ページナビゲーションの上限判定に使用する
        /// </summary>
        public int PageCount
        {
            get => _pageCount;
            set
            {
                _pageCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageIndicator));
                RaiseNavigationCanExecuteChanged();
                RaiseActionCanExecuteChanged();
            }
        }

        /// <summary>
        /// プレビューエリアに表示するページインジケーター文字列(例: "3/10 ページ")<br/>
        /// <see cref="FilePath"/>・<see cref="PageCount"/>・<see cref="PageNumber"/>の変化に連動する
        /// </summary>
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


        /********************************************************************************/
        /*                                   コマンド                                   */
        /********************************************************************************/
        /// <summary>前のページへ移動するコマンド</summary>
        public ICommand PreviousPageCommand { get; }

        /// <summary>次のページへ移動するコマンド</summary>
        public ICommand NextPageCommand { get; }

        /// <summary><see cref="PageNumber"/>で指定したページへ移動するコマンド</summary>
        public ICommand GoToPageCommand { get; }

        /// <summary>解像度モードの選択肢リスト</summary>
        public IReadOnlyList<ResolutionModeOption> ResolutionModeOptions => _resolutionModeOptions;

        /// <summary>テーマの選択肢リスト</summary>
        public IReadOnlyList<ThemeModeOption> ThemeModeOptions => _themeModeOptions;

        /// <summary>選択中の外観テーマ</summary>
        public ThemeMode ThemeMode
        {
            get => _themeMode;
            set
            {
                if (_themeMode == value)
                {
                    return;
                }

                _themeMode = value;
                OnPropertyChanged();
                ThemeManager.Apply(value);
            }
        }

        /// <summary>ファイル選択ダイアログを開くコマンド</summary>
        public ICommand BrowseCommand { get; }

        /// <summary>指定ページを PNG として保存するコマンド</summary>
        public ICommand SaveCommand { get; }

        /// <summary>実行中の非同期処理をキャンセルするコマンド</summary>
        public ICommand CancelCommand { get; }

        /// <summary>プレビュー画像をクリップボードにコピーするコマンド</summary>
        public ICommand CopyToClipboardCommand { get; }


        /********************************************************************************/
        /*                               イベントハンドラ                               */
        /********************************************************************************/
        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 依存するサービスを注入して ViewModel を初期化し、各コマンドを生成する。
        /// </summary>
        /// <param name="pdfService">PDF 変換サービス</param>
        /// <param name="dialogService">ダイアログ表示サービス</param>
        /// <param name="clipboardService">クリップボード操作サービス</param>
        public MainViewModel(IPdfConversionService pdfService, IDialogService dialogService, IClipboardService clipboardService)
        {
            // サービス注入
            _pdfService = pdfService;
            _dialogService = dialogService;
            _clipboardService = clipboardService;

            // コマンド初期化
            BrowseCommand = new RelayCommand(OnBrowse);
            SaveCommand = new AsyncRelayCommand(OnSave, () => !string.IsNullOrEmpty(FilePath) && !IsBusy);
            CancelCommand = new RelayCommand(CancelOperation, () => IsBusy);
            CopyToClipboardCommand = new RelayCommand(OnCopyToClipboard, CanCopyToClipboard);
            GoToPageCommand = new AsyncRelayCommand(OnGoToPage, CanGoToPage);
            PreviousPageCommand = new AsyncRelayCommand(OnPreviousPage, CanGoPrevious);
            NextPageCommand = new AsyncRelayCommand(OnNextPage, CanGoNext);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// <see cref="PageNumber"/> を解析して現在プレビューすべきページ番号(1 始まり)を返す<br/>
        /// 不正値の場合は 1 または <see cref="PageCount"/> でクランプする
        /// </summary>
        private int CurrentPreviewPage => int.TryParse(PageNumber, out int value) ? (value < 1 ? 1 : (value > Math.Max(1, PageCount) ? Math.Max(1, PageCount) : value)) : 1;

        /// <summary>
        /// <see cref="FilePath"/>のPDFを検証して読み込み、ページ数取得とプレビュー生成を開始する
        /// </summary>
        /// <param name="forceReload"><c>true</c>のとき、同一パスでも再読み込みする</param>
        public void LoadPdfFromPath(bool forceReload = false)
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                return;
            }

            if (!File.Exists(FilePath))
            {
                PageCount = 0;
                PreviewImage = null;
                _loadedFilePath = null;
                StatusMessage = "ファイルが見つかりません。パスを確認してください。";
                return;
            }

            if (!Path.GetExtension(FilePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "PDFファイル (.pdf) を指定してください。";
                return;
            }

            if (!forceReload && string.Equals(FilePath, _loadedFilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _loadedFilePath = FilePath;
            StatusMessage = "ファイルを読み込み中...";
            _ = LoadPageCountAndConvertAsync();
        }

        /// <summary>
        /// ファイル選択ダイアログを表示し、選択されたPDFのパスを<see cref="FilePath"/>に設定する
        /// </summary>
        private void OnBrowse()
        {
            string selectedPath = _dialogService.ShowOpenPdfFileDialog();
            if (selectedPath != null)
            {
                FilePath = selectedPath;
                LoadPdfFromPath(forceReload: true);
            }
        }

        /// <summary>
        /// PDF のページ数を取得し、続けてプレビューを生成する<br/>
        /// ページ番号が範囲外の場合は 1 にリセットする
        /// </summary>
        private async Task LoadPageCountAndConvertAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            PrepareCancellation();
            CancellationToken cancellationToken = _cancelTokenSource.Token;
            string loadingPath = FilePath;

            try
            {
                PageCount = await _pdfService.GetPdfPageCountAsync(loadingPath, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                PageCount = 0;
                PreviewImage = null;
                _loadedFilePath = null;
                StatusMessage = $"PDFの読み込みに失敗しました: {ex.Message}";
                return;
            }

            if (!string.Equals(FilePath, loadingPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (PageCount > 0 && (!int.TryParse(PageNumber, out int current) || current < 1 || current > PageCount))
            {
                PageNumber = "1";
            }

            await OnConvert(showResolutionDialog: true);
        }

        /// <summary>
        /// PDFが読み込み済みのとき、現在の解像度設定でプレビューを再生成する
        /// </summary>
        private async Task RefreshPreviewIfLoadedAsync()
        {
            if (string.IsNullOrEmpty(FilePath) || PageCount <= 0 || IsBusy)
            {
                return;
            }

            await OnConvert();
        }

        /// <summary>前のページへ移動できるかどうかを返す</summary>
        private bool CanGoPrevious() => !IsBusy && PageCount > 0 && CurrentPreviewPage > 1;

        /// <summary>次のページへ移動できるかどうかを返す</summary>
        private bool CanGoNext() => !IsBusy && PageCount > 0 && CurrentPreviewPage < PageCount;

        /// <summary><see cref="PageNumber"/>が有効なページ番号を示しているかどうかを返す</summary>
        private bool CanGoToPage() => !IsBusy && PageCount > 0 && int.TryParse(PageNumber, out int pageNumber) && pageNumber >= 1 && pageNumber <= PageCount;

        /// <summary>クリップボードへのコピーが実行可能かどうかを返す</summary>
        private bool CanCopyToClipboard() => !IsBusy && PreviewImage != null;

        /// <summary>ページナビゲーション系コマンドの実行可否を一括で再評価する</summary>
        private void RaiseNavigationCanExecuteChanged()
        {
            RaiseCanExecuteChanged(PreviousPageCommand);
            RaiseCanExecuteChanged(NextPageCommand);
            RaiseCanExecuteChanged(GoToPageCommand);
        }

        /// <summary>保存・コピーなどのアクション系コマンドの実行可否を一括で再評価する</summary>
        private void RaiseActionCanExecuteChanged()
        {
            RaiseCanExecuteChanged(SaveCommand);
            RaiseCanExecuteChanged(GoToPageCommand);
            RaiseCanExecuteChanged(CancelCommand);
            RaiseCanExecuteChanged(CopyToClipboardCommand);
        }

        /// <summary>
        /// <see cref="IRelayCommand"/>の実行可否再評価を通知する
        /// </summary>
        private static void RaiseCanExecuteChanged(ICommand command)
        {
            if (command is IRelayCommand relayCommand)
            {
                relayCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// ページ番号入力欄の値でプレビューを更新する<br/>
        /// ページ数が未取得の場合はエラーメッセージを表示して中断する
        /// </summary>
        private async Task OnGoToPage()
        {
            if (PageCount <= 0)
            {
                StatusMessage = "ページ数が不明です。PDFを読み込んでください。";
                return;
            }

            await OnConvert(showResolutionDialog: true);
        }

        /// <summary>
        /// 新しい<see cref="CancellationTokenSource"/>を生成し、
        /// 前回の処理が残っている場合はキャンセルしてから置き換える
        /// </summary>
        private void PrepareCancellation()
        {
            _cancelTokenSource?.Cancel();
            _cancelTokenSource?.Dispose();
            _cancelTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 現在実行中の非同期処理にキャンセルを要求する<br/>
        /// すでにキャンセル済みの場合は何もしない
        /// </summary>
        private void CancelOperation()
        {
            if (_cancelTokenSource?.IsCancellationRequested == false)
            {
                _cancelTokenSource.Cancel();
                StatusMessage = "処理をキャンセルしています...";
            }
        }

        /// <summary>
        /// 前のページに移動してプレビューを更新する
        /// </summary>
        private async Task OnPreviousPage()
        {
            if (CurrentPreviewPage <= 1) return;
            PageNumber = (CurrentPreviewPage - 1).ToString();
            await OnConvert(showResolutionDialog: true);
        }

        /// <summary>
        /// プレビュー画像を白背景でフラット化してクリップボードにコピーする<br/>
        /// PDF ページが透明チャンネルを持つ場合、クリップボードの互換性を確保するために
        /// <see cref="DrawingVisual"/>で白背景を合成する
        /// </summary>
        private void OnCopyToClipboard()
        {
            if (PreviewImage == null)
            {
                StatusMessage = "コピーするプレビュー画像がありません。";
                return;
            }

            try
            {
                _clipboardService.CopyImage(PreviewImage);
                StatusMessage = "プレビュー画像をクリップボードにコピーしました。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"クリップボードへのコピーに失敗しました: {ex.Message}";
            }
        }

        /// <summary>
        /// 次のページに移動してプレビューを更新する
        /// </summary>
        private async Task OnNextPage()
        {
            if (CurrentPreviewPage >= PageCount) return;
            PageNumber = (CurrentPreviewPage + 1).ToString();
            await OnConvert(showResolutionDialog: true);
        }

        /// <summary>
        /// <see cref="PageNumber"/>が示すページのプレビューを非同期で生成する<br/>
        /// 前回の処理が実行中の場合はキャンセルしてから新しい処理を開始する
        /// </summary>
        /// <param name="showResolutionDialog">解像度の検証失敗時にダイアログを表示するかどうか</param>
        private async Task OnConvert(bool showResolutionDialog = false)
        {
            if (string.IsNullOrEmpty(FilePath)) return;
            ProgressValue = 0;
            PrepareCancellation();
            CancellationToken cancellationToken = _cancelTokenSource.Token;
            IsBusy = true;
            StatusMessage = "プレビュー生成中...";

            try
            {
                if (!int.TryParse(PageNumber, out int pageNumber) || pageNumber < 1)
                {
                    _dialogService.ShowMessage("有効なページ番号を入力してください。");
                    StatusMessage = "有効なページ番号を入力してください。";
                    return;
                }

                if (!TryGetResolutionValue(out double val, showDialog: showResolutionDialog))
                {
                    return;
                }

                int pageIndex = pageNumber - 1;
                PreviewImage = await _pdfService.ConvertPdfPageToImageAsync(FilePath, pageIndex, ResolutionMode, val, cancellationToken);
                StatusMessage = "プレビューを更新しました。";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "プレビュー変換をキャンセルしました。";
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"PDF変換エラー: {ex.Message}");
                StatusMessage = "プレビューの生成中にエラーが発生しました。";
            }
            finally
            {
                IsBusy = false;
                _cancelTokenSource?.Dispose();
                _cancelTokenSource = null;
            }
        }

        /// <summary>
        /// <see cref="PageRange"/>(または全ページ)をPNGファイルとして保存する非同期処理<br/>
        /// フォルダー選択ダイアログを表示し、選択されたフォルダーに<c>page_N.png</c>として出力する
        /// </summary>
        private async Task OnSave()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            if (!TryGetResolutionValue(out double resolutionValue, showDialog: true))
            {
                return;
            }

            ProgressValue = 0;
            PrepareCancellation();
            CancellationToken cancellationToken = _cancelTokenSource.Token;
            IsBusy = true;
            IsSaving = true;
            StatusMessage = "保存処理を開始しています...";

            try
            {
                string folderPath = _dialogService.ShowFolderBrowserDialog();
                if (folderPath == null)
                {
                    StatusMessage = "保存先フォルダーの選択がキャンセルされました。";
                    return;
                }
                IEnumerable<int> pageIndexes = null;
                if (!IsAllPagesSelected)
                {
                    if (string.IsNullOrWhiteSpace(PageRange))
                    {
                        _dialogService.ShowMessage("保存対象のページ番号または範囲を入力してください。");
                        StatusMessage = "保存対象のページ番号または範囲を指定してください。";
                        return;
                    }

                    try
                    {
                        pageIndexes = PageRangeParser.Parse(PageRange, PageCount);
                    }
                    catch (Exception ex)
                    {
                        _dialogService.ShowMessage($"ページ範囲の指定が不正です: {ex.Message}");
                        StatusMessage = "有効なページ範囲を入力してください。";
                        return;
                    }
                }

                if (!ConfirmOverwriteExistingFiles(folderPath, pageIndexes, IsAllPagesSelected))
                {
                    StatusMessage = "保存処理はキャンセルされました。";
                    return;
                }

                var progress = new Progress<SaveProgressReport>(report =>
                {
                    ProgressValue = report.Percentage;
                    StatusMessage = report.Message;
                });

                await _pdfService.SavePdfPagesToImagesAsync(FilePath, pageIndexes, folderPath, IsAllPagesSelected, ResolutionMode, resolutionValue, progress, cancellationToken);
                ProgressValue = 100;
                _dialogService.ShowMessage("画像の保存が完了しました。");
                StatusMessage = "画像の保存が完了しました。";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "保存処理はキャンセルされました。";
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"画像保存エラー: {ex.Message}");
                StatusMessage = "保存処理中にエラーが発生しました。";
            }
            finally
            {
                IsSaving = false;
                IsBusy = false;
                _cancelTokenSource?.Dispose();
                _cancelTokenSource = null;
            }
        }

        /// <summary>
        /// 現在の解像度設定を検証して数値を取得する
        /// </summary>
        /// <param name="value">解析された数値</param>
        /// <param name="showDialog">検証失敗時にダイアログを表示するかどうか</param>
        /// <returns>検証に成功した場合は <c>true</c></returns>
        private bool TryGetResolutionValue(out double value, bool showDialog)
        {
            if (ResolutionValueParser.TryParse(ResolutionMode, ResolutionValue, out value, out string errorMessage))
            {
                return true;
            }

            StatusMessage = errorMessage;
            if (showDialog)
            {
                _dialogService.ShowMessage(errorMessage);
            }

            return false;
        }

        /// <summary>
        /// 保存先に同名の<c>PNG</c>が存在する場合、上書きの確認ダイアログを表示する
        /// </summary>
        /// <param name="folderPath">保存先フォルダーの絶対パス</param>
        /// <param name="pageIndexes">保存対象のページインデックス一覧(0 始まり)</param>
        /// <param name="saveAllPages">全ページ保存かどうか</param>
        /// <returns>上書きして続行する場合は <c>true</c>、キャンセル時は <c>false</c></returns>
        private bool ConfirmOverwriteExistingFiles(string folderPath, IEnumerable<int> pageIndexes, bool saveAllPages)
        {
            IEnumerable<int> pagesToSave = saveAllPages
                ? Enumerable.Range(0, PageCount)
                : pageIndexes;

            var existingFiles = pagesToSave
                .Select(index => Path.Combine(folderPath, $"page_{index + 1}.png"))
                .Where(File.Exists)
                .Select(Path.GetFileName)
                .ToList();

            if (existingFiles.Count == 0)
            {
                return true;
            }

            string message = existingFiles.Count <= 5
                ? $"以下のファイルは既に存在します。上書きしますか？\n\n{string.Join("\n", existingFiles)}"
                : $"{existingFiles.Count} 件のファイルが既に存在します。上書きしますか？";

            return _dialogService.ShowYesNo(message, "上書きの確認", DialogIcon.Warning);
        }

        /// <summary>
        /// 指定したプロパティの変更を通知する<br/>
        /// <see cref="CallerMemberNameAttribute"/>によりプロパティ名を自動取得する
        /// </summary>
        /// <param name="propertyName">変更されたプロパティの名前</param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
