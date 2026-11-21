using System;

namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// 例外メッセージをユーザー向け表示用に整形する
    /// </summary>
    public static class ExceptionFormattingHelper
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// ユーザー向けに表示する例外メッセージを返す<br/>
        /// 根本原因のメッセージが異なる場合は内部例外のメッセージを括弧付きで付加する
        /// </summary>
        /// <param name="exception">対象の例外</param>
        /// <returns>表示用メッセージ</returns>
        public static string GetUserMessage(Exception? exception)
        {
            if (exception == null)
            {
                return string.Empty;
            }

            Exception root = Unwrap(exception);
            string message = root.Message ?? string.Empty;

            Exception? inner = root.InnerException;
            while (inner != null)
            {
                string innerMessage = inner.Message;
                if (!string.IsNullOrWhiteSpace(innerMessage)
                    && !string.Equals(message, innerMessage, StringComparison.Ordinal)
                    && message.IndexOf(innerMessage, StringComparison.Ordinal) < 0)
                {
                    message = $"{message} ({innerMessage})";
                    break;
                }

                inner = inner.InnerException;
            }

            return message;
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>AggregateException を単一の根本原因例外に展開する</summary>
        /// <param name="exception">展開する例外</param>
        /// <returns>展開後の例外</returns>
        private static Exception Unwrap(Exception exception)
        {
            if (exception is AggregateException aggregate)
            {
                return aggregate.Flatten().InnerException ?? exception;
            }

            return exception;
        }
    }
}
