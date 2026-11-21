using System;
using System.Threading;
using System.Threading.Tasks;

namespace PdfConverter.Services
{
    /// <summary>
    /// COM コンポーネント向けに STA スレッド上で処理を実行する
    /// </summary>
    internal static class StaTaskRunner
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 指定した処理を STA スレッド上で実行する
        /// </summary>
        /// <typeparam name="T">戻り値の型</typeparam>
        /// <param name="func">実行する処理</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <param name="cancelCleanup">キャンセル時に呼び出すクリーンアップ処理</param>
        /// <returns>処理結果</returns>
        public static Task<T> RunAsync<T>(
            Func<CancellationToken, T> func,
            CancellationToken cancellationToken = default,
            Action cancelCleanup = null)
        {
            var tcs = new TaskCompletionSource<T>();
            CancellationTokenRegistration? registration = null;

            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() =>
                {
                    try
                    {
                        cancelCleanup?.Invoke();
                    }
                    catch
                    {
                    }

                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            var thread = new Thread(() =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.TrySetCanceled(cancellationToken);
                        return;
                    }

                    tcs.TrySetResult(func(cancellationToken));
                }
                catch (OperationCanceledException)
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    registration?.Dispose();
                }
            })
            {
                IsBackground = true,
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return tcs.Task;
        }
    }
}
