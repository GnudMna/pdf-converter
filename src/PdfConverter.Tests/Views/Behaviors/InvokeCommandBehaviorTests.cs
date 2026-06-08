using System.Windows;
using System.Windows.Controls;
using FluentAssertions;
using PdfConverter.Commands;
using PdfConverter.Tests.Helpers;
using PdfConverter.Views.Behaviors;
using Xunit;

namespace PdfConverter.Tests.Views.Behaviors
{
    /// <summary>
    /// <see cref="InvokeCommandBehavior"/> のフォーカス喪失時コマンド実行を検証する
    /// </summary>
    public class InvokeCommandBehaviorTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// フォーカス喪失時にコマンドが実行されることを検証する
        /// </summary>
        [Fact]
        public void LostFocus_ExecutesCommand()
        {
            bool executed = false;

            StaTestHelper.Run(() =>
            {
                var command = new RelayCommand(() => executed = true);
                var textBox = new TextBox();
                InvokeCommandBehavior.SetLostFocusCommand(textBox, command);
                textBox.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
            });

            executed.Should().BeTrue();
        }

        /// <summary>
        /// コマンドが実行不可のときはフォーカス喪失でも実行されないことを検証する
        /// </summary>
        [Fact]
        public void LostFocus_WhenCannotExecute_DoesNotExecuteCommand()
        {
            bool executed = false;

            StaTestHelper.Run(() =>
            {
                var command = new RelayCommand(() => executed = true, () => false);
                var textBox = new TextBox();
                InvokeCommandBehavior.SetLostFocusCommand(textBox, command);
                textBox.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
            });

            executed.Should().BeFalse();
        }

        /// <summary>
        /// コマンドを解除するとフォーカス喪失時に実行されないことを検証する
        /// </summary>
        [Fact]
        public void ClearLostFocusCommand_DoesNotExecute()
        {
            bool executed = false;

            StaTestHelper.Run(() =>
            {
                var command = new RelayCommand(() => executed = true);
                var textBox = new TextBox();
                InvokeCommandBehavior.SetLostFocusCommand(textBox, command);
                InvokeCommandBehavior.SetLostFocusCommand(textBox, null);
                textBox.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
            });

            executed.Should().BeFalse();
        }
    }
}
