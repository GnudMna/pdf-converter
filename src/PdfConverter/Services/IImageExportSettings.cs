using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// ユーザー設定に永続化する画像出力設定
    /// </summary>
    public interface IImageExportSettings
    {
        /// <summary>出力画像形式</summary>
        OutputImageFormat OutputImageFormat { get; set; }

        /// <summary>解像度の指定方法</summary>
        ResolutionMode ResolutionMode { get; set; }

        /// <summary>解像度の値</summary>
        string ResolutionValue { get; set; }

        /// <summary>透明度を保持するかどうか</summary>
        bool PreserveTransparency { get; set; }

        /// <summary>現在の設定を保存する</summary>
        void Save();
    }
}
