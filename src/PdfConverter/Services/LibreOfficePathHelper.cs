using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PdfConverter.Services
{
    /// <summary>
    /// LibreOfficeの<c>soffice.exe</c>のパス解決
    /// </summary>
    public static class LibreOfficePathHelper
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 使用する<c>soffice.exe</c>の絶対パスを解決する
        /// </summary>
        /// <param name="configuredPath">ユーザー指定パス（空の場合は自動検出）</param>
        /// <returns><c>soffice.exe</c>の絶対パス</returns>
        public static string Resolve(string configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                string trimmedPath = configuredPath.Trim();
                if (File.Exists(trimmedPath))
                {
                    return trimmedPath;
                }

                throw new FileNotFoundException(
                    $"指定された LibreOffice 実行ファイルが見つかりません: {trimmedPath}",
                    trimmedPath);
            }

            foreach (string candidate in GetDefaultCandidates())
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            string fromPath = FindOnPath("soffice.exe");
            if (!string.IsNullOrEmpty(fromPath))
            {
                return fromPath;
            }

            throw new InvalidOperationException(
                "LibreOffice が見つかりません。LibreOffice をインストールするか、soffice.exe のパスを指定してください。");
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// Windows標準インストール先の候補一覧を返す
        /// </summary>
        /// <returns>候補パス一覧</returns>
        internal static IEnumerable<string> GetDefaultCandidates()
        {
            var installRoots = new List<string>();

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
            {
                installRoots.Add(programFiles);
            }

            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(programFilesX86))
            {
                installRoots.Add(programFilesX86);
            }

            // 32bit プロセスでは ProgramFiles が x86 側を指すことがあるため、64bit 側も明示的に試す
            string programW6432 = Environment.GetEnvironmentVariable("ProgramW6432");
            if (!string.IsNullOrEmpty(programW6432))
            {
                installRoots.Add(programW6432);
            }

            return installRoots
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(root => Path.Combine(root, "LibreOffice", "program", "soffice.exe"));
        }

        /// <summary>
        /// PATH環境変数から実行ファイルを検索する
        /// </summary>
        /// <param name="fileName">ファイル名</param>
        /// <returns>見つかった絶対パス。見つからない場合は<c>null</c></returns>
        private static string FindOnPath(string fileName)
        {
            string pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathVariable))
            {
                return null;
            }

            return pathVariable
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => Path.Combine(path.Trim(), fileName))
                .FirstOrDefault(File.Exists);
        }
    }
}
