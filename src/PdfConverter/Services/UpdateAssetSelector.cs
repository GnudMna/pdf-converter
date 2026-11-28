using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace PdfConverter.Services
{
    /// <summary>
    /// 更新パッケージ (インストーラー・ポータブル版の zip) のアセットの選択
    /// </summary>
    public static class UpdateAssetSelector
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 現在プロセスのアーキテクチャ向けプラットフォーム名を返す
        /// </summary>
        /// <returns><c>x64</c> または <c>x86</c></returns>
        public static string GetCurrentPlatform() =>
            Environment.Is64BitProcess ? "x64" : "x86";

        /// <summary>
        /// 更新パッケージのアセット名の末尾 (プラットフォーム名より後ろ) を返す
        /// </summary>
        /// <param name="kind">更新パッケージの種類</param>
        /// <returns><c>-setup.exe</c> または <c>-portable.zip</c></returns>
        public static string GetAssetSuffix(UpdatePackageKind kind) =>
            kind == UpdatePackageKind.Portable ? "-portable.zip" : "-setup.exe";

        /// <summary>
        /// プラットフォームに合う <c>PdfConverter-*-{platform}-setup.exe</c> (ポータブル版は <c>-portable.zip</c>) を選ぶ
        /// </summary>
        /// <param name="assets">アセット一覧</param>
        /// <param name="platform"><c>x64</c> / <c>x86</c></param>
        /// <param name="kind">更新パッケージの種類</param>
        /// <returns>一致するアセット。無ければ <c>null</c></returns>
        public static GitHubReleaseAsset? SelectPackage(
            GitHubReleaseAsset[]? assets,
            string platform,
            UpdatePackageKind kind)
        {
            if (assets == null || string.IsNullOrEmpty(platform))
            {
                return null;
            }

            string marker = $"-{platform}{GetAssetSuffix(kind)}";
            foreach (GitHubReleaseAsset asset in assets)
            {
                if (IsPackageName(asset?.Name, marker))
                {
                    return asset;
                }
            }

            return null;
        }

        /// <summary>
        /// 指定した種類の更新パッケージが (プラットフォームを問わず) 1 件でもあるか判定する
        /// </summary>
        /// <param name="assets">アセット一覧</param>
        /// <param name="kind">更新パッケージの種類</param>
        /// <returns>該当するアセットがあれば <c>true</c></returns>
        public static bool HasAnyPackage(GitHubReleaseAsset[]? assets, UpdatePackageKind kind)
        {
            if (assets == null)
            {
                return false;
            }

            string suffix = GetAssetSuffix(kind);
            foreach (GitHubReleaseAsset asset in assets)
            {
                if (IsPackageName(asset?.Name, suffix))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 保存先フォルダー直下のファイル名としてそのまま使える名前か判定する
        /// </summary>
        /// <param name="name">アセット名などのファイル名</param>
        /// <returns>ディレクトリ区切り・相対パス表記・ファイル名に使えない文字を含まなければ <c>true</c></returns>
        /// <remarks>
        /// 更新サーバーの応答をパスに組み込むため、保存先フォルダー外への書き込み (パストラバーサル) を防ぐ<br/>
        /// Windows が末尾の <c>.</c> と空白を切り捨てて別名として扱うため、これらで終わる名前も拒否する
        /// </remarks>
        public static bool IsSafeFileName([NotNullWhen(true)] string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "." || name == "..")
            {
                return false;
            }

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || name.Contains('/')
                || name.Contains('\\')
                || name.Contains(':'))
            {
                return false;
            }

            return !name.EndsWith('.')
                && !name.EndsWith(' ');
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// <c>PdfConverter-</c> で始まり、指定した末尾で終わる安全なファイル名か判定する
        /// </summary>
        /// <param name="name">アセット名</param>
        /// <param name="suffix">末尾</param>
        /// <returns>該当すれば <c>true</c></returns>
        private static bool IsPackageName([NotNullWhen(true)] string? name, string suffix)
        {
            return IsSafeFileName(name)
                && name.StartsWith("PdfConverter-", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
