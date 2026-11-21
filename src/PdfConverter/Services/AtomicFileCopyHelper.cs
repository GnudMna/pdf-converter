using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PdfConverter.Services
{
    /// <summary>
    /// ファイルを途中経過が見えない形でコピーする
    /// </summary>
    /// <remarks>
    /// 保存先と同じフォルダーの一時ファイルへ書き出してから、同一ボリューム内のリネームで反映する<br/>
    /// 書き出しの失敗・キャンセルでは既存の保存先ファイルに触れず、一時ファイルも残さない
    /// </remarks>
    internal static class AtomicFileCopyHelper
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>コピー時のバッファーサイズ (バイト)</summary>
        private const int BufferSize = 81920;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// ファイルをコピーし、完了した時点で保存先へ反映する
        /// </summary>
        /// <param name="sourcePath">コピー元ファイルのパス</param>
        /// <param name="destinationPath">コピー先ファイルのパス (親フォルダーが存在すること)</param>
        /// <param name="overwrite"><c>true</c> の場合は既存のコピー先ファイルを置き換える</param>
        /// <param name="cancellationToken">キャンセルトークン (コピー中と反映の直前に確認する)</param>
        /// <returns>非同期操作のタスク</returns>
        /// <exception cref="IOException"><paramref name="overwrite"/> が <c>false</c> でコピー先が既に存在する場合など</exception>
        /// <exception cref="OperationCanceledException">キャンセルされた場合</exception>
        public static async Task CopyAsync(
            string sourcePath,
            string destinationPath,
            bool overwrite,
            CancellationToken cancellationToken)
        {
            string fullDestinationPath = Path.GetFullPath(destinationPath);
            string destinationDirectory = Path.GetDirectoryName(fullDestinationPath)
                ?? throw new ArgumentException("コピー先のフォルダーを特定できません。", nameof(destinationPath));
            string tempPath = Path.Combine(
                destinationDirectory,
                $".{Path.GetFileName(fullDestinationPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                using (var source = new FileStream(
                    sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
                using (var temp = new FileStream(
                    tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous))
                {
                    await source.CopyToAsync(temp, BufferSize, cancellationToken).ConfigureAwait(false);
                    await temp.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                // 反映後は取り消せないため、書き出し後に要求されたキャンセルもここで受け付ける
                cancellationToken.ThrowIfCancellationRequested();

                File.Move(tempPath, fullDestinationPath, overwrite);
            }
            finally
            {
                // 反映に成功した場合は一時ファイルが存在せず、何もしない
                PdfConverterTempDirectory.TryDeleteFile(tempPath);
            }
        }
    }
}
