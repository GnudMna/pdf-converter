using System;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// Word → PDF 変換エンジンの設定
    /// </summary>
    public interface IWordToPdfConversionSettings
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>使用する変換エンジン</summary>
        WordToPdfBackend Backend { get; set; }

        /// <summary>LibreOffice の <c>soffice.exe</c> のパス (空の場合は自動検出)</summary>
        string LibreOfficePath { get; set; }

        /// <summary>出力する PDF の形式</summary>
        WordToPdfPdfFormat PdfFormat { get; set; }

        /// <summary>出力最適化モード</summary>
        WordToPdfOptimizeFor OptimizeFor { get; set; }

        /// <summary>しおり (見出し) を PDF に出力するかどうか</summary>
        bool ExportBookmarks { get; set; }

        /// <summary>コメントを PDF に出力するかどうか</summary>
        bool ExportComments { get; set; }


        /********************************************************************************/
        /*                                   イベント                                   */
        /********************************************************************************/
        /// <summary>設定が変更されたときに発生する</summary>
        event EventHandler SettingsChanged;


        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>現在の設定を永続化する</summary>
        void Save();
    }
}
