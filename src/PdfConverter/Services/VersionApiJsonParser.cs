using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace PdfConverter.Services
{
    /// <summary>
    /// gh-release-bridge の <c>/version</c> JSON を解析する
    /// </summary>
    public static class VersionApiJsonParser
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// JSON 文字列を <see cref="VersionApiResponse"/> に変換する
        /// </summary>
        /// <param name="json">レスポンス本文</param>
        /// <returns>解析結果</returns>
        /// <exception cref="InvalidDataException">形式が不正な場合</exception>
        public static VersionApiResponse Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException("version API の応答が空です。");
            }

            var serializer = new DataContractJsonSerializer(typeof(VersionApiDto));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                object? parsed = serializer.ReadObject(stream);
                if (!(parsed is VersionApiDto dto) || string.IsNullOrWhiteSpace(dto.Tag))
                {
                    throw new InvalidDataException("version API の応答形式が不正です。");
                }

                var assets = new List<VersionApiAsset>();
                if (dto.Assets != null)
                {
                    foreach (VersionApiAssetDto asset in dto.Assets)
                    {
                        if (asset == null || string.IsNullOrWhiteSpace(asset.Name))
                        {
                            continue;
                        }

                        assets.Add(new VersionApiAsset
                        {
                            Name = asset.Name,
                            Size = asset.Size,
                            Digest = asset.Digest,
                        });
                    }
                }

                return new VersionApiResponse
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
        private sealed class VersionApiDto
        {
            /// <summary>リリースタグ</summary>
            [DataMember(Name = "tag")]
            public string? Tag { get; set; }

            /// <summary>アセット一覧</summary>
            [DataMember(Name = "assets")]
            public VersionApiAssetDto[]? Assets { get; set; }
        }

        [DataContract]
        private sealed class VersionApiAssetDto
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
