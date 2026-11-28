namespace PdfConverter.Services
{
    /// <summary>
    /// GitHub Release 情報から更新の有無を判定する
    /// </summary>
    public static class UpdateEvaluator
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// GitHub Release 情報を評価する
        /// </summary>
        /// <param name="currentVersionRaw">現在バージョン (表示用)</param>
        /// <param name="response">API レスポンス</param>
        /// <param name="platform"><c>x64</c> / <c>x86</c></param>
        /// <param name="packageKind">選ぶ更新パッケージの種類 (インストール版はインストーラー、ポータブル版は zip)</param>
        /// <returns>判定結果</returns>
        public static UpdateCheckResult Evaluate(
            string currentVersionRaw,
            GitHubRelease response,
            string platform,
            UpdatePackageKind packageKind = UpdatePackageKind.Installer)
        {
            if (response == null || string.IsNullOrWhiteSpace(response.Tag))
            {
                return UpdateCheckResult.Failed("最新リリース情報を取得できませんでした。");
            }

            if (!SemVerComparer.TryParse(currentVersionRaw, out var currentVersion))
            {
                return UpdateCheckResult.Failed($"現在のバージョンが不正です: {currentVersionRaw}");
            }

            string latestRaw = SemVerComparer.StripVersionPrefix(response.Tag.Trim());
            if (!SemVerComparer.TryParse(latestRaw, out var latestVersion))
            {
                return UpdateCheckResult.Failed($"最新バージョンのタグが不正です: {response.Tag}");
            }

            if (!SemVerComparer.IsNewer(latestVersion, currentVersion))
            {
                return UpdateCheckResult.UpToDate(currentVersionRaw, latestRaw);
            }

            GitHubReleaseAsset? asset = UpdateAssetSelector.SelectPackage(response.Assets, platform, packageKind);
            if (asset == null)
            {
                string packageName = packageKind == UpdatePackageKind.Portable ? "ポータブル版" : "インストーラー";
                if (UpdateAssetSelector.HasAnyPackage(response.Assets, packageKind))
                {
                    return UpdateCheckResult.Failed(
                        $"この CPU ({platform}) 向けの{packageName}が見つかりませんでした。(タグ: {response.Tag})");
                }

                return UpdateCheckResult.Failed(
                    $"{packageName}のアセットが見つかりませんでした。(タグ: {response.Tag})");
            }

            if (string.IsNullOrWhiteSpace(asset.Digest))
            {
                return UpdateCheckResult.Failed($"アセットに digest がありません: {asset.Name}");
            }

            if (!UpdateDigestHelper.TryParseDigest(asset.Digest, out string? sha256, out string? digestError))
            {
                return UpdateCheckResult.Failed(digestError);
            }

            if (!UpdateDigestHelper.TryValidateDeclaredSize(asset.Size, out string? sizeError))
            {
                return UpdateCheckResult.Failed(sizeError);
            }

            return UpdateCheckResult.Available(new AvailableUpdate
            {
                CurrentVersion = currentVersionRaw,
                LatestVersion = latestRaw,
                Tag = response.Tag,
                AssetName = asset.Name,
                PackageKind = packageKind,
                Size = asset.Size,
                Sha256 = sha256,
            });
        }
    }
}
