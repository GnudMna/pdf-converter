using PdfConverter.Models;
using PdfConverter.Properties;

namespace PdfConverter.Services
{
    /// <summary>
    /// ユーザー設定に永続化する画像出力設定
    /// </summary>
    public sealed class ImageExportSettings : IImageExportSettings
    {
        /// <inheritdoc/>
        public OutputImageFormat OutputImageFormat { get; set; }

        /// <inheritdoc/>
        public ResolutionMode ResolutionMode { get; set; }

        /// <inheritdoc/>
        public string ResolutionValue { get; set; }

        /// <inheritdoc/>
        public bool PreserveTransparency { get; set; }

        /// <summary>
        /// 保存済み設定を読み込んで初期化する
        /// </summary>
        public ImageExportSettings()
        {
            OutputImageFormat = OutputImageFormatParser.Parse(Settings.Default.OutputImageFormat);
            ResolutionMode = ResolutionModeParser.Parse(Settings.Default.ResolutionMode);
            ResolutionValue = ResolveResolutionValue(ResolutionMode, Settings.Default.ResolutionValue);
            PreserveTransparency = ResolvePreserveTransparency(OutputImageFormat, Settings.Default.PreserveTransparency);
        }

        /// <inheritdoc/>
        public void Save()
        {
            Settings.Default.OutputImageFormat = OutputImageFormat.ToString();
            Settings.Default.ResolutionMode = ResolutionMode.ToString();
            Settings.Default.ResolutionValue = ResolutionValue ?? string.Empty;
            Settings.Default.PreserveTransparency = PreserveTransparency;
            Settings.Default.Save();
        }

        /// <summary>
        /// 保存済みの解像度値を検証し、不正な場合は既定値にフォールバックする
        /// </summary>
        /// <param name="mode">解像度の指定方法</param>
        /// <param name="storedValue">保存済みの解像度値</param>
        /// <returns>有効な解像度値</returns>
        internal static string ResolveResolutionValue(ResolutionMode mode, string storedValue)
        {
            if (ResolutionValueParser.TryParse(mode, storedValue, out _, out _))
            {
                return storedValue.Trim();
            }

            return ResolutionValueParser.GetDefaultValue(mode);
        }

        /// <summary>
        /// 出力形式に応じて透明度保持設定を解決する
        /// </summary>
        /// <param name="format">出力画像形式</param>
        /// <param name="storedValue">保存済みの透明度保持設定</param>
        /// <returns>有効な透明度保持設定</returns>
        internal static bool ResolvePreserveTransparency(OutputImageFormat format, bool storedValue)
        {
            return ImageBitmapHelper.SupportsTransparency(format) && storedValue;
        }
    }
}
