using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PdfConverter.ViewModels;

namespace PdfConverter.Views.Panels
{
    /// <summary>
    /// PDF ページプレビューとページナビゲーションパネル
    /// </summary>
    /// <remarks>
    /// プレビューを表示サイズに合わせて描画できるよう、プレビュー画像を表示する領域のサイズをデバイス px で ViewModel に通知する
    /// </remarks>
    public partial class PreviewPanel : UserControl
    {
        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        public PreviewPanel()
        {
            InitializeComponent();
            PreviewImageHost.SizeChanged += (_, _) => ReportPreviewViewportSize();
            DataContextChanged += (_, _) => ReportPreviewViewportSize();
        }


        /********************************************************************************/
        /*                               イベントハンドラ                               */
        /********************************************************************************/
        /// <inheritdoc/>
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            ReportPreviewViewportSize();
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>プレビュー画像を表示する領域のサイズをデバイス px に換算して ViewModel に通知する</summary>
        private void ReportPreviewViewportSize()
        {
            if (DataContext is not PreviewPanelViewModel viewModel)
            {
                return;
            }

            DpiScale dpi = VisualTreeHelper.GetDpi(PreviewImageHost);
            int pixelWidth = (int)Math.Ceiling(PreviewImageHost.ActualWidth * dpi.DpiScaleX);
            int pixelHeight = (int)Math.Ceiling(PreviewImageHost.ActualHeight * dpi.DpiScaleY);
            viewModel.UpdatePreviewViewportPixelSize(pixelWidth, pixelHeight);
        }
    }
}
