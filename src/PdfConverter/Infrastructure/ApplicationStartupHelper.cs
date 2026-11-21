using System.Diagnostics.CodeAnalysis;
using PdfConverter.Services;

namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// アプリケーション起動時のパス解決など、テスト可能な起動補助ロジック
    /// </summary>
    internal static class ApplicationStartupHelper
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 起動引数から最初のサポート対象ドキュメントパスを取得する
        /// </summary>
        /// <param name="args">起動引数</param>
        /// <param name="filePath">見つかったファイルパス</param>
        /// <returns>サポート対象のパスが見つかった場合は <c>true</c></returns>
        public static bool TryGetInitialDocumentPath(string[]? args, [NotNullWhen(true)] out string? filePath)
        {
            return DocumentFileHelper.TryGetFirstSupportedPathFromArgs(args, out filePath);
        }
    }
}
