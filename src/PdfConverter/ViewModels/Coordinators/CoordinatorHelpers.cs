using PdfConverter.Models;
using PdfConverter.Services;

namespace PdfConverter.ViewModels.Coordinators
{
    /// <summary>
    /// Coordinator 間で共有する補助メソッド
    /// </summary>
    internal static class CoordinatorHelpers
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 現在の解像度設定を検証して数値を取得する
        /// </summary>
        /// <param name="resolution">解像度入力ホスト</param>
        /// <param name="session">ステータス更新先のセッション</param>
        /// <param name="value">解析された数値</param>
        /// <param name="showFieldValidation">入力欄の検証メッセージを表示するかどうか</param>
        /// <returns>true: 解析に成功した / false: 解析に失敗した</returns>
        public static bool TryGetResolutionValue(
            IResolutionInputHost resolution,
            ICoordinatorSession session,
            out double value,
            bool showFieldValidation)
        {
            if (ResolutionValueParser.TryParse(resolution.ResolutionMode, resolution.ResolutionValue, out value, out string errorMessage))
            {
                resolution.ResolutionValidationMessage = null;
                return true;
            }

            if (showFieldValidation)
            {
                resolution.ResolutionValidationMessage = errorMessage;
            }

            session.SetStatus(errorMessage, StatusKind.Warning);
            return false;
        }
    }
}
