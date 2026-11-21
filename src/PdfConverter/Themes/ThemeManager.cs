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
        private const string ThemeMarkerKey = "ThemeMarker";

        /// <summary>保存済み設定に基づいて起動時テーマを適用する</summary>
        public static void Initialize()
        {
            Apply(ParseThemeMode(Settings.Default.ThemeMode));
        }

        /// <summary>指定テーマを適用し、ユーザー設定に保存する</summary>
        public static void Apply(ThemeMode mode)
        {
            var effectiveMode = ResolveEffectiveMode(mode);
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

            Settings.Default.ThemeMode = mode.ToString();
            Settings.Default.Save();
        }

        /// <summary>Windowsのアプリテーマ設定がダークかどうかを返す</summary>
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

        /// <summary>設定文字列を<see cref="ThemeMode"/>に変換する</summary>
        public static ThemeMode ParseThemeMode(string value)
        {
            if (Enum.TryParse(value, out ThemeMode mode))
            {
                return mode;
            }

            return ThemeMode.System;
        }

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
