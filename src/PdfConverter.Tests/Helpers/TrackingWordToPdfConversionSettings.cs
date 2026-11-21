using System;
using PdfConverter.Models;
using PdfConverter.Services;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// 保存回数を追跡できる <see cref="IWordToPdfConversionSettings"/> のテスト用実装
    /// </summary>
    internal sealed class TrackingWordToPdfConversionSettings : IWordToPdfConversionSettings
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        public WordToPdfBackend Backend { get; set; } = WordToPdfBackend.MicrosoftWord;

        public string LibreOfficePath { get; set; } = string.Empty;

        public WordToPdfPdfFormat PdfFormat { get; set; } = WordToPdfPdfFormat.Standard;

        public WordToPdfOptimizeFor OptimizeFor { get; set; } = WordToPdfOptimizeFor.Print;

        public bool ExportBookmarks { get; set; } = true;

        public bool ExportComments { get; set; }

        public int SaveCount { get; private set; }

        public event EventHandler SettingsChanged { add { } remove { } }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        public void Save()
        {
            SaveCount++;
        }

        public void Reload()
        {
        }
    }
}
