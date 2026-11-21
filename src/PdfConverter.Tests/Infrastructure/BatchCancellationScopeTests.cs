using System;
using PdfConverter.Infrastructure;
using Xunit;

namespace PdfConverter.Tests.Infrastructure
{
    /// <summary>
    /// <see cref="BatchCancellationScope"/> のキャンセル要求の扱いを検証する
    /// </summary>
    public class BatchCancellationScopeTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 作成直後はトークンが取り消されていないことを検証する
        /// </summary>
        [Fact]
        public void Token_AfterCreation_IsNotCanceled()
        {
            using var scope = new BatchCancellationScope();

            Assert.False(scope.Token.IsCancellationRequested);
        }

        /// <summary>
        /// キャンセルを要求するとトークンが取り消されることを検証する
        /// </summary>
        [Fact]
        public void RequestCancel_WhileActive_CancelsToken()
        {
            using var scope = new BatchCancellationScope();

            bool requested = scope.RequestCancel();

            Assert.True(requested);
            Assert.True(scope.Token.IsCancellationRequested);
        }

        /// <summary>
        /// 破棄後に届いたキャンセル要求は例外を出さずに無視することを検証する
        /// </summary>
        [Fact]
        public void RequestCancel_AfterDispose_IsIgnored()
        {
            var scope = new BatchCancellationScope();
            scope.Dispose();

            bool requested = scope.RequestCancel();

            Assert.False(requested);
        }

        /// <summary>
        /// 複数回破棄しても例外が発生しないことを検証する
        /// </summary>
        [Fact]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var scope = new BatchCancellationScope();

            scope.Dispose();
            Exception exception = Record.Exception(scope.Dispose);

            Assert.Null(exception);
        }
    }
}
