using System.Configuration;
using PdfConverter.Infrastructure;

namespace PdfConverter.Properties
{
    /// <summary>
    /// 自動生成の <see cref="Settings"/> に対する手書き拡張
    /// </summary>
    internal sealed partial class Settings
    {
        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// ポータブル版では、設定の保存先をアプリフォルダー内のファイルに差し替えて初期化する
        /// </summary>
        private Settings()
        {
            if (PortableMode.IsEnabled)
            {
                UseProvider(new PortableSettingsProvider(PortableMode.SettingsFilePath));
            }
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 破損などで使い物にならなくなったシングルトンを破棄し、既定値で作り直す
        /// </summary>
        public static void RecreateDefaultInstance()
        {
            defaultInstance = (Settings)Synchronized(new Settings());
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// すべての設定項目の保存先を指定したプロバイダーにする
        /// </summary>
        /// <param name="provider">設定の保存先</param>
        private void UseProvider(SettingsProvider provider)
        {
            Providers.Add(provider);
            foreach (SettingsProperty property in Properties)
            {
                property.Provider = provider;
            }
        }
    }
}
