using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using PdfConverter.Tests.Helpers;
using PdfConverter.Views.Behaviors;
using Xunit;

namespace PdfConverter.Tests.Views.Behaviors
{
    /// <summary>
    /// <see cref="TextBoxBehavior"/> の Enter キー押下時のバインディング反映を検証する
    /// </summary>
    public class TextBoxBehaviorTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 入力中はバインディングのソースへ反映されないことを検証する
        /// </summary>
        [Fact]
        public void TextInput_BeforeEnter_DoesNotUpdateSource()
        {
            string sourceText = null;

            StaTestHelper.Run(() =>
            {
                var source = new TextSource { Text = "before" };
                RunWithBoundTextBox(source, textBox =>
                {
                    textBox.Text = "after";
                });
                sourceText = source.Text;
            });

            Assert.Equal("before", sourceText);
        }

        /// <summary>
        /// Enter キー押下でバインディングのソースへ反映されることを検証する
        /// </summary>
        [Fact]
        public void EnterKey_UpdatesSource()
        {
            string sourceText = null;
            bool handled = false;

            StaTestHelper.Run(() =>
            {
                var source = new TextSource { Text = "before" };
                RunWithBoundTextBox(source, textBox =>
                {
                    textBox.Text = "after";
                    handled = RaiseKeyDown(textBox, Key.Enter);
                });
                sourceText = source.Text;
            });

            Assert.Equal("after", sourceText);
            Assert.True(handled);
        }

        /// <summary>
        /// Enter 以外のキー押下ではバインディングのソースへ反映されないことを検証する
        /// </summary>
        [Fact]
        public void OtherKey_DoesNotUpdateSource()
        {
            string sourceText = null;

            StaTestHelper.Run(() =>
            {
                var source = new TextSource { Text = "before" };
                RunWithBoundTextBox(source, textBox =>
                {
                    textBox.Text = "after";
                    RaiseKeyDown(textBox, Key.A);
                });
                sourceText = source.Text;
            });

            Assert.Equal("before", sourceText);
        }

        /// <summary>
        /// 反映を無効に戻すと Enter キー押下でもバインディングのソースへ反映されないことを検証する
        /// </summary>
        [Fact]
        public void EnterKey_AfterDisabled_DoesNotUpdateSource()
        {
            string sourceText = null;

            StaTestHelper.Run(() =>
            {
                var source = new TextSource { Text = "before" };
                RunWithBoundTextBox(source, textBox =>
                {
                    TextBoxBehavior.SetUpdateSourceOnEnter(textBox, false);
                    textBox.Text = "after";
                    RaiseKeyDown(textBox, Key.Enter);
                });
                sourceText = source.Text;
            });

            Assert.Equal("before", sourceText);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>明示的に反映するバインディングを持つ TextBox をウィンドウに表示して操作する</summary>
        /// <param name="source">バインディングのソース</param>
        /// <param name="action">TextBox に対する操作</param>
        private static void RunWithBoundTextBox(TextSource source, System.Action<TextBox> action)
        {
            var window = new Window { Width = 100, Height = 100 };
            var textBox = new TextBox();
            textBox.SetBinding(TextBox.TextProperty, new Binding(nameof(TextSource.Text))
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
            });
            TextBoxBehavior.SetUpdateSourceOnEnter(textBox, true);
            window.Content = textBox;
            window.Show();
            textBox.Focus();
            try
            {
                action(textBox);
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>TextBox にキー押下イベントを送る</summary>
        /// <param name="textBox">対象の TextBox</param>
        /// <param name="key">押下するキー</param>
        /// <returns>イベントが処理済みになったかどうか</returns>
        private static bool RaiseKeyDown(TextBox textBox, Key key)
        {
            var args = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(textBox),
                0,
                key)
            {
                RoutedEvent = UIElement.PreviewKeyDownEvent,
            };
            textBox.RaiseEvent(args);
            return args.Handled;
        }


        /********************************************************************************/
        /*                                   ネスト型                                   */
        /********************************************************************************/
        /// <summary>バインディングのソース</summary>
        private sealed class TextSource
        {
            /// <summary>バインドする文字列</summary>
            public string Text { get; set; }
        }
    }
}
