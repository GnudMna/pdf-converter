using System.Collections.Generic;
using PdfConverter.Models;
using PdfConverter.Themes;

namespace PdfConverter.ViewModels
{
    /// <summary>
    /// 外観テーマ (ライト / ダーク / システム) の UI 設定を管理する ViewModel
    /// </summary>
    public sealed class ThemeSettingsViewModel : ViewModelBase
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>テーマ</summary>
        private ThemeMode _themeMode = ThemeManager.LoadSavedThemeMode();

        /// <summary>テーマの選択肢</summary>
        private static readonly IReadOnlyList<ThemeModeOption> ThemeModeOptionsList =
            new List<ThemeModeOption>
            {
                new ThemeModeOption(ThemeMode.Light, "ライト"),
                new ThemeModeOption(ThemeMode.Dark, "ダーク"),
                new ThemeModeOption(ThemeMode.System, "システム"),
            };


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>テーマ</summary>
        public ThemeMode ThemeMode
        {
            get => _themeMode;
            set
            {
                if (!SetProperty(ref _themeMode, value))
                {
                    return;
                }

                ThemeManager.Apply(value);
                OnPropertyChanged(nameof(IsLightThemeSelected));
                OnPropertyChanged(nameof(IsDarkThemeSelected));
                OnPropertyChanged(nameof(IsSystemThemeSelected));
            }
        }

        /// <summary>ライトテーマが選択されているかどうか</summary>
        public bool IsLightThemeSelected
        {
            get => ThemeMode == ThemeMode.Light;
            set
            {
                if (value)
                {
                    ThemeMode = ThemeMode.Light;
                }
            }
        }

        /// <summary>ダークテーマが選択されているかどうか</summary>
        public bool IsDarkThemeSelected
        {
            get => ThemeMode == ThemeMode.Dark;
            set
            {
                if (value)
                {
                    ThemeMode = ThemeMode.Dark;
                }
            }
        }

        /// <summary>システム設定に従うテーマが選択されているかどうか</summary>
        public bool IsSystemThemeSelected
        {
            get => ThemeMode == ThemeMode.System;
            set
            {
                if (value)
                {
                    ThemeMode = ThemeMode.System;
                }
            }
        }

        /// <summary>テーマの選択肢</summary>
        public IReadOnlyList<ThemeModeOption> ThemeModeOptions => ThemeModeOptionsList;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 保存済み設定から UI 状態を再読み込みする (テーマ適用は行わない)
        /// </summary>
        public void ReloadFromStore()
        {
            ThemeMode mode = ThemeManager.LoadSavedThemeMode();
            if (_themeMode != mode)
            {
                _themeMode = mode;
                OnPropertyChanged(nameof(ThemeMode));
            }

            OnPropertyChanged(nameof(IsLightThemeSelected));
            OnPropertyChanged(nameof(IsDarkThemeSelected));
            OnPropertyChanged(nameof(IsSystemThemeSelected));
        }
    }
}
