using PdfConverter.Models;
using PdfConverter.Services;

namespace PdfConverter.ViewModels.Coordinators
{
    /// <summary>
    /// Coordinator間で共有する補助メソッド
    /// </summary>
    internal static class CoordinatorHelpers
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 現在の解像度設定を検証して数値を取得する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <param name="dialogService">ダイアログサービス</param>
        /// <param name="value">解析された数値</param>
        /// <param name="showDialog">ダイアログを表示するかどうか</param>
        /// <returns>解析に成功した場合は <c>true</c></returns>
        public static bool TryGetResolutionValue(
            IMainViewModelHost host,
            IDialogService dialogService,
            out double value,
            bool showDialog)
        {
            if (ResolutionValueParser.TryParse(host.ResolutionMode, host.ResolutionValue, out value, out string errorMessage))
            {
                return true;
            }

            host.StatusMessage = errorMessage;
            if (showDialog)
            {
                dialogService.ShowMessage(errorMessage);
            }

            return false;
        }
    }
}
