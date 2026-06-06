using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// PDF ファイルをページ単位で画像に変換・保存するサービスのコントラクト
    /// </summary>
    /// <remarks>
    /// テスト時にモックへ差し替えられるよう、インターフェースとして定義する
    /// </remarks>
    public interface IPdfConversionService
    {
        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>
        /// PDFファイルの総ページ数を非同期で取得する
        /// </summary>
        /// <param name="filePath">PDFファイルの絶対パス</param>
        /// <returns>PDFの総ページ数</returns>
        /// <exception cref="System.IO.FileNotFoundException">ファイルが存在しない場合</exception>
        Task<int> GetPdfPageCountAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// PDFの指定ページをビットマップに変換して返す
        /// </summary>
        /// <param name="filePath">PDF ファイルの絶対パス</param>
        /// <param name="pageIndex">変換対象のページインデックス(0 始まり)</param>
        /// <param name="mode">解像度の指定方法</param>
        /// <param name="value"><paramref name="mode"/>に対応する数値(幅・高さ・DPI)</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>変換されたページのビットマップ</returns>
        /// <exception cref="System.IO.FileNotFoundException">ファイルが存在しない場合</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/>がページ数の範囲外の場合</exception>
        Task<BitmapSource> ConvertPdfPageToImageAsync(string filePath, int pageIndex, ResolutionMode mode = ResolutionMode.Default, double value = 0, CancellationToken cancellationToken = default);

        /// <summary>
        /// 指定したページ群をPNGファイルとしてフォルダーへ一括保存する
        /// </summary>
        /// <remarks>
        /// CPUコア数に応じた並列処理で保存し、進捗を<paramref name="progress"/>で報告する
        /// </remarks>
        /// <param name="filePath">PDF ファイルの絶対パス</param>
        /// <param name="pageIndexes">保存対象のページインデックス一覧(0 始まり)<br/><paramref name="saveAllPages"/>が<c>true</c>の場合は無視される</param>
        /// <param name="folderPath">保存先フォルダーの絶対パス</param>
        /// <param name="saveAllPages"><c>true</c>の場合は全ページを保存する</param>
        /// <param name="mode">解像度の指定方法</param>
        /// <param name="value"><paramref name="mode"/>に対応する数値(幅・高さ・DPI)</param>
        /// <param name="progress">進捗通知用コールバック<br/><c>null</c>の場合は通知しない</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <exception cref="System.IO.FileNotFoundException">ファイルが存在しない場合</exception>
        /// <exception cref="ArgumentException"><paramref name="pageIndexes"/>が空、または指定が不正な場合</exception>
        Task SavePdfPagesToImagesAsync(string filePath, IEnumerable<int> pageIndexes, string folderPath, bool saveAllPages, ResolutionMode mode = ResolutionMode.Default, double value = 0, IProgress<SaveProgressReport> progress = null, CancellationToken cancellationToken = default);
    }
}
