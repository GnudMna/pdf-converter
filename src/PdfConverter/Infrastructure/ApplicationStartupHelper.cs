using System;
using System.IO;
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
        /// PDFium などのネイティブ DLL を検索するディレクトリパスを返す
        /// </summary>
        /// <param name="assemblyLocation">実行アセンブリの配置パス</param>
        /// <returns><c>lib</c> サブフォルダーの絶対パス</returns>
        public static string GetNativeDllSearchPath(string assemblyLocation)
        {
            string exeDir = string.IsNullOrWhiteSpace(assemblyLocation)
                ? null
                : Path.GetDirectoryName(assemblyLocation);
            exeDir ??= AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(exeDir, "lib");
        }

        /// <summary>
        /// 起動引数から最初のサポート対象ドキュメントパスを取得する
        /// </summary>
        /// <param name="args">起動引数</param>
        /// <param name="filePath">見つかったファイルパス</param>
        /// <returns>サポート対象のパスが見つかった場合は <c>true</c></returns>
        public static bool TryGetInitialDocumentPath(string[] args, out string filePath)
        {
            return DocumentFileHelper.TryGetFirstSupportedPathFromArgs(args, out filePath);
        }
    }
}
