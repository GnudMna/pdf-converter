using PdfConverter.Models;

namespace PdfConverter.ViewModels.Coordinators
{
    /// <summary>
    /// <see cref="IPdfSaveCoordinator"/> が保存操作に必要とする ViewModel 状態
    /// </summary>
    public interface ISaveCoordinatorHost : ICoordinatorSession, IResolutionInputHost
    {
        /// <summary>読み込むドキュメントのパス</summary>
        string FilePath { get; }

        /// <summary>PDF のページ数</summary>
        int PageCount { get; }

        /// <summary>保存するページの範囲</summary>
        string PageRange { get; }

        /// <summary>全ページを保存するかどうか</summary>
        bool IsAllPagesSelected { get; }

        /// <summary>出力画像形式</summary>
        OutputImageFormat OutputImageFormat { get; }

        /// <summary>透明度を保持するかどうか</summary>
        bool PreserveTransparency { get; }

        /// <summary>保存ページ範囲の入力エラーメッセージ</summary>
        string PageRangeValidationMessage { get; set; }
    }
}
