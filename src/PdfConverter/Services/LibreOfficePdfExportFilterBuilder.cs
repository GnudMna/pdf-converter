using System.Collections.Generic;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// LibreOffice の <c>writer_pdf_Export</c> フィルタ引数を組み立てる
    /// </summary>
    internal static class LibreOfficePdfExportFilterBuilder
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// <c>--convert-to</c> に渡す変換形式文字列を生成する
        /// </summary>
        /// <param name="settings">Word → PDF 変換設定</param>
        /// <returns>変換形式文字列</returns>
        public static string BuildConvertToArgument(IWordToPdfConversionSettings settings)
        {
            string filterJson = BuildFilterJson(settings);
            if (string.IsNullOrEmpty(filterJson))
            {
                return "pdf";
            }

            return "pdf:writer_pdf_Export:" + filterJson;
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// フィルタ JSON を生成する (既定値のみの場合は空文字列)
        /// </summary>
        /// <param name="settings">Word → PDF 変換設定</param>
        /// <returns>フィルタ JSON</returns>
        private static string BuildFilterJson(IWordToPdfConversionSettings settings)
        {
            var properties = new List<string>();

            if (settings.PdfFormat != WordToPdfPdfFormat.Standard)
            {
                properties.Add(BuildLongProperty("SelectPdfVersion", GetSelectPdfVersion(settings.PdfFormat)));
            }

            if (!settings.ExportBookmarks)
            {
                properties.Add(BuildBooleanProperty("ExportBookmarks", false));
            }

            if (settings.ExportComments)
            {
                properties.Add(BuildBooleanProperty("ExportNotes", true));
            }

            if (settings.OptimizeFor == WordToPdfOptimizeFor.Online)
            {
                properties.Add(BuildBooleanProperty("ReduceImageResolution", true));
                properties.Add(BuildLongProperty("Quality", 75));
            }

            if (properties.Count == 0)
            {
                return string.Empty;
            }

            return "{" + string.Join(",", properties) + "}";
        }

        /// <summary>
        /// PDF 形式を LibreOffice の <c>SelectPdfVersion</c> 値に変換する
        /// </summary>
        /// <param name="format">PDF 形式</param>
        /// <returns><c>SelectPdfVersion</c> 値</returns>
        private static int GetSelectPdfVersion(WordToPdfPdfFormat format)
        {
            switch (format)
            {
                case WordToPdfPdfFormat.PdfA:
                    return 1;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// long 型フィルタプロパティを JSON 断片として生成する
        /// </summary>
        private static string BuildLongProperty(string name, int value)
        {
            return "\"" + name + "\":{\"type\":\"long\",\"value\":\"" + value + "\"}";
        }

        /// <summary>
        /// boolean 型フィルタプロパティを JSON 断片として生成する
        /// </summary>
        private static string BuildBooleanProperty(string name, bool value)
        {
            return "\"" + name + "\":{\"type\":\"boolean\",\"value\":\"" + value.ToString().ToLowerInvariant() + "\"}";
        }
    }
}
