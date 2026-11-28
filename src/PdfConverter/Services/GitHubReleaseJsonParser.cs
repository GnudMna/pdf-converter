using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace PdfConverter.Services
{
    /// <summary>
    /// GitHub Releases API の最新リリース (<c>/repos/{owner}/{repo}/releases/latest</c>) の JSON を解析する
    /// </summary>
    public static class GitHubReleaseJsonParser
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// JSON 文字列を <see cref="GitHubRelease"/> に変換する
        /// </summary>
        /// <param name="json">レスポンス本文</param>
        /// <returns>解析結果</returns>
        /// <exception cref="InvalidDataException">形式が不正な場合</exception>
        public static GitHubRelease Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException("GitHub Release 情報の応答が空です。");
            }

            var serializer = new DataContractJsonSerializer(typeof(GitHubReleaseDto));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                object? parsed = serializer.ReadObject(stream);
                if (!(parsed is GitHubReleaseDto dto) || string.IsNullOrWhiteSpace(dto.Tag))
                {
                    throw new InvalidDataException("GitHub Release 情報の応答形式が不正です。");
                }

                var assets = new List<GitHubReleaseAsset>();
                if (dto.Assets != null)
                {
                    foreach (GitHubReleaseAssetDto asset in dto.Assets)
                    {
                        if (asset == null || string.IsNullOrWhiteSpace(asset.Name))
                        {
                            continue;
                        }

                        assets.Add(new GitHubReleaseAsset
                        {
                            Name = asset.Name,
                            Size = asset.Size,
                            Digest = asset.Digest,
                        });
                    }
                }

                return new GitHubRelease
                {
                    Tag = dto.Tag,
                    Assets = assets.ToArray(),
                };
            }
        }


        /********************************************************************************/
        /*                                   ネスト型                                   */
        /********************************************************************************/
        [DataContract]
        private sealed class GitHubReleaseDto
        {
            /// <summary>リリースタグ</summary>
            [DataMember(Name = "tag_name")]
            public string? Tag { get; set; }

            /// <summary>アセット一覧</summary>
            [DataMember(Name = "assets")]
            public GitHubReleaseAssetDto[]? Assets { get; set; }
        }

        [DataContract]
        private sealed class GitHubReleaseAssetDto
        {
            /// <summary>アセット名</summary>
            [DataMember(Name = "name")]
            public string? Name { get; set; }

            /// <summary>バイトサイズ</summary>
            [DataMember(Name = "size")]
            public long Size { get; set; }

            /// <summary>GitHub の digest (例: <c>sha256:...</c>)</summary>
            [DataMember(Name = "digest")]
            public string? Digest { get; set; }
        }
    }
}
