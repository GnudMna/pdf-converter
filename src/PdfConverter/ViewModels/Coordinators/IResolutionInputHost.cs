using PdfConverter.Models;

namespace PdfConverter.ViewModels.Coordinators
{
    /// <summary>
    /// Coordinator が解像度入力を検証するためのインターフェース
    /// </summary>
    public interface IResolutionInputHost
    {
        /// <summary>解像度の指定方法</summary>
        ResolutionMode ResolutionMode { get; }

        /// <summary>解像度の値</summary>
        string ResolutionValue { get; }

        /// <summary>出力解像度の入力エラーメッセージ</summary>
        string ResolutionValidationMessage { get; set; }
    }
}
