using System;
using System.Threading;
using System.Threading.Tasks;

namespace PdfConverter.Services
{
    /// <summary>
    /// COMコンポーネント向けにSTAスレッド上で処理を実行する
    /// </summary>
    internal static class StaTaskRunner
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 指定した処理をSTAスレッド上で実行する
        /// </summary>
        /// <typeparam name="T">戻り値の型</typeparam>
        /// <param name="func">実行する処理</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>処理結果</returns>
        public static Task<T> RunAsync<T>(Func<CancellationToken, T> func, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<T>();

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
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
                catch (OperationCanceledException ex)
                {
                    tcs.TrySetException(ex);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
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
