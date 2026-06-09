using System;
using System.Collections.Generic;
using PdfConverter.Models;
using PdfConverter.Services;

namespace PdfConverter.ViewModels
{
    /// <summary>
    /// 画像出力 (形式・解像度・ページ範囲) に関する UI 設定を管理する ViewModel
    /// </summary>
    public sealed class ImageExportSettingsViewModel : ViewModelBase
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>画像出力設定</summary>
        private readonly IImageExportSettings _settings;

        /// <summary>プレビュー再生成を要求するコールバック</summary>
        private readonly Action _requestPreviewRefresh;

        /// <summary>保存コマンドの実行可能状態を更新するコールバック</summary>
        private readonly Action _raiseSaveCanExecuteChanged;

        /// <summary>保存するページの範囲</summary>
        private string _pageRange = "1";

        /// <summary>全ページを保存するかどうか</summary>
        private bool _isAllPagesSelected;

        /// <summary>解像度の指定方法</summary>
        private ResolutionMode _resolutionMode;

        /// <summary>解像度の値</summary>
        private string _resolutionValue;

        /// <summary>出力画像形式</summary>
        private OutputImageFormat _outputImageFormat;

        /// <summary>透明度を保持するかどうか</summary>
        private bool _preserveTransparency;

        /// <summary>出力解像度の入力エラーメッセージ</summary>
        private string _resolutionValidationMessage;

        /// <summary>保存ページ範囲の入力エラーメッセージ</summary>
        private string _pageRangeValidationMessage;

        /// <summary>解像度の選択肢</summary>
        private static readonly IReadOnlyList<ResolutionModeOption> ResolutionModeOptionsList =
            new List<ResolutionModeOption>
            {
                new ResolutionModeOption(ResolutionMode.Width, "幅 (px)"),
                new ResolutionModeOption(ResolutionMode.Height, "高さ (px)"),
                new ResolutionModeOption(ResolutionMode.Dpi, "DPI"),
            };

        /// <summary>出力画像形式の選択肢</summary>
        private static readonly IReadOnlyList<OutputImageFormatOption> OutputImageFormatOptionsList =
            new List<OutputImageFormatOption>
            {
                new OutputImageFormatOption(OutputImageFormat.Png, "PNG"),
                new OutputImageFormatOption(OutputImageFormat.Jpeg, "JPEG"),
                new OutputImageFormatOption(OutputImageFormat.Bmp, "BMP"),
            };


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 画像出力設定 ViewModel を初期化する
        /// </summary>
        /// <param name="settings">画像出力設定</param>
        /// <param name="requestPreviewRefresh">プレビュー再生成を要求するコールバック</param>
        /// <param name="raiseSaveCanExecuteChanged">保存コマンドの実行可能状態を更新するコールバック</param>
        public ImageExportSettingsViewModel(
            IImageExportSettings settings,
            Action requestPreviewRefresh,
            Action raiseSaveCanExecuteChanged)
        {
            _settings = settings;
            _requestPreviewRefresh = requestPreviewRefresh;
            _raiseSaveCanExecuteChanged = raiseSaveCanExecuteChanged;
            _outputImageFormat = settings.OutputImageFormat;
            _resolutionMode = settings.ResolutionMode;
            _resolutionValue = settings.ResolutionValue;
            _preserveTransparency = settings.PreserveTransparency;
        }


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
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

                PageRangeValidationMessage = null;
                _raiseSaveCanExecuteChanged?.Invoke();
            }
        }

        /// <summary>全ページを保存するかどうか</summary>
        public bool IsAllPagesSelected
        {
            get => _isAllPagesSelected;
            set => SetProperty(ref _isAllPagesSelected, value);
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
                ResolutionValidationMessage = null;
                PersistSettings();
                _requestPreviewRefresh?.Invoke();
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

                ResolutionValidationMessage = null;
                PersistSettings();
                _requestPreviewRefresh?.Invoke();
            }
        }

        /// <summary>解像度の選択肢</summary>
        public IReadOnlyList<ResolutionModeOption> ResolutionModeOptions => ResolutionModeOptionsList;

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
                PersistSettings();
            }
        }

        /// <summary>出力画像形式の選択肢</summary>
        public IReadOnlyList<OutputImageFormatOption> OutputImageFormatOptions => OutputImageFormatOptionsList;

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

                PersistSettings();
                _requestPreviewRefresh?.Invoke();
            }
        }

        /// <summary>透明度を選択可能かどうか</summary>
        public bool IsTransparencySelectable => ImageBitmapHelper.SupportsTransparency(OutputImageFormat);

        /// <summary>出力解像度の入力エラーメッセージ</summary>
        public string ResolutionValidationMessage
        {
            get => _resolutionValidationMessage;
            set => SetProperty(ref _resolutionValidationMessage, value);
        }

        /// <summary>保存ページ範囲の入力エラーメッセージ</summary>
        public string PageRangeValidationMessage
        {
            get => _pageRangeValidationMessage;
            set => SetProperty(ref _pageRangeValidationMessage, value);
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>検証メッセージをクリアする</summary>
        public void ClearValidationMessages()
        {
            ResolutionValidationMessage = null;
            PageRangeValidationMessage = null;
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>永続化対象の設定を保存する</summary>
        private void PersistSettings()
        {
            if (_settings == null)
            {
                return;
            }

            _settings.OutputImageFormat = _outputImageFormat;
            _settings.ResolutionMode = _resolutionMode;
            _settings.ResolutionValue = _resolutionValue;
            _settings.PreserveTransparency = _preserveTransparency;
            _settings.Save();
        }
    }
}
