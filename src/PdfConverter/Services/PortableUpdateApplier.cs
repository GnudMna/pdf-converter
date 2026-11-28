using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfConverter.Infrastructure;

namespace PdfConverter.Services
{
    /// <summary>
    /// 展開済みの新しい版のポータブル版で、アプリフォルダーのファイルを置き換える
    /// </summary>
    /// <remarks>
    /// 旧版のマニフェストと新しい版のマニフェストに載るファイルを、いったんアプリフォルダー内の退避フォルダーへ移してから
    /// 新しい版のファイルを複製する。途中で失敗したら複製したファイルを消し、退避したファイルを元に戻す<br/>
    /// 退避は同じボリューム内の移動だけで済むよう、アプリフォルダー内に作る。利用者のデータ (<see cref="PortableMode.DataDirectoryName"/>) には触れない
    /// </remarks>
    public static class PortableUpdateApplier
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>置き換え前のファイルを退避するフォルダー名の接頭辞</summary>
        public const string BackupDirectoryPrefix = ".update-backup-";


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// アプリフォルダーのファイルを新しい版のファイルで置き換える
        /// </summary>
        /// <param name="sourceDirectory">展開済みの新しい版のフォルダー</param>
        /// <param name="targetDirectory">置き換えるポータブル版のアプリフォルダー</param>
        /// <param name="errorMessage">失敗時のメッセージ</param>
        /// <returns>置き換えられた場合は <c>true</c>。失敗した場合は元の状態に戻して <c>false</c></returns>
        public static bool TryApply(string sourceDirectory, string targetDirectory, out string? errorMessage)
        {
            ArgumentException.ThrowIfNullOrEmpty(sourceDirectory);
            ArgumentException.ThrowIfNullOrEmpty(targetDirectory);

            errorMessage = null;
            string source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceDirectory));
            string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDirectory));

            // 引数を取り違えて無関係なフォルダーを書き換えないよう、ポータブル版のフォルダーであることを確かめる
            if (!PortableMode.HasMarker(target)
                || !File.Exists(Path.Combine(target, PortableUpdatePackage.AppExecutableFileName)))
            {
                errorMessage = $"更新先がポータブル版の PDF Converter のフォルダーではありません: {target}";
                return false;
            }

            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "更新元と更新先が同じフォルダーです。";
                return false;
            }

            IReadOnlyList<string> newFiles;
            try
            {
                newFiles = PortableUpdatePackage.ReadManifest(source);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                errorMessage = $"新しい版のファイル一覧を読み込めません。\n\n{ex.Message}";
                return false;
            }

            foreach (string relativePath in newFiles)
            {
                if (!File.Exists(Path.Combine(source, relativePath)))
                {
                    errorMessage = $"新しい版のファイルが見つかりません: {relativePath}";
                    return false;
                }
            }

            // 旧版のマニフェストが読めなくても、新しい版と同じ名前のファイルは置き換えられる (旧版にしか無いファイルが残るだけ)
            IReadOnlyList<string> oldFiles = PortableUpdatePackage.TryReadManifest(target);

            var replacedFiles = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string relativePath in oldFiles.Concat(newFiles))
            {
                if (seen.Add(relativePath))
                {
                    replacedFiles.Add(relativePath);
                }
            }

            string backupDirectory = Path.Combine(target, BackupDirectoryPrefix + Guid.NewGuid().ToString("N"));
            var movedFiles = new List<string>();
            var copiedFiles = new List<string>();
            try
            {
                foreach (string relativePath in replacedFiles)
                {
                    string currentPath = Path.Combine(target, relativePath);
                    if (!File.Exists(currentPath))
                    {
                        continue;
                    }

                    string backupPath = Path.Combine(backupDirectory, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                    File.Move(currentPath, backupPath);
                    movedFiles.Add(relativePath);
                }

                foreach (string relativePath in newFiles)
                {
                    string destinationPath = Path.Combine(target, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    File.Copy(Path.Combine(source, relativePath), destinationPath, overwrite: false);
                    copiedFiles.Add(relativePath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ApplicationLogger.Error("Update", "Failed to replace the portable application files; rolling back", ex);
                bool restored = Rollback(target, backupDirectory, movedFiles, copiedFiles);
                errorMessage = restored
                    ? $"ファイルを置き換えられなかったため、更新を取り消しました。\n\n{ex.Message}"
                    : $"ファイルを置き換えられず、一部のファイルを元に戻せませんでした。退避したファイルは次のフォルダーにあります:\n{backupDirectory}\n\n{ex.Message}";
                return false;
            }

            TryDeleteDirectory(backupDirectory);
            RemoveEmptyDirectories(target, oldFiles);
            return true;
        }

        /// <summary>
        /// 前回の更新で消せなかった退避フォルダーを削除する (使用中で消せないファイルは残す)
        /// </summary>
        /// <param name="appDirectory">ポータブル版のアプリフォルダー</param>
        public static void CleanupLeftoverBackups(string appDirectory)
        {
            try
            {
                if (!Directory.Exists(appDirectory))
                {
                    return;
                }

                foreach (string directory in Directory.EnumerateDirectories(appDirectory, BackupDirectoryPrefix + "*"))
                {
                    TryDeleteDirectory(directory);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ApplicationLogger.Warning("Update", $"Failed to clean up portable update backups: {appDirectory}", ex);
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 複製したファイルを消し、退避したファイルを元の場所へ戻す
        /// </summary>
        /// <param name="target">アプリフォルダー</param>
        /// <param name="backupDirectory">退避フォルダー</param>
        /// <param name="movedFiles">退避したファイル</param>
        /// <param name="copiedFiles">複製したファイル</param>
        /// <returns>すべて元に戻せた場合は <c>true</c></returns>
        private static bool Rollback(
            string target,
            string backupDirectory,
            IReadOnlyList<string> movedFiles,
            IReadOnlyList<string> copiedFiles)
        {
            bool restored = true;
            foreach (string relativePath in copiedFiles)
            {
                try
                {
                    File.Delete(Path.Combine(target, relativePath));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    ApplicationLogger.Error("Update", $"Failed to remove a copied file during rollback: {relativePath}", ex);
                    restored = false;
                }
            }

            foreach (string relativePath in movedFiles)
            {
                try
                {
                    File.Move(Path.Combine(backupDirectory, relativePath), Path.Combine(target, relativePath), overwrite: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    ApplicationLogger.Error("Update", $"Failed to restore a file during rollback: {relativePath}", ex);
                    restored = false;
                }
            }

            if (restored)
            {
                TryDeleteDirectory(backupDirectory);
            }

            return restored;
        }

        /// <summary>
        /// 旧版のファイルを置いていたフォルダーのうち、空になったものを削除する
        /// </summary>
        /// <param name="target">アプリフォルダー</param>
        /// <param name="oldFiles">旧版のファイル (アプリフォルダーからの相対パス)</param>
        private static void RemoveEmptyDirectories(string target, IReadOnlyList<string> oldFiles)
        {
            foreach (string relativePath in oldFiles)
            {
                string? directory = Path.GetDirectoryName(Path.Combine(target, relativePath));
                while (!string.IsNullOrEmpty(directory)
                    && directory.Length > target.Length
                    && directory.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (!Directory.Exists(directory) || Directory.EnumerateFileSystemEntries(directory).Any())
                        {
                            break;
                        }

                        Directory.Delete(directory);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        break;
                    }

                    directory = Path.GetDirectoryName(directory);
                }
            }
        }

        /// <summary>
        /// フォルダーを中身ごと削除する (削除できなくても例外は送出しない)
        /// </summary>
        /// <param name="directory">削除するフォルダー</param>
        private static void TryDeleteDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ApplicationLogger.Warning("Update", $"Failed to delete a portable update backup: {directory}", ex);
            }
        }
    }
}
