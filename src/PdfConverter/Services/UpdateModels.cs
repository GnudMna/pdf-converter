using System;

namespace PdfConverter.Services
{
    /// <summary>
    /// gh-release-bridge の <c>/version</c> レスポンス
    /// </summary>
    public sealed class VersionApiResponse
    {
        /// <summary>リリースタグ (例: <c>v0.4.0</c>)</summary>
        public string Tag { get; set; } = string.Empty;

        /// <summary>許可されたアセット一覧</summary>
        public VersionApiAsset[] Assets { get; set; } = Array.Empty<VersionApiAsset>();
    }

    /// <summary>
    /// version API のアセット要素
    /// </summary>
    public sealed class VersionApiAsset
    {
        /// <summary>アセット名</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>バイトサイズ</summary>
        public long Size { get; set; }

        /// <summary>GitHub の digest (例: <c>sha256:...</c>)。無い場合は <c>null</c></summary>
        public string? Digest { get; set; }
    }

    /// <summary>
    /// 適用可能な更新情報
    /// </summary>
    public sealed class AvailableUpdate
    {
        /// <summary>現在のバージョン (表示用)</summary>
        public string CurrentVersion { get; set; } = string.Empty;

        /// <summary>最新バージョン (先頭 <c>v</c> なし)</summary>
        public string LatestVersion { get; set; } = string.Empty;

        /// <summary>リリースタグ</summary>
        public string Tag { get; set; } = string.Empty;

        /// <summary>ダウンロードするアセット名</summary>
        public string AssetName { get; set; } = string.Empty;

        /// <summary>宣言サイズ (バイト)</summary>
        public long Size { get; set; }

        /// <summary>正規化済み SHA-256 (小文字 64 桁)</summary>
        public string Sha256 { get; set; } = string.Empty;
    }

    /// <summary>
    /// 更新確認の結果種別
    /// </summary>
    public enum UpdateCheckKind
    {
        /// <summary>最新</summary>
        UpToDate,

        /// <summary>更新あり</summary>
        Available,

        /// <summary>失敗</summary>
        Failed,
    }

    /// <summary>
    /// 更新確認の結果
    /// </summary>
    public sealed class UpdateCheckResult
    {
        /// <summary>結果種別</summary>
        public UpdateCheckKind Kind { get; private set; }

        /// <summary>現在バージョン</summary>
        public string? CurrentVersion { get; private set; }

        /// <summary>最新バージョン (判明時)</summary>
        public string? LatestVersion { get; private set; }

        /// <summary>利用可能な更新 (Available 時)</summary>
        public AvailableUpdate? Update { get; private set; }

        /// <summary>エラーメッセージ (Failed 時)</summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>最新である結果を生成する</summary>
        public static UpdateCheckResult UpToDate(string currentVersion, string latestVersion) =>
            new UpdateCheckResult
            {
                Kind = UpdateCheckKind.UpToDate,
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
            };

        /// <summary>更新ありの結果を生成する</summary>
        public static UpdateCheckResult Available(AvailableUpdate update) =>
            new UpdateCheckResult
            {
                Kind = UpdateCheckKind.Available,
                CurrentVersion = update.CurrentVersion,
                LatestVersion = update.LatestVersion,
                Update = update,
            };

        /// <summary>失敗結果を生成する</summary>
        public static UpdateCheckResult Failed(string message) =>
            new UpdateCheckResult
            {
                Kind = UpdateCheckKind.Failed,
                ErrorMessage = message,
            };
    }

    /// <summary>
    /// 更新適用の結果種別
    /// </summary>
    public enum UpdateApplyKind
    {
        /// <summary>インストーラーを起動した</summary>
        SetupStarted,

        /// <summary>失敗</summary>
        Failed,
    }

    /// <summary>
    /// 更新適用の結果
    /// </summary>
    public sealed class UpdateApplyResult
    {
        /// <summary>結果種別</summary>
        public UpdateApplyKind Kind { get; private set; }

        /// <summary>保存したセットアップのパス (成功時)</summary>
        public string? SetupPath { get; private set; }

        /// <summary>エラーメッセージ (失敗時)</summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>セットアップ起動成功の結果を生成する</summary>
        public static UpdateApplyResult SetupStarted(string setupPath) =>
            new UpdateApplyResult
            {
                Kind = UpdateApplyKind.SetupStarted,
                SetupPath = setupPath,
            };

        /// <summary>失敗結果を生成する</summary>
        public static UpdateApplyResult Failed(string message) =>
            new UpdateApplyResult
            {
                Kind = UpdateApplyKind.Failed,
                ErrorMessage = message,
            };
    }
}
