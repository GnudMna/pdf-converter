using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using PdfConverter.Infrastructure;

namespace PdfConverter.Services
{
    /// <summary>
    /// ポータブル版の zip の展開と、ポータブル版を構成するファイルの一覧 (マニフェスト) の読み込み
    /// </summary>
    /// <remarks>
    /// zip は署名を検証してから展開するが、展開先の外への書き込みと過大な展開は署名とは別に拒否する<br/>
    /// マニフェスト (<see cref="PortableMode.ManifestFileName"/>) は <c>scripts/build-portable.ps1</c> が作り、
    /// 更新時に旧版のファイルを消すために使う。利用者のデータ (<see cref="PortableMode.DataDirectoryName"/>) は含めない
    /// </remarks>
    public static class PortableUpdatePackage
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>アプリ本体の実行ファイル名</summary>
        public const string AppExecutableFileName = "PDF Converter.exe";

        /// <summary>展開を許すエントリー数の上限</summary>
        internal const int MaxEntryCount = 10000;

        /// <summary>展開後の合計サイズの上限 (バイト)</summary>
        internal const long MaxExtractedBytes = 2L * 1024L * 1024L * 1024L;

        /// <summary>マニフェストの上限バイト数</summary>
        private const long MaxManifestBytes = 1024L * 1024L;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// zip を空のフォルダーへ展開し、ポータブル版として必要なファイルがそろっているか確かめる
        /// </summary>
        /// <param name="zipPath">zip のパス</param>
        /// <param name="destinationDirectory">展開先 (既にある場合は作り直す)</param>
        /// <exception cref="InvalidDataException">zip の内容がポータブル版として不正な場合</exception>
        public static void Extract(string zipPath, string destinationDirectory)
        {
            ArgumentException.ThrowIfNullOrEmpty(zipPath);
            ArgumentException.ThrowIfNullOrEmpty(destinationDirectory);

            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationDirectory));
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            Directory.CreateDirectory(root);

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                if (archive.Entries.Count > MaxEntryCount)
                {
                    throw new InvalidDataException("ポータブル版の zip のファイル数が上限を超えています。");
                }

                long totalBytes = 0;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    totalBytes += entry.Length;
                    if (entry.Length < 0 || totalBytes > MaxExtractedBytes)
                    {
                        throw new InvalidDataException("ポータブル版の zip の展開後のサイズが上限を超えています。");
                    }
                }

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string relativePath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    bool isDirectory = relativePath.EndsWith(Path.DirectorySeparatorChar);
                    string destinationPath = ResolveUnder(root, relativePath)
                        ?? throw new InvalidDataException($"ポータブル版の zip に不正なパスがあります: {entry.FullName}");

                    if (isDirectory)
                    {
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    entry.ExtractToFile(destinationPath, overwrite: false);
                }
            }

            foreach (string required in new[] { AppExecutableFileName, PortableMode.MarkerFileName, PortableMode.ManifestFileName })
            {
                if (!File.Exists(Path.Combine(root, required)))
                {
                    throw new InvalidDataException($"ポータブル版の zip に {required} がありません。");
                }
            }
        }

        /// <summary>
        /// アプリフォルダーのマニフェストを読み込む
        /// </summary>
        /// <param name="appDirectory">アプリフォルダー</param>
        /// <returns>アプリフォルダーからの相対パスの一覧 (重複なし)</returns>
        /// <exception cref="InvalidDataException">マニフェストが無い・大きすぎる・不正なパスを含む場合</exception>
        public static IReadOnlyList<string> ReadManifest(string appDirectory)
        {
            ArgumentException.ThrowIfNullOrEmpty(appDirectory);

            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(appDirectory));
            var manifestFile = new FileInfo(Path.Combine(root, PortableMode.ManifestFileName));
            if (!manifestFile.Exists)
            {
                throw new InvalidDataException($"{PortableMode.ManifestFileName} がありません: {root}");
            }

            if (manifestFile.Length > MaxManifestBytes)
            {
                throw new InvalidDataException($"{PortableMode.ManifestFileName} のサイズが上限を超えています。");
            }

            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadAllLines(manifestFile.FullName))
            {
                string relativePath = line.Trim().Replace('/', Path.DirectorySeparatorChar);
                if (relativePath.Length == 0)
                {
                    continue;
                }

                if (!IsAppFilePath(root, relativePath))
                {
                    throw new InvalidDataException($"{PortableMode.ManifestFileName} に不正なパスがあります: {line}");
                }

                if (seen.Add(relativePath))
                {
                    paths.Add(relativePath);
                }
            }

            return paths;
        }

        /// <summary>
        /// アプリフォルダーのマニフェストを読み込む。読み込めない場合は空の一覧を返す
        /// </summary>
        /// <param name="appDirectory">アプリフォルダー</param>
        /// <returns>アプリフォルダーからの相対パスの一覧</returns>
        public static IReadOnlyList<string> TryReadManifest(string appDirectory)
        {
            try
            {
                return ReadManifest(appDirectory);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                ApplicationLogger.Warning("Update", $"Failed to read the portable manifest: {appDirectory}", ex);
                return Array.Empty<string>();
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// マニフェストに載せてよいパス (アプリフォルダー内のファイルで、利用者のデータではない) か判定する
        /// </summary>
        /// <param name="root">アプリフォルダーの絶対パス</param>
        /// <param name="relativePath">アプリフォルダーからの相対パス</param>
        /// <returns>載せてよいパスなら <c>true</c></returns>
        private static bool IsAppFilePath(string root, string relativePath)
        {
            string[] segments = relativePath.Split(Path.DirectorySeparatorChar);
            if (Array.Exists(segments, segment => segment.Length == 0 || segment == "." || segment == "..")
                || ResolveUnder(root, relativePath) == null)
            {
                return false;
            }

            string firstSegment = segments[0];
            return !string.Equals(firstSegment, PortableMode.DataDirectoryName, StringComparison.OrdinalIgnoreCase)
                && !firstSegment.StartsWith(PortableUpdateApplier.BackupDirectoryPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 相対パスを基準フォルダー内の絶対パスに解決する
        /// </summary>
        /// <param name="root">基準フォルダーの絶対パス (末尾の区切り文字なし)</param>
        /// <param name="relativePath">相対パス</param>
        /// <returns>基準フォルダー内の絶対パス。外を指す・絶対パス・空の場合は <c>null</c></returns>
        private static string? ResolveUnder(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)
                || Path.IsPathRooted(relativePath)
                || relativePath.Contains(':'))
            {
                return null;
            }

            string fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
            return fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : null;
        }
    }
}
