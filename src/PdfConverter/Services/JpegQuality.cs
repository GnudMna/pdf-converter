using System.Globalization;

namespace PdfConverter.Services
{
    /// <summary>
    /// JPEG 品質の範囲と検証に関する共通処理
    /// </summary>
    public static class JpegQuality
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>JPEG 品質の最小値</summary>
        public const int Min = 1;

        /// <summary>JPEG 品質の最大値</summary>
        public const int Max = 100;

        /// <summary>JPEG 品質の既定値</summary>
        public const int Default = 90;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// JPEG 品質が有効範囲 (<see cref="Min"/>〜<see cref="Max"/>) に収まるかどうかを返す
        /// </summary>
        /// <param name="quality">JPEG 品質</param>
        /// <returns>true: 有効範囲内</returns>
        public static bool IsValid(int quality)
        {
            return quality >= Min && quality <= Max;
        }

        /// <summary>
        /// JPEG 品質を有効範囲に収める
        /// </summary>
        /// <param name="quality">JPEG 品質</param>
        /// <returns>有効範囲に収めた JPEG 品質</returns>
        public static int Clamp(int quality)
        {
            return System.Math.Clamp(quality, Min, Max);
        }

        /// <summary>
        /// 保存済みの JPEG 品質を検証し、不正な場合は既定値にフォールバックする
        /// </summary>
        /// <param name="storedValue">保存済みの JPEG 品質</param>
        /// <returns>有効な JPEG 品質</returns>
        public static int Resolve(int storedValue)
        {
            return IsValid(storedValue) ? storedValue : Default;
        }

        /// <summary>
        /// 文字列を JPEG 品質として解析する
        /// </summary>
        /// <param name="value">入力文字列</param>
        /// <param name="quality">解析した JPEG 品質</param>
        /// <returns>true: 整数かつ有効範囲内</returns>
        public static bool TryParse(string? value, out int quality)
        {
            return int.TryParse(value?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out quality)
                && IsValid(quality);
        }
    }
}
