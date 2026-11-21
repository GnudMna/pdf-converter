using System;
using System.Collections.Generic;
using PdfConverter.Models;
using PdfConverter.Services;

namespace PdfConverter.ViewModels
{
    /// <summary>
    /// Word → PDF 変換に関する UI 設定を管理する ViewModel
    /// </summary>
    public sealed class WordConversionSettingsViewModel : ViewModelBase
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>Word → PDF 変換設定</summary>
        private readonly IWordToPdfConversionSettings _settings;

        /// <summary>設定変更後に読み込み済み Word 文書を再読み込みするコールバック</summary>
        private readonly Action _reloadDocument;

        /// <summary>Word → PDF 変換エンジン</summary>
        private WordToPdfBackend _backend;

        /// <summary>LibreOffice の <c>soffice.exe</c> のパス</summary>
        private string _libreOfficePath;

        /// <summary>Word 設定セクションを展開するかどうか</summary>
        private bool _isSectionExpanded;

        /// <summary>Word → PDF 変換時の PDF 形式</summary>
        private WordToPdfPdfFormat _pdfFormat;

        /// <summary>Word → PDF 変換時の出力最適化モード</summary>
        private WordToPdfOptimizeFor _optimizeFor;

        /// <summary>Word → PDF 変換時にしおりを出力するかどうか</summary>
        private bool _exportBookmarks;

        /// <summary>Word → PDF 変換時にコメントを出力するかどうか</summary>
        private bool _exportComments;

        /// <summary>Word → PDF 変換エンジンの選択肢</summary>
        private static readonly IReadOnlyList<WordToPdfBackendOption> BackendOptionsList =
            new List<WordToPdfBackendOption>
            {
                new WordToPdfBackendOption(WordToPdfBackend.MicrosoftWord, "Microsoft Word"),
                new WordToPdfBackendOption(WordToPdfBackend.LibreOffice, "LibreOffice"),
            };

        /// <summary>Word → PDF 変換時の PDF 形式の選択肢</summary>
        private static readonly IReadOnlyList<WordToPdfPdfFormatOption> PdfFormatOptionsList =
            new List<WordToPdfPdfFormatOption>
            {
                new WordToPdfPdfFormatOption(WordToPdfPdfFormat.Standard, "標準 PDF"),
                new WordToPdfPdfFormatOption(WordToPdfPdfFormat.PdfA, "PDF/A-1"),
            };

        /// <summary>Word → PDF 変換時の出力最適化モードの選択肢</summary>
        private static readonly IReadOnlyList<WordToPdfOptimizeForOption> OptimizeForOptionsList =
            new List<WordToPdfOptimizeForOption>
            {
                new WordToPdfOptimizeForOption(WordToPdfOptimizeFor.Print, "印刷向け"),
                new WordToPdfOptimizeForOption(WordToPdfOptimizeFor.Online, "オンライン向け"),
            };


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 保存済み設定を読み込み、Word 変換設定 ViewModel を初期化する
        /// </summary>
        /// <param name="settings">Word → PDF 変換設定</param>
        /// <param name="reloadDocument">設定変更後に読み込み済み Word 文書を再読み込みするコールバック</param>
        public WordConversionSettingsViewModel(IWordToPdfConversionSettings settings, Action reloadDocument)
        {
            _settings = settings;
            _reloadDocument = reloadDocument;
            _backend = settings.Backend;
            _libreOfficePath = settings.LibreOfficePath ?? string.Empty;
            _pdfFormat = settings.PdfFormat;
            _optimizeFor = settings.OptimizeFor;
            _exportBookmarks = settings.ExportBookmarks;
            _exportComments = settings.ExportComments;
        }


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>Word 設定セクションを展開するかどうか</summary>
        public bool IsSectionExpanded
        {
            get => _isSectionExpanded;
            set => SetProperty(ref _isSectionExpanded, value);
        }

        /// <summary>Word → PDF 変換エンジン</summary>
        public WordToPdfBackend Backend
        {
            get => _backend;
            set
            {
                if (!SetProperty(ref _backend, value))
                {
                    return;
                }

                if (_settings.Backend != value)
                {
                    _settings.Backend = value;
                    ApplySettingsChange(reloadDocument: true);
                }

                OnPropertyChanged(nameof(IsLibreOfficePathVisible));
            }
        }

        /// <summary>Word → PDF 変換エンジンの選択肢</summary>
        public IReadOnlyList<WordToPdfBackendOption> BackendOptions => BackendOptionsList;

        /// <summary>LibreOffice の <c>soffice.exe</c> のパス</summary>
        public string LibreOfficePath
        {
            get => _libreOfficePath;
            set
            {
                string normalizedValue = value ?? string.Empty;
                if (!SetProperty(ref _libreOfficePath, normalizedValue))
                {
                    return;
                }

                _settings.LibreOfficePath = normalizedValue;
                if (_settings.Backend == WordToPdfBackend.LibreOffice)
                {
                    ApplySettingsChange(reloadDocument: true);
                }
                else
                {
                    _settings.Save();
                }
            }
        }

        /// <summary>LibreOffice の <c>soffice.exe</c> のパス入力欄を表示するかどうか</summary>
        public bool IsLibreOfficePathVisible => Backend == WordToPdfBackend.LibreOffice;

        /// <summary>Word → PDF 変換時の PDF 形式</summary>
        public WordToPdfPdfFormat PdfFormat
        {
            get => _pdfFormat;
            set
            {
                if (!SetProperty(ref _pdfFormat, value))
                {
                    return;
                }

                if (_settings.PdfFormat != value)
                {
                    _settings.PdfFormat = value;
                    ApplySettingsChange(reloadDocument: true);
                }
            }
        }

        /// <summary>Word → PDF 変換時の PDF 形式の選択肢</summary>
        public IReadOnlyList<WordToPdfPdfFormatOption> PdfFormatOptions => PdfFormatOptionsList;

        /// <summary>Word → PDF 変換時の出力最適化モード</summary>
        public WordToPdfOptimizeFor OptimizeFor
        {
            get => _optimizeFor;
            set
            {
                if (!SetProperty(ref _optimizeFor, value))
                {
                    return;
                }

                if (_settings.OptimizeFor != value)
                {
                    _settings.OptimizeFor = value;
                    ApplySettingsChange(reloadDocument: true);
                }
            }
        }

        /// <summary>Word → PDF 変換時の出力最適化モードの選択肢</summary>
        public IReadOnlyList<WordToPdfOptimizeForOption> OptimizeForOptions => OptimizeForOptionsList;

        /// <summary>Word → PDF 変換時にしおりを出力するかどうか</summary>
        public bool ExportBookmarks
        {
            get => _exportBookmarks;
            set
            {
                if (!SetProperty(ref _exportBookmarks, value))
                {
                    return;
                }

                if (_settings.ExportBookmarks != value)
                {
                    _settings.ExportBookmarks = value;
                    ApplySettingsChange(reloadDocument: true);
                }
            }
        }

        /// <summary>Word → PDF 変換時にコメントを出力するかどうか</summary>
        public bool ExportComments
        {
            get => _exportComments;
            set
            {
                if (!SetProperty(ref _exportComments, value))
                {
                    return;
                }

                if (_settings.ExportComments != value)
                {
                    _settings.ExportComments = value;
                    ApplySettingsChange(reloadDocument: true);
                }
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>設定を保存し、必要に応じて Word 文書を再読み込みする</summary>
        /// <param name="reloadDocument">読み込み済み Word 文書を再読み込みするかどうか</param>
        private void ApplySettingsChange(bool reloadDocument)
        {
            _settings.Save();
            if (reloadDocument)
            {
                _reloadDocument?.Invoke();
            }
        }
    }
}
