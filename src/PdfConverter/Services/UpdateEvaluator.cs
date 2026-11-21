namespace PdfConverter.Services
{
    /// <summary>
    /// version API レスポンスから更新の有無を判定する
    /// </summary>
    public static class UpdateEvaluator
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// version API レスポンスを評価する
        /// </summary>
        /// <param name="currentVersionRaw">現在バージョン（表示用）</param>
        /// <param name="response">API レスポンス</param>
        /// <param name="platform"><c>x64</c> / <c>x86</c></param>
        /// <returns>判定結果</returns>
        public static UpdateCheckResult Evaluate(
            string currentVersionRaw,
            VersionApiResponse response,
            string platform)
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

            VersionApiAsset asset = UpdateAssetSelector.SelectInstaller(response.Assets, platform);
            if (asset == null)
            {
                if (UpdateAssetSelector.HasAnyInstaller(response.Assets))
                {
                    return UpdateCheckResult.Failed(
                        $"この CPU（{platform}）向けのインストーラーが見つかりませんでした。（タグ: {response.Tag}）");
                }

                return UpdateCheckResult.Failed(
                    $"インストーラーアセットが見つかりませんでした。（タグ: {response.Tag}）");
            }

            if (string.IsNullOrWhiteSpace(asset.Digest))
            {
                return UpdateCheckResult.Failed($"アセットに digest がありません: {asset.Name}");
            }

            if (!UpdateDigestHelper.TryParseDigest(asset.Digest, out string sha256, out string digestError))
            {
                return UpdateCheckResult.Failed(digestError);
            }

            if (!UpdateDigestHelper.TryValidateDeclaredSize(asset.Size, out string sizeError))
            {
                return UpdateCheckResult.Failed(sizeError);
            }

            return UpdateCheckResult.Available(new AvailableUpdate
            {
                CurrentVersion = currentVersionRaw,
                LatestVersion = latestRaw,
                Tag = response.Tag,
                AssetName = asset.Name,
                Size = asset.Size,
                Sha256 = sha256,
            });
        }
    }
}
