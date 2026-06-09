using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        /// <summary>
        /// Enter キー押下時にコマンドが実行されることを検証する
        /// </summary>
        [Fact]
        public void EnterKey_ExecutesCommand()
        {
            bool executed = false;

            StaTestHelper.Run(() =>
            {
                var command = new RelayCommand(() => executed = true);
                var window = new Window { Width = 100, Height = 100 };
                var textBox = new TextBox();
                window.Content = textBox;
                InvokeCommandBehavior.SetEnterKeyCommand(textBox, command);
                window.Show();
                textBox.Focus();
                textBox.RaiseEvent(new KeyEventArgs(
                    Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(textBox),
                    0,
                    Key.Enter)
                {
                    RoutedEvent = UIElement.PreviewKeyDownEvent,
                });
                window.Close();
            });

            executed.Should().BeTrue();
        }

        /// <summary>
        /// コマンドが実行不可のときは Enter キー押下でも実行されないことを検証する
        /// </summary>
        [Fact]
        public void EnterKey_WhenCannotExecute_DoesNotExecuteCommand()
        {
            bool executed = false;

            StaTestHelper.Run(() =>
            {
                var command = new RelayCommand(() => executed = true, () => false);
                var window = new Window { Width = 100, Height = 100 };
                var textBox = new TextBox();
                window.Content = textBox;
                InvokeCommandBehavior.SetEnterKeyCommand(textBox, command);
                window.Show();
                textBox.Focus();
                textBox.RaiseEvent(new KeyEventArgs(
                    Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(textBox),
                    0,
                    Key.Enter)
                {
                    RoutedEvent = UIElement.PreviewKeyDownEvent,
                });
                window.Close();
            });

            executed.Should().BeFalse();
        }
    }
}
