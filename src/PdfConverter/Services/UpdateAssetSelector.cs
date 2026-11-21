using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace PdfConverter.Services
{
    /// <summary>
    /// インストーラーアセットの選択
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
        /// プラットフォームに合う <c>PdfConverter-*-{platform}-setup.exe</c> を選ぶ
        /// </summary>
        /// <param name="assets">アセット一覧</param>
        /// <param name="platform"><c>x64</c> / <c>x86</c></param>
        /// <returns>一致するアセット。無ければ <c>null</c></returns>
        public static VersionApiAsset? SelectInstaller(VersionApiAsset[]? assets, string platform)
        {
            if (assets == null || string.IsNullOrEmpty(platform))
            {
                return null;
            }

            string marker = $"-{platform}-setup.exe";
            foreach (VersionApiAsset asset in assets)
            {
                if (!IsSafeFileName(asset?.Name))
                {
                    continue;
                }

                if (asset.Name.EndsWith(marker, StringComparison.OrdinalIgnoreCase) &&
                    asset.Name.StartsWith("PdfConverter-", StringComparison.OrdinalIgnoreCase))
                {
                    return asset;
                }
            }

            return null;
        }

        /// <summary>
        /// インストーラー系アセットが 1 件でもあるか判定する
        /// </summary>
        /// <param name="assets">アセット一覧</param>
        /// <returns>setup.exe があれば <c>true</c></returns>
        public static bool HasAnyInstaller(VersionApiAsset[]? assets)
        {
            if (assets == null)
            {
                return false;
            }

            foreach (VersionApiAsset asset in assets)
            {
                if (IsSafeFileName(asset?.Name) &&
                    asset.Name.StartsWith("PdfConverter-", StringComparison.OrdinalIgnoreCase) &&
                    asset.Name.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase))
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
    }
}
