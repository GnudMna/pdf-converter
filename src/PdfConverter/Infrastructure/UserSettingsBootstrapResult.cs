namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// ユーザー設定の起動時初期化結果
    /// </summary>
    public sealed class UserSettingsBootstrapResult
    {
        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 初期化結果を作成する
        /// </summary>
        /// <param name="recoveredFromCorruption">破損した設定ファイルを回復したかどうか</param>
        /// <param name="upgraded">旧バージョンの設定を移行したかどうか</param>
        public UserSettingsBootstrapResult(bool recoveredFromCorruption, bool upgraded)
        {
            RecoveredFromCorruption = recoveredFromCorruption;
            Upgraded = upgraded;
        }


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>破損した設定ファイルを削除して既定値へ回復したかどうか</summary>
        public bool RecoveredFromCorruption { get; }

        /// <summary>旧バージョンのユーザー設定を移行したかどうか</summary>
        public bool Upgraded { get; }
    }
}
