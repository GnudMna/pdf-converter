using System;
using System.Threading;

namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// バッチモードの実行中に Ctrl+C / Ctrl+Break を受け取り、処理のキャンセルへ変換する<br/>
    /// 既定の動作ではプロセスが即座に終了し、Word / LibreOffice のプロセスや一時ファイルの後片付けが行われないため、
    /// キャンセルとして通知して呼び出し元に後片付けをさせる
    /// </summary>
    internal sealed class BatchCancellationScope : IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>キャンセル要求の通知元</summary>
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        /// <summary>破棄後に届いたキャンセル要求を無視するための排他用オブジェクト</summary>
        private readonly object _sync = new object();

        /// <summary>破棄済みかどうか</summary>
        private bool _disposed;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// Ctrl+C / Ctrl+Break の受信を開始する
        /// </summary>
        public BatchCancellationScope()
        {
            // GUI サブシステムのため、コンソールへ接続する前に登録したハンドラーには Ctrl+C が届かない
            BatchConsoleHelper.TryAttachParentConsole();
            Console.CancelKeyPress += OnCancelKeyPress;
        }


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>Ctrl+C / Ctrl+Break を受け取ると取り消し済みになるトークン</summary>
        public CancellationToken Token => _cancellationTokenSource.Token;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// キャンセルを要求する (Ctrl+C / Ctrl+Break の受信時に呼ばれる)
        /// </summary>
        /// <returns>true: キャンセルを要求した / false: 破棄済みのため無視した</returns>
        internal bool RequestCancel()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return false;
                }

                _cancellationTokenSource.Cancel();
                return true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Console.CancelKeyPress -= OnCancelKeyPress;

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _cancellationTokenSource.Dispose();
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// Ctrl+C / Ctrl+Break の受信時に、プロセスの終了を止めてキャンセルを要求する
        /// </summary>
        /// <param name="sender">イベントの発生元</param>
        /// <param name="e">イベントデータ</param>
        private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            // 既定の動作 (プロセスの即時終了) を止め、呼び出し元の後片付けを経て終了させる
            e.Cancel = true;

            if (RequestCancel())
            {
                BatchConsoleHelper.WriteErrorLine("中断しています。後片付けが終わるまでお待ちください...");
            }
        }
    }
}
