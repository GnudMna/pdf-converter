using System;
using System.IO;

namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// ポータブル版として動いているかの判定と、ポータブル版のデータの置き場所
    /// </summary>
    /// <remarks>
    /// 実行ファイルと同じフォルダーにマーカーファイル (<see cref="MarkerFileName"/>) があればポータブル版とみなす<br/>
    /// ポータブル版は設定とログを <c>%LOCALAPPDATA%</c> ではなく、実行ファイルと同じフォルダーの
    /// <see cref="DataDirectoryName"/> に保存し、更新はインストーラーではなく zip で行う<br/>
    /// マーカーファイルはポータブル版の zip (<c>scripts/build-portable.ps1</c>) にだけ含め、インストーラーには含めない
    /// </remarks>
    public static class PortableMode
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>ポータブル版であることを示すマーカーファイルの名前</summary>
        public const string MarkerFileName = "portable.txt";

        /// <summary>ポータブル版のデータ (設定・ログ) を置くフォルダーの名前</summary>
        public const string DataDirectoryName = "UserData";

        /// <summary>ポータブル版のアプリを構成するファイルの一覧 (アプリフォルダーからの相対パスを 1 行に 1 つ)</summary>
        public const string ManifestFileName = "portable-manifest.txt";

        /// <summary>ユーザー設定のファイル名</summary>
        public const string SettingsFileName = "user.config";


        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>判定結果のキャッシュ (起動中にマーカーの有無が変わっても判定は変えない)</summary>
        private static readonly Lazy<bool> IsEnabledCache = new Lazy<bool>(DetectEnabled);


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>テスト時にポータブル版かどうかの判定を差し替える</summary>
        internal static bool? IsEnabledOverride { get; set; }

        /// <summary>テスト時にアプリフォルダーを差し替える</summary>
        internal static string? AppDirectoryOverride { get; set; }

        /// <summary>ポータブル版として動いているかどうか</summary>
        public static bool IsEnabled => IsEnabledOverride ?? IsEnabledCache.Value;

        /// <summary>実行ファイルのあるフォルダー (末尾の区切り文字なし)</summary>
        public static string AppDirectory =>
            Path.TrimEndingDirectorySeparator(AppDirectoryOverride ?? AppContext.BaseDirectory);

        /// <summary>ポータブル版のデータフォルダー</summary>
        public static string DataDirectory => Path.Combine(AppDirectory, DataDirectoryName);

        /// <summary>ポータブル版のユーザー設定ファイル</summary>
        public static string SettingsFilePath => Path.Combine(DataDirectory, SettingsFileName);

        /// <summary>ポータブル版のログフォルダー</summary>
        public static string LogDirectory => GetLogDirectory(AppDirectory);


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 指定したフォルダーにマーカーファイルがあるか判定する
        /// </summary>
        /// <param name="directory">調べるフォルダー</param>
        /// <returns>マーカーファイルがあれば <c>true</c></returns>
        public static bool HasMarker(string directory)
        {
            return !string.IsNullOrEmpty(directory)
                && File.Exists(Path.Combine(directory, MarkerFileName));
        }

        /// <summary>
        /// 指定したアプリフォルダーのポータブル版のログフォルダーを返す
        /// </summary>
        /// <param name="appDirectory">アプリフォルダー</param>
        /// <returns>ログフォルダーの絶対パス</returns>
        public static string GetLogDirectory(string appDirectory)
        {
            return Path.Combine(appDirectory, DataDirectoryName, "logs");
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>実行ファイルのフォルダーにマーカーファイルがあるか調べる</summary>
        /// <returns>ポータブル版なら <c>true</c></returns>
        private static bool DetectEnabled() => HasMarker(AppDirectory);
    }
}
