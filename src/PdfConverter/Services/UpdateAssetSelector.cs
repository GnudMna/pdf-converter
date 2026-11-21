using System;

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
        public static VersionApiAsset SelectInstaller(VersionApiAsset[] assets, string platform)
        {
            if (assets == null || string.IsNullOrEmpty(platform))
            {
                return null;
            }

            string marker = $"-{platform}-setup.exe";
            foreach (VersionApiAsset asset in assets)
            {
                if (asset?.Name == null)
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
        public static bool HasAnyInstaller(VersionApiAsset[] assets)
        {
            if (assets == null)
            {
                return false;
            }

            foreach (VersionApiAsset asset in assets)
            {
                if (asset?.Name != null &&
                    asset.Name.StartsWith("PdfConverter-", StringComparison.OrdinalIgnoreCase) &&
                    asset.Name.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
