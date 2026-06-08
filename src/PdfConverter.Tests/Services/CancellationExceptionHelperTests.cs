using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="CancellationExceptionHelper"/> のキャンセル例外判定を検証する
    /// </summary>
    public class CancellationExceptionHelperTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// OperationCanceledException がキャンセル例外として判定されることを検証する
        /// </summary>
        [Fact]
        public void IsOrContainsCancellation_OperationCanceledException_ReturnsTrue()
        {
            CancellationExceptionHelper.IsOrContainsCancellation(new OperationCanceledException())
                .Should().BeTrue();
        }

        /// <summary>
        /// キャンセル由来の AggregateException がキャンセル例外として判定されることを検証する
        /// </summary>
        [Fact]
        public void IsOrContainsCancellation_CancellationAggregate_ReturnsTrue()
        {
            var aggregate = new AggregateException(
                new OperationCanceledException(),
                new TaskCanceledException());

            CancellationExceptionHelper.IsOrContainsCancellation(aggregate)
                .Should().BeTrue();
        }

        /// <summary>
        /// 通常の例外はキャンセル例外として判定されないことを検証する
        /// </summary>
        [Fact]
        public void IsOrContainsCancellation_OtherException_ReturnsFalse()
        {
            CancellationExceptionHelper.IsOrContainsCancellation(new InvalidOperationException())
                .Should().BeFalse();
        }

        /// <summary>
        /// 混在した AggregateException はキャンセル例外として判定されないことを検証する
        /// </summary>
        [Fact]
        public void IsOrContainsCancellation_MixedAggregate_ReturnsFalse()
        {
            var aggregate = new AggregateException(
                new OperationCanceledException(),
                new InvalidOperationException());

            CancellationExceptionHelper.IsOrContainsCancellation(aggregate)
                .Should().BeFalse();
        }
    }
}
