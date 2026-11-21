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
    public interface IPdfConversionService
    {
        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>
        /// パスワードで保護された PDF を開くためのパスワードを設定する
        /// </summary>
        /// <remarks>
        /// 設定は 1 ファイル分だけ保持し、別のファイルに設定すると置き換える<br/>
        /// 以降、<paramref name="filePath"/> の PDF を開くときにこのパスワードを使う<br/>
        /// パスワードが必要な PDF を開こうとしたとき、またはパスワードが正しくないときは <see cref="PdfPasswordRequiredException"/> を送出する
        /// </remarks>
        /// <param name="filePath">PDF ファイルの絶対パス</param>
        /// <param name="password">パスワード (設定を解除する場合は <c>null</c>)</param>
        void SetPdfPassword(string filePath, string? password);

        /// <summary>
        /// PDF ファイルの総ページ数を非同期で取得する
        /// </summary>
        /// <param name="filePath">PDF ファイルの絶対パス</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>PDF の総ページ数</returns>
        /// <exception cref="PdfPasswordRequiredException">パスワードが必要、または設定したパスワードが正しくない場合</exception>
        Task<int> GetPdfPageCountAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// PDF の指定ページをビットマップに変換して返す
        /// </summary>
        /// <param name="filePath">PDF ファイルの絶対パス</param>
        /// <param name="pageIndex">変換対象のページインデックス (0 始まり)</param>
        /// <param name="mode">解像度の指定方法</param>
        /// <param name="value"><paramref name="mode"/> に対応する数値 (幅・高さ・DPI)</param>
        /// <param name="preserveTransparency"><c>true</c> の場合は透明度を保持する</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>変換されたページのビットマップ</returns>
        Task<BitmapSource> ConvertPdfPageToImageAsync(string filePath, int pageIndex, ResolutionMode mode = ResolutionMode.Default, double value = 0, bool preserveTransparency = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// PDF の指定ページを、出力解像度を上限として指定サイズに収まるよう縮小したプレビューとして描画する
        /// </summary>
        /// <remarks>
        /// 出力解像度での描画サイズが上限を超える場合は、縮小して描画できる場合でも保存と同じく失敗させる
        /// </remarks>
        /// <param name="filePath">PDF ファイルの絶対パス</param>
        /// <param name="pageIndex">変換対象のページインデックス (0 始まり)</param>
        /// <param name="mode">解像度の指定方法</param>
        /// <param name="value"><paramref name="mode"/> に対応する数値 (幅・高さ・DPI)</param>
        /// <param name="preserveTransparency"><c>true</c> の場合は透明度を保持する</param>
        /// <param name="maxPixelWidth">プレビュー画像の最大幅 (px、1 以上)</param>
        /// <param name="maxPixelHeight">プレビュー画像の最大高さ (px、1 以上)</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>プレビュー画像と出力解像度での描画サイズ</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxPixelWidth"/> または <paramref name="maxPixelHeight"/> が 1 未満の場合</exception>
        Task<PdfPagePreview> RenderPdfPagePreviewAsync(string filePath, int pageIndex, ResolutionMode mode, double value, bool preserveTransparency, int maxPixelWidth, int maxPixelHeight, CancellationToken cancellationToken = default);

        /// <summary>
        /// 指定したページ群を画像ファイルとしてフォルダーへ一括保存する
        /// </summary>
        /// <remarks>
        /// CPU コア数に応じた並列処理で保存し、進捗を <paramref name="progress"/> で報告する<br/>
        /// 画像は保存先フォルダー内のステージングディレクトリへ書き出したうえで、全ページ完了後にリネームで保存先へ反映する<br/>
        /// キャンセルまたは失敗時は保存先を変更せず、部分出力を残さない
        /// </remarks>
        /// <param name="filePath">PDF ファイルの絶対パス</param>
        /// <param name="pageIndexes">保存対象のページインデックス一覧 (0 始まり)<br/><paramref name="saveAllPages"/> が <c>true</c> の場合は無視される</param>
        /// <param name="folderPath">保存先フォルダーの絶対パス</param>
        /// <param name="saveAllPages"><c>true</c> の場合は全ページを保存する</param>
        /// <param name="mode">解像度の指定方法</param>
        /// <param name="value"><paramref name="mode"/> に対応する数値 (幅・高さ・DPI)</param>
        /// <param name="format">出力画像形式</param>
        /// <param name="preserveTransparency"><c>true</c> の場合は透明度を保持する</param>
        /// <param name="progress">進捗通知用コールバック<br/><c>null</c> の場合は通知しない</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>非同期操作の完了を表すタスク</returns>
        /// <exception cref="OperationCanceledException">キャンセル要求が発生した場合</exception>
        Task SavePdfPagesToImagesAsync(string filePath, IEnumerable<int>? pageIndexes, string folderPath, bool saveAllPages, ResolutionMode mode = ResolutionMode.Default, double value = 0, OutputImageFormat format = OutputImageFormat.Png, bool preserveTransparency = true, IProgress<SaveProgressReport>? progress = null, CancellationToken cancellationToken = default);
    }
}
