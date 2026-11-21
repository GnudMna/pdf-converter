using System;

namespace PdfConverter.Services
{
    /// <summary>
    /// PDF を開くためのパスワードが必要、または指定したパスワードが正しくないことを表す例外
    /// </summary>
    public sealed class PdfPasswordRequiredException : Exception
    {
        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// パスワードを指定したかどうかを指定して初期化する
        /// </summary>
        /// <param name="wasPasswordProvided">パスワードを指定して開こうとしたかどうか</param>
        public PdfPasswordRequiredException(bool wasPasswordProvided)
            : base(wasPasswordProvided ? "パスワードが正しくありません。" : "パスワードで保護された PDF です。")
        {
            WasPasswordProvided = wasPasswordProvided;
        }


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>パスワードを指定して開こうとしたかどうか (<c>true</c> の場合は指定したパスワードが正しくない)</summary>
        public bool WasPasswordProvided { get; }
    }
}
