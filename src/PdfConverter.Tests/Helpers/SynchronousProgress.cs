using System;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// 進捗を呼び出し元のスレッドで即座に通知する <see cref="IProgress{T}"/> 実装
    /// </summary>
    /// <remarks>
    /// <see cref="Progress{T}"/> は通知をスレッドプールへ非同期に送るため、
    /// 処理の完了直後に検証すると通知が届いておらずテストが不安定になる
    /// </remarks>
    /// <typeparam name="T">進捗値の型</typeparam>
    internal sealed class SynchronousProgress<T> : IProgress<T>
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>進捗通知時に呼び出す処理</summary>
        private readonly Action<T> _handler;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 進捗通知時に呼び出す処理を指定して初期化する
        /// </summary>
        /// <param name="handler">進捗通知時に呼び出す処理</param>
        public SynchronousProgress(Action<T> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public void Report(T value)
        {
            _handler(value);
        }
    }
}
