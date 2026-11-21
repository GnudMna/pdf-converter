namespace PdfConverter.Properties
{
    /// <summary>
    /// 自動生成の <see cref="Settings"/> に対する手書き拡張
    /// </summary>
    internal sealed partial class Settings
    {
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
    }
}
