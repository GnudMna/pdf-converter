using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using PdfConverter.Infrastructure;
using PdfConverter.Services;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// ポータブル版のアプリフォルダーと zip をテスト用に作る
    /// </summary>
    internal static class PortablePackageTestHelper
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// ポータブル版として最低限必要なファイル (実行ファイル・マーカー) に、指定したファイルを加えた一覧を返す
        /// </summary>
        /// <param name="version">ファイルの内容に含める版 (置き換えの確認用)</param>
        /// <param name="extraFiles">追加するファイル (相対パス → 内容)</param>
        /// <returns>相対パス → 内容 (マニフェストは含まない)</returns>
        public static Dictionary<string, string> CreateAppFiles(string version, params (string Path, string Content)[] extraFiles)
        {
            var files = new Dictionary<string, string>
            {
                [PortableUpdatePackage.AppExecutableFileName] = $"exe {version}",
                [PortableMode.MarkerFileName] = "portable",
            };
            foreach ((string path, string content) in extraFiles)
            {
                files[path] = content;
            }

            return files;
        }

        /// <summary>
        /// ファイルとマニフェストをフォルダーへ書き出す
        /// </summary>
        /// <param name="directory">書き出し先</param>
        /// <param name="files">相対パス → 内容 (マニフェストは自動で加える)</param>
        public static void WriteAppDirectory(string directory, IReadOnlyDictionary<string, string> files)
        {
            foreach (KeyValuePair<string, string> file in WithManifest(files))
            {
                string path = Path.Combine(directory, file.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, file.Value);
            }
        }

        /// <summary>
        /// ファイルとマニフェストを含む zip を作る
        /// </summary>
        /// <param name="files">相対パス → 内容 (マニフェストは自動で加える)</param>
        /// <returns>zip のバイト列</returns>
        public static byte[] BuildZip(IReadOnlyDictionary<string, string> files)
        {
            return BuildRawZip(WithManifest(files));
        }

        /// <summary>
        /// 指定したエントリーだけを含む zip を作る (マニフェストは加えない)
        /// </summary>
        /// <param name="entries">エントリー名 → 内容</param>
        /// <returns>zip のバイト列</returns>
        public static byte[] BuildRawZip(IReadOnlyDictionary<string, string> entries)
        {
            using (var stream = new MemoryStream())
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (KeyValuePair<string, string> entry in entries)
                    {
                        ZipArchiveEntry zipEntry = archive.CreateEntry(entry.Key.Replace('\\', '/'));
                        using (var writer = new StreamWriter(zipEntry.Open(), new UTF8Encoding(false)))
                        {
                            writer.Write(entry.Value);
                        }
                    }
                }

                return stream.ToArray();
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>ファイル一覧にマニフェスト (自身を含む全ファイルの相対パス) を加える</summary>
        /// <param name="files">相対パス → 内容</param>
        /// <returns>マニフェストを加えた一覧</returns>
        private static Dictionary<string, string> WithManifest(IReadOnlyDictionary<string, string> files)
        {
            var result = files.ToDictionary(file => file.Key, file => file.Value);
            IEnumerable<string> paths = files.Keys.Append(PortableMode.ManifestFileName);
            result[PortableMode.ManifestFileName] = string.Join("\r\n", paths);
            return result;
        }
    }
}
