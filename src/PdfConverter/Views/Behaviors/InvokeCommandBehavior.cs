using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PdfConverter.Views.Behaviors
{
    /// <summary>
    /// 指定した <see cref="ICommand"/> を UI イベントで実行する Attached Behavior
    /// </summary>
    public static class InvokeCommandBehavior
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>フォーカス喪失時に実行するコマンド</summary>
        public static readonly DependencyProperty LostFocusCommandProperty =
            DependencyProperty.RegisterAttached(
                "LostFocusCommand",
                typeof(ICommand),
                typeof(InvokeCommandBehavior),
                new PropertyMetadata(null, OnLostFocusCommandChanged));


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>フォーカス喪失時に実行するコマンドを取得する</summary>
        /// <param name="element">対象の要素</param>
        /// <returns>フォーカス喪失時に実行するコマンド</returns>
        public static ICommand GetLostFocusCommand(DependencyObject element) =>
            (ICommand)element.GetValue(LostFocusCommandProperty);

        /// <summary>フォーカス喪失時に実行するコマンドを設定する</summary>
        /// <param name="element">対象の要素</param>
        /// <param name="value">フォーカス喪失時に実行するコマンド</param>
        public static void SetLostFocusCommand(DependencyObject element, ICommand value) =>
            element.SetValue(LostFocusCommandProperty, value);


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>フォーカス喪失時に実行するコマンドが変更されたときの処理</summary>
        /// <param name="dependencyObject">対象の要素</param>
        /// <param name="e">イベントの引数</param>
        private static void OnLostFocusCommandChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (!(dependencyObject is Control control))
            {
                return;
            }

            control.LostFocus -= OnLostFocus;

            if (e.NewValue is ICommand)
            {
                control.LostFocus += OnLostFocus;
            }
        }

        /// <summary>フォーカス喪失時のイベントハンドラ</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (!(sender is Control control))
            {
                return;
            }

            if (!(GetLostFocusCommand(control) is ICommand command))
            {
                return;
            }

            if (command.CanExecute(null))
            {
                command.Execute(null);
            }
        }
    }
}
