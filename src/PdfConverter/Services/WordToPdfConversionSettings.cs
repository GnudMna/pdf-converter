using System;
using PdfConverter.Models;
using PdfConverter.Properties;

namespace PdfConverter.Services
{
    /// <summary>
    /// ユーザー設定に永続化する Word → PDF 変換エンジン設定
    /// </summary>
    public sealed class WordToPdfConversionSettings : IWordToPdfConversionSettings
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <inheritdoc/>
        public WordToPdfBackend Backend { get; set; }

        /// <inheritdoc/>
        public string LibreOfficePath { get; set; }

        /// <inheritdoc/>
        public WordToPdfPdfFormat PdfFormat { get; set; }

        /// <inheritdoc/>
        public WordToPdfOptimizeFor OptimizeFor { get; set; }

        /// <inheritdoc/>
        public bool ExportBookmarks { get; set; }

        /// <inheritdoc/>
        public bool ExportComments { get; set; }


        /********************************************************************************/
        /*                                   イベント                                   */
        /********************************************************************************/
        /// <inheritdoc/>
        public event EventHandler SettingsChanged;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 保存済み設定を読み込んで初期化する
        /// </summary>
        public WordToPdfConversionSettings()
        {
            Backend = WordToPdfBackendParser.Parse(Settings.Default.WordToPdfBackend);
            LibreOfficePath = Settings.Default.LibreOfficePath ?? string.Empty;
            PdfFormat = WordToPdfPdfFormatParser.Parse(Settings.Default.WordToPdfPdfFormat);
            OptimizeFor = WordToPdfOptimizeForParser.Parse(Settings.Default.WordToPdfOptimizeFor);
            ExportBookmarks = Settings.Default.WordToPdfExportBookmarks;
            ExportComments = Settings.Default.WordToPdfExportComments;
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public void Save()
        {
            Settings.Default.WordToPdfBackend = Backend.ToString();
            Settings.Default.LibreOfficePath = LibreOfficePath ?? string.Empty;
            Settings.Default.WordToPdfPdfFormat = PdfFormat.ToString();
            Settings.Default.WordToPdfOptimizeFor = OptimizeFor.ToString();
            Settings.Default.WordToPdfExportBookmarks = ExportBookmarks;
            Settings.Default.WordToPdfExportComments = ExportComments;
            Settings.Default.Save();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
