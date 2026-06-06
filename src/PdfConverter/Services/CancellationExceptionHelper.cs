using System;
using System.Linq;

namespace PdfConverter.Services
{
    /// <summary>
    /// 協調的キャンセルに関する例外を判定するヘルパー
    /// </summary>
    internal static class CancellationExceptionHelper
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 指定した例外がキャンセル由来かどうかを判定する
        /// </summary>
        /// <param name="exception">判定する例外</param>
        /// <returns>true: キャンセル由来, false: キャンセル由来ではない</returns>
        public static bool IsOrContainsCancellation(Exception exception)
        {
            if (exception == null)
            {
                return false;
            }

            if (exception is OperationCanceledException)
            {
                return true;
            }

            if (exception is AggregateException aggregate)
            {
                return aggregate.Flatten().InnerExceptions.All(e => e is OperationCanceledException);
            }

            return false;
        }
    }
}
