using System;
using System.Threading;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// WPF テスト用の STA スレッド実行ヘルパー
    /// </summary>
    internal static class StaTestHelper
    {
        /// <summary>
        /// 指定したアクションを STA スレッド上で実行する
        /// </summary>
        public static void Run(Action action)
        {
            Exception captured = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    captured = ex;
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
