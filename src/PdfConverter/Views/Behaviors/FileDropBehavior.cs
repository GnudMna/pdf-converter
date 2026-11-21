using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.ViewModels;

namespace PdfConverter.Views.Behaviors
{
    /// <summary>
    /// 要素への PDF / Word ファイルのドラッグ&amp;ドロップを <see cref="IMainWindowViewModel"/> に委譲する
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

            if (HandlerSets.TryGetValue(element, out HandlerSet? existing))
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
        private static void OnDragEnter(object sender, DragEventArgs e)
        {
            UpdateDragFeedback(sender, e);

            // ドロップ不可のカーソルではドロップ自体が届かないため、ドラッグの進入時に理由を伝える
            if (TryGetViewModel(sender, out IMainWindowViewModel? viewModel))
            {
                NotifyIfRejectedWhileBusy(viewModel, e.Data);
            }
        }

        /// <summary>ドラッグオーバーイベントのハンドラ</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void OnDragOver(object sender, DragEventArgs e) => UpdateDragFeedback(sender, e);

        /// <summary>ドラッグリーブイベントのハンドラ</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void OnDragLeave(object sender, DragEventArgs e)
        {
            if (TryGetViewModel(sender, out IMainWindowViewModel? viewModel))
            {
                viewModel.IsDropOverlayVisible = false;
            }
        }

        /// <summary>ドロップイベントのハンドラ</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void OnDrop(object sender, DragEventArgs e)
        {
            if (TryGetViewModel(sender, out IMainWindowViewModel? viewModel))
            {
                TryProcessDrop(viewModel, e.Data, () => CommitFocusedTextBox((DependencyObject)sender));
            }

            e.Handled = true;
        }

        /// <summary>ドラッグフィードバックを更新する</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void UpdateDragFeedback(object sender, DragEventArgs e)
        {
            if (!TryGetViewModel(sender, out IMainWindowViewModel? viewModel))
            {
                return;
            }

            e.Effects = EvaluateDragEffects(viewModel, e.Data, out bool showOverlay);
            viewModel.IsDropOverlayVisible = showOverlay;
            e.Handled = true;
        }

        /// <summary>ドラッグ中のデータからドロップ効果とオーバーレイ表示を判定する</summary>
        /// <param name="viewModel">メインウィンドウ ViewModel</param>
        /// <param name="data">ドラッグ中のデータ</param>
        /// <param name="showOverlay">オーバーレイを表示するかどうか</param>
        /// <returns>ドロップ効果</returns>
        internal static DragDropEffects EvaluateDragEffects(
            IMainWindowViewModel viewModel,
            IDataObject data,
            out bool showOverlay)
        {
            if (viewModel.IsBusy)
            {
                showOverlay = false;
                return DragDropEffects.None;
            }

            bool acceptable = DocumentFileHelper.ContainsSupportedDocument(data);
            showOverlay = acceptable;
            return acceptable ? DragDropEffects.Copy : DragDropEffects.None;
        }

        /// <summary>ドロップされたデータを処理する</summary>
        /// <param name="viewModel">メインウィンドウ ViewModel</param>
        /// <param name="data">ドロップされたデータ</param>
        /// <param name="commitPendingInput">ドキュメントを受け付けたとき、読み込みの前に入力途中の値を反映する処理</param>
        /// <returns>true: ドキュメントを受け付けた / false: 受け付けなかった</returns>
        internal static bool TryProcessDrop(IMainWindowViewModel viewModel, IDataObject data, Action? commitPendingInput = null)
        {
            viewModel.IsDropOverlayVisible = false;

            if (viewModel.IsBusy)
            {
                NotifyIfRejectedWhileBusy(viewModel, data);
                return false;
            }

            if (DocumentFileHelper.TryGetFirstSupportedPath(data, out string? filePath))
            {
                // フォーカスを残したままドロップされた入力欄の値を、読み込みに使う設定へ反映する
                if (commitPendingInput != null)
                {
                    viewModel.CommitPendingInput(commitPendingInput);
                }

                viewModel.HandleDroppedDocument(filePath);

                int droppedCount = DocumentFileHelper.GetDroppedFileCount(data);
                if (droppedCount > 1)
                {
                    viewModel.NotifyMultipleFilesDropped(droppedCount, filePath);
                }

                return true;
            }

            return false;
        }

        /// <summary>処理中に対応ドキュメントが来たとき、読み込めない理由をステータスバーへ表示する</summary>
        /// <param name="viewModel">メインウィンドウ ViewModel</param>
        /// <param name="data">ドラッグ・ドロップされたデータ</param>
        internal static void NotifyIfRejectedWhileBusy(IMainWindowViewModel viewModel, IDataObject data)
        {
            if (viewModel.IsBusy && DocumentFileHelper.ContainsSupportedDocument(data))
            {
                viewModel.SetStatus(DocumentFileHelper.BusyLoadRejectedMessage, StatusKind.Warning);
            }
        }

        /// <summary>フォーカスのある TextBox に入力途中の値があれば、バインディングのソースへ反映する</summary>
        /// <remarks>
        /// ドラッグ中はウィンドウが非アクティブでキーボードフォーカスを持たないため、論理フォーカスから対象を探す
        /// </remarks>
        /// <param name="element">ドロップ先の要素</param>
        private static void CommitFocusedTextBox(DependencyObject element)
        {
            DependencyObject? focusScope = FocusManager.GetFocusScope(element);
            if (focusScope == null
                || !(FocusManager.GetFocusedElement(focusScope) is TextBox textBox)
                || !(textBox.GetBindingExpression(TextBox.TextProperty) is BindingExpression binding)
                || !binding.IsDirty)
            {
                return;
            }

            binding.UpdateSource();
        }

        /// <summary>ViewModel を取得する</summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="viewModel">取得した ViewModel</param>
        /// <returns>true: 取得できた / false: 取得できなかった</returns>
        private static bool TryGetViewModel(object sender, [NotNullWhen(true)] out IMainWindowViewModel? viewModel)
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
            /// <summary>ドラッグエンターイベントのハンドラ</summary>
            public DragEventHandler DragEnter { get; }

            /// <summary>ドラッグオーバーイベントのハンドラ</summary>
            public DragEventHandler DragOver { get; }

            /// <summary>ドラッグリーブイベントのハンドラ</summary>
            public DragEventHandler DragLeave { get; }

            /// <summary>ドロップイベントのハンドラ</summary>
            public DragEventHandler Drop { get; }

            /// <summary>
            /// 各ドラッグ&amp;ドロップイベントのハンドラを設定して初期化する
            /// </summary>
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
