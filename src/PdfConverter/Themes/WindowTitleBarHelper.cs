using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PdfConverter.Themes
{
    /// <summary>
    /// OS 標準のタイトルバーの色を、適用中のテーマに合わせる
    /// </summary>
    public static class WindowTitleBarHelper
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>タイトルバーをダークモードで描画する DWM 属性 (Windows 10 20H1 以降)</summary>
        private const int DwmwaUseImmersiveDarkMode = 20;

        /// <summary>タイトルバーの背景色を指定する DWM 属性 (Windows 11 以降)</summary>
        private const int DwmwaCaptionColor = 35;

        /// <summary>タイトルバーの文字色を指定する DWM 属性 (Windows 11 以降)</summary>
        private const int DwmwaTextColor = 36;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 適用中のテーマに合わせて、ウィンドウのタイトルバーの色を設定する<br/>
        /// ウィンドウのハンドルが未作成の場合や OS が属性に対応していない場合は何もしない
        /// </summary>
        /// <param name="window">対象のウィンドウ</param>
        public static void Apply(Window window)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            int useDarkMode = ThemeManager.IsDarkThemeApplied ? 1 : 0;
            _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref useDarkMode, sizeof(int));

            if (TryGetResourceColor("WindowBackgroundBrush", out Color captionColor))
            {
                int captionColorRef = ToColorRef(captionColor);
                _ = DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref captionColorRef, sizeof(int));
            }

            if (TryGetResourceColor("SectionTitleForegroundBrush", out Color textColor))
            {
                int textColorRef = ToColorRef(textColor);
                _ = DwmSetWindowAttribute(handle, DwmwaTextColor, ref textColorRef, sizeof(int));
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// WPF の色を Win32 の COLORREF (0x00BBGGRR) に変換する
        /// </summary>
        /// <param name="color">変換する色</param>
        /// <returns>COLORREF 形式の値</returns>
        internal static int ToColorRef(Color color)
        {
            return color.R | (color.G << 8) | (color.B << 16);
        }

        /// <summary>
        /// アプリケーションリソースの <see cref="SolidColorBrush"/> から色を取得する
        /// </summary>
        /// <param name="resourceKey">リソースキー</param>
        /// <param name="color">取得した色</param>
        /// <returns>true: 取得できた / false: リソースがない、またはブラシの種類が違う</returns>
        private static bool TryGetResourceColor(string resourceKey, out Color color)
        {
            if (Application.Current?.TryFindResource(resourceKey) is SolidColorBrush brush)
            {
                color = brush.Color;
                return true;
            }

            color = default;
            return false;
        }


        /********************************************************************************/
        /*                               Win32 API 定義                                 */
        /********************************************************************************/
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
    }
}
