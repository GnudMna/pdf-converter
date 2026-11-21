using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// WPF テスト用の STA スレッド実行ヘルパー
    /// </summary>
    internal static class StaTestHelper
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 指定したアクションを STA スレッド上で実行する
        /// </summary>
        public static void Run(Action action)
        {
            Run(() =>
            {
                action();
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// 指定した非同期アクションを STA スレッド上で実行する
        /// </summary>
        public static void Run(Func<Task> action)
        {
            Exception captured = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    captured = ex;
                }
                finally
                {
                    // 終了していない Dispatcher が残ると、WPF がプロセス終了時にそのスレッドへの呼び出しを 1 件約 300 ms 待つ
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (captured != null)
            {
                throw captured;
            }
        }
    }
}
