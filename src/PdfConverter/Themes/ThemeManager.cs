using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using PdfConverter.Models;
using PdfConverter.Properties;

namespace PdfConverter.Themes
{
    /// <summary>
    /// ライト / ダークテーマの切り替えと永続化を担当する
    /// </summary>
    public static class ThemeManager
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private const string ThemeMarkerKey = "ThemeMarker";

        private static ThemeMode _selectedThemeMode = ThemeMode.System;
        private static ThemeMode? _lastAppliedEffectiveMode;
        private static bool _isMonitoringSystemTheme;
        

        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 保存済み設定に基づいて起動時テーマを適用する
        /// </summary>
        public static void Initialize()
        {
            _selectedThemeMode = ParseThemeMode(Settings.Default.ThemeMode);
            ApplyThemeResources(_selectedThemeMode);
            EnsureSystemThemeMonitoring();
        }

        /// <summary>
        /// 指定テーマを適用し、ユーザー設定に保存する
        /// </summary>
        /// <param name="mode">適用するテーマモード</param>
        public static void Apply(ThemeMode mode)
        {
            _selectedThemeMode = mode;
            ApplyThemeResources(mode);
            Settings.Default.ThemeMode = mode.ToString();
            Settings.Default.Save();
            EnsureSystemThemeMonitoring();
        }

        /// <summary>
        /// システムテーマ監視を解除する
        /// </summary>
        public static void Shutdown()
        {
            if (_isMonitoringSystemTheme)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                _isMonitoringSystemTheme = false;
            }
        }

        /// <summary>
        /// Windowsのアプリテーマ設定がダークかどうかを返す
        /// </summary>
        /// <returns>true: ダークテーマ, false: ライトテーマ</returns>
        public static bool IsSystemDarkMode()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(
                       @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                var value = key?.GetValue("AppsUseLightTheme");
                if (value is int useLightTheme)
                {
                    return useLightTheme == 0;
                }
            }

            return false;
        }

        /// <summary>
        /// 設定文字列を<see cref="ThemeMode"/>に変換する
        /// </summary>
        /// <param name="value">設定文字列</param>
        /// <returns><see cref="ThemeMode"/></returns>
        public static ThemeMode ParseThemeMode(string value)
        {
            if (Enum.TryParse(value, out ThemeMode mode))
            {
                return mode;
            }

            return ThemeMode.System;
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// システム設定変更イベントに反応すべきかどうかを判定する
        /// </summary>
        /// <param name="selectedMode">ユーザーが選択したテーマモード</param>
        /// <param name="category">変更されたユーザー設定のカテゴリ</param>
        /// <returns>テーマの再適用が必要ならtrue</returns>
        internal static bool ShouldReactToUserPreferenceChange(ThemeMode selectedMode, UserPreferenceCategory category)
        {
            return selectedMode == ThemeMode.System && category == UserPreferenceCategory.General;
        }

        /// <summary>
        /// システムテーマ監視を確保する
        /// </summary>
        private static void EnsureSystemThemeMonitoring()
        {
            if (_selectedThemeMode == ThemeMode.System)
            {
                if (!_isMonitoringSystemTheme)
                {
                    SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
                    _isMonitoringSystemTheme = true;
                }

                return;
            }

            if (_isMonitoringSystemTheme)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                _isMonitoringSystemTheme = false;
            }
        }

        /// <summary>
        /// システムテーマ変更イベントに反応する
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">イベントの引数</param>
        private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (!ShouldReactToUserPreferenceChange(_selectedThemeMode, e.Category))
            {
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            dispatcher.BeginInvoke(new Action(() =>
            {
                if (_selectedThemeMode == ThemeMode.System)
                {
                    ApplyThemeResources(ThemeMode.System);
                }
            }));
        }

        /// <summary>
        /// テーマリソースを適用する
        /// </summary>
        /// <param name="mode">適用するテーマモード</param>
        private static void ApplyThemeResources(ThemeMode mode)
        {
            ThemeMode effectiveMode = ResolveEffectiveMode(mode);
            if (_lastAppliedEffectiveMode == effectiveMode)
            {
                return;
            }

            _lastAppliedEffectiveMode = effectiveMode;
            var themeUri = new Uri(
                effectiveMode == ThemeMode.Dark
                    ? "Themes/DarkTheme.xaml"
                    : "Themes/LightTheme.xaml",
                UriKind.Relative);

            var appResources = Application.Current.Resources;
            var mergedDictionaries = appResources.MergedDictionaries;
            var existingTheme = mergedDictionaries
                .FirstOrDefault(dictionary => dictionary.Contains(ThemeMarkerKey));

            if (existingTheme != null)
            {
                mergedDictionaries.Remove(existingTheme);
            }

            mergedDictionaries.Insert(0, new ResourceDictionary { Source = themeUri });
        }

        /// <summary>
        /// 有効なテーマモードを解決する
        /// </summary>
        /// <param name="mode">適用するテーマモード</param>
        /// <returns><see cref="ThemeMode"/></returns>
        private static ThemeMode ResolveEffectiveMode(ThemeMode mode)
        {
            if (mode == ThemeMode.System)
            {
                return IsSystemDarkMode() ? ThemeMode.Dark : ThemeMode.Light;
            }

            return mode;
        }
    }
}
