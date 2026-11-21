using Xunit;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// 静的な状態 (ログ出力先・一時ディレクトリのルートなど) を書き換えるテストを集めるコレクション
    /// </summary>
    /// <remarks>
    /// 並列実行を無効にしたコレクションは、他のテストがすべて終わった後に単独で実行される<br/>
    /// 静的な状態の書き換えが、並列実行中の他のテストクラスへ影響しないようにする
    /// </remarks>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class GlobalStateTestCollection
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>コレクション名</summary>
        public const string Name = "GlobalState";
    }
}
