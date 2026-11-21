using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace PdfConverter.Views.Behaviors
{
    /// <summary>
    /// <see cref="TextBox"/> の入力内容の反映方法を拡張する Attached Behavior
    /// </summary>
    public static class TextBoxBehavior
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>Enter キー押下時に <see cref="TextBox.Text"/> のバインディングをソースへ反映するかどうか</summary>
        public static readonly DependencyProperty UpdateSourceOnEnterProperty =
            DependencyProperty.RegisterAttached(
                "UpdateSourceOnEnter",
                typeof(bool),
                typeof(TextBoxBehavior),
                new PropertyMetadata(false, OnUpdateSourceOnEnterChanged));


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>Enter キー押下時にバインディングをソースへ反映するかどうかを取得する</summary>
        /// <param name="element">対象の要素</param>
        /// <returns>Enter キー押下時にバインディングをソースへ反映するかどうか</returns>
        public static bool GetUpdateSourceOnEnter(DependencyObject element) =>
            (bool)element.GetValue(UpdateSourceOnEnterProperty);

        /// <summary>Enter キー押下時にバインディングをソースへ反映するかどうかを設定する</summary>
        /// <param name="element">対象の要素</param>
        /// <param name="value">Enter キー押下時にバインディングをソースへ反映するかどうか</param>
        public static void SetUpdateSourceOnEnter(DependencyObject element, bool value) =>
            element.SetValue(UpdateSourceOnEnterProperty, value);


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>Enter キー押下時の反映有無が変更されたときの処理</summary>
        /// <param name="dependencyObject">対象の要素</param>
        /// <param name="e">イベントの引数</param>
        private static void OnUpdateSourceOnEnterChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (!(dependencyObject is TextBox textBox))
            {
                return;
            }

            textBox.PreviewKeyDown -= OnPreviewKeyDown;

            if (e.NewValue is true)
            {
                textBox.PreviewKeyDown += OnPreviewKeyDown;
            }
        }

        /// <summary>Enter キー押下時のイベントハンドラ</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || !(sender is TextBox textBox))
            {
                return;
            }

            BindingExpression? binding = textBox.GetBindingExpression(TextBox.TextProperty);
            if (binding is null)
            {
                return;
            }

            binding.UpdateSource();
            e.Handled = true;
        }
    }
}
