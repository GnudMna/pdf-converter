using System.Globalization;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// 解像度モードと数値入力の検証・解析を行うユーティリティ
    /// </summary>
    public static class ResolutionValueParser
    {
        /// <summary>
        /// 解像度の数値入力を検証して解析する
        /// </summary>
        /// <param name="mode">解像度の指定方法</param>
        /// <param name="resolutionValue">数値の文字列表現</param>
        /// <param name="value">解析された数値</param>
        /// <param name="errorMessage">検証失敗時のメッセージ</param>
        /// <returns>検証に成功した場合は <c>true</c></returns>
        public static bool TryParse(ResolutionMode mode, string resolutionValue, out double value, out string errorMessage)
        {
            value = 0;
            errorMessage = null;

            if (mode == ResolutionMode.Default)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(resolutionValue))
            {
                errorMessage = GetRequiredMessage(mode);
                return false;
            }

            if (!double.TryParse(resolutionValue.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                && !double.TryParse(resolutionValue.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                errorMessage = "解像度には数値を入力してください。";
                return false;
            }

            if (value <= 0)
            {
                errorMessage = "解像度には 0 より大きい値を入力してください。";
                return false;
            }

            if (mode == ResolutionMode.Dpi && value > 1200)
            {
                errorMessage = "DPI は 1200 以下で指定してください。";
                return false;
            }

            if ((mode == ResolutionMode.Width || mode == ResolutionMode.Height) && value > 30000)
            {
                errorMessage = "ピクセル値は 30000 以下で指定してください。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// モードに応じた必須入力メッセージを返す
        /// </summary>
        private static string GetRequiredMessage(ResolutionMode mode)
        {
            switch (mode)
            {
                case ResolutionMode.Width:
                    return "幅 (px) を入力してください。";
                case ResolutionMode.Height:
                    return "高さ (px) を入力してください。";
                case ResolutionMode.Dpi:
                    return "DPI を入力してください。";
                default:
                    return "解像度の数値を入力してください。";
            }
        }
    }
}
