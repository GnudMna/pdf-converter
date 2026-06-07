using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using PdfConverter.Services;
using PdfConverter.ViewModels;

namespace PdfConverter.Views.Behaviors
{
    /// <summary>
    /// ウィンドウへのPDFファイルのドラッグ&amp;ドロップを<see cref="IMainWindowViewModel"/>に委譲する
    /// </summary>
    public static class FileDropBehavior
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>ドラッグ&amp;ドロップを管理するテーブル</summary>
        private static readonly ConditionalWeakTable<UIElement, HandlerSet> HandlerSets =
            new ConditionalWeakTable<UIElement, HandlerSet>();

        /// <summary>ドラッグ&amp;ドロップを有効にするかどうか</summary>
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(FileDropBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>ドラッグ&amp;ドロップを有効にするかどうかを取得する</summary>
        /// <param name="element">対象の要素</param>
        /// <returns>true: 有効 / false: 無効</returns>
        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        /// <summary>ドラッグ&amp;ドロップを有効にするかどうかを設定する</summary>
        /// <param name="element">対象の要素</param>
        /// <param name="value">true: 有効 / false: 無効</param>
        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// ドラッグ&amp;ドロップを有効にするかどうかが変更されたときの処理
        /// </summary>
        /// <param name="dependencyObject">対象の要素</param>
        /// <param name="e">イベントの引数</param>
        private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (!(dependencyObject is UIElement element))
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                var handlers = new HandlerSet();
                HandlerSets.Add(element, handlers);
                element.AllowDrop = true;
                element.AddHandler(UIElement.DragEnterEvent, handlers.DragEnter, handledEventsToo: true);
                element.AddHandler(UIElement.DragOverEvent, handlers.DragOver, handledEventsToo: true);
                element.AddHandler(UIElement.DragLeaveEvent, handlers.DragLeave, handledEventsToo: true);
                element.AddHandler(UIElement.DropEvent, handlers.Drop, handledEventsToo: true);
                return;
            }

            if (HandlerSets.TryGetValue(element, out HandlerSet existing))
            {
                element.RemoveHandler(UIElement.DragEnterEvent, existing.DragEnter);
                element.RemoveHandler(UIElement.DragOverEvent, existing.DragOver);
                element.RemoveHandler(UIElement.DragLeaveEvent, existing.DragLeave);
                element.RemoveHandler(UIElement.DropEvent, existing.Drop);
                element.AllowDrop = false;
                HandlerSets.Remove(element);
            }
        }

        /// <summary>ドラッグエンターイベントのハンドラ</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void OnDragEnter(object sender, DragEventArgs e) => UpdateDragFeedback(sender, e);

        /// <summary>ドラッグオーバーイベントのハンドラ</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void OnDragOver(object sender, DragEventArgs e) => UpdateDragFeedback(sender, e);

        /// <summary>ドラッグリーブイベントのハンドラ</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void OnDragLeave(object sender, DragEventArgs e)
        {
            if (TryGetViewModel(sender, out IMainWindowViewModel viewModel))
            {
                viewModel.IsDropOverlayVisible = false;
            }
        }

        /// <summary>ドロップイベントのハンドラ</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void OnDrop(object sender, DragEventArgs e)
        {
            if (!TryGetViewModel(sender, out IMainWindowViewModel viewModel))
            {
                return;
            }

            viewModel.IsDropOverlayVisible = false;

            if (viewModel.IsBusy)
            {
                e.Handled = true;
                return;
            }

            if (DocumentFileHelper.TryGetFirstSupportedPath(e.Data, out string filePath))
            {
                viewModel.HandleDroppedDocument(filePath);
            }

            e.Handled = true;
        }

        /// <summary>ドラッグフィードバックを更新する</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void UpdateDragFeedback(object sender, DragEventArgs e)
        {
            if (!TryGetViewModel(sender, out IMainWindowViewModel viewModel))
            {
                return;
            }

            if (viewModel.IsBusy)
            {
                viewModel.IsDropOverlayVisible = false;
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            bool acceptable = DocumentFileHelper.ContainsSupportedDocument(e.Data);
            viewModel.IsDropOverlayVisible = acceptable;
            e.Effects = acceptable ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        /// <summary>ViewModelを取得する</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="viewModel">取得したViewModel</param>
        /// <returns>true: 取得できた / false: 取得できなかった</returns>
        private static bool TryGetViewModel(object sender, out IMainWindowViewModel viewModel)
        {
            viewModel = null;

            if (sender is FrameworkElement element)
            {
                viewModel = element.DataContext as IMainWindowViewModel;
            }

            return viewModel != null;
        }

        /// <summary>要素に登録するイベントハンドラ一式</summary>
        private sealed class HandlerSet
        {
            public DragEventHandler DragEnter { get; }
            public DragEventHandler DragOver { get; }
            public DragEventHandler DragLeave { get; }
            public DragEventHandler Drop { get; }

            public HandlerSet()
            {
                DragEnter = OnDragEnter;
                DragOver = OnDragOver;
                DragLeave = OnDragLeave;
                Drop = OnDrop;
            }
        }
    }
}
