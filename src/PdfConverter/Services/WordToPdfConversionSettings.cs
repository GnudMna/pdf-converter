using System;
using PdfConverter.Models;
using PdfConverter.Properties;

namespace PdfConverter.Services
{
    /// <summary>
    /// ユーザー設定に永続化するWord → PDF変換エンジン設定
    /// </summary>
    public sealed class WordToPdfConversionSettings : IWordToPdfConversionSettings
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <inheritdoc/>
        public WordToPdfBackend Backend { get; set; }

        /// <inheritdoc/>
        public string LibreOfficePath { get; set; }


        /********************************************************************************/
        /*                                    イベント                                    */
        /********************************************************************************/
        /// <inheritdoc/>
        public event EventHandler SettingsChanged;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 保存済み設定を読み込んで初期化する
        /// </summary>
        public WordToPdfConversionSettings()
        {
            Backend = WordToPdfBackendParser.Parse(Settings.Default.WordToPdfBackend);
            LibreOfficePath = Settings.Default.LibreOfficePath ?? string.Empty;
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public void Save()
        {
            Settings.Default.WordToPdfBackend = Backend.ToString();
            Settings.Default.LibreOfficePath = LibreOfficePath ?? string.Empty;
            Settings.Default.Save();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
