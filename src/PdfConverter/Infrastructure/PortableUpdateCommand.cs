using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using PdfConverter.Services;

namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// ポータブル版の更新処理 (新しい版の実行ファイルを、展開先から起動して行う) の起動引数と実行
    /// </summary>
    /// <remarks>
    /// 実行中の旧版は自身のファイルを置き換えられないため、展開した新しい版を
    /// <c>--apply-portable-update --target &lt;アプリフォルダー&gt; --wait-pid &lt;旧版のプロセス ID&gt;</c> で起動して終了する<br/>
    /// 新しい版は旧版の終了を待ってからアプリフォルダーのファイルを置き換え、置き換えた版を起動し直す<br/>
    /// 旧版が組み立てた引数を新しい版が解釈するため、引数の形式は版をまたいで変えない
    /// </remarks>
    public static class PortableUpdateCommand
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>ポータブル版の更新処理として起動することを示す引数</summary>
        public const string ApplyOption = "--apply-portable-update";

        /// <summary>置き換えるアプリフォルダーを指定する引数</summary>
        public const string TargetOption = "--target";

        /// <summary>終了を待つ旧版のプロセス ID を指定する引数</summary>
        public const string WaitPidOption = "--wait-pid";

        /// <summary>メッセージボックスの題名</summary>
        private const string Caption = "PDF Converter の更新";

        /// <summary>旧版の終了を待つ上限</summary>
        private static readonly TimeSpan ParentExitTimeout = TimeSpan.FromSeconds(60);

        /// <summary>同じフォルダーの他の PDF Converter の終了を待つ上限 (超えたら利用者に確認する)</summary>
        private static readonly TimeSpan OtherInstancesTimeout = TimeSpan.FromSeconds(10);

        /// <summary>同じフォルダーで動いているか確かめるプロセス名</summary>
        private static readonly string[] AppProcessNames = ["PDF Converter", "pdfconv"];


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 更新処理の起動引数を組み立てる
        /// </summary>
        /// <param name="targetDirectory">置き換えるアプリフォルダー</param>
        /// <param name="waitProcessId">終了を待つプロセス ID</param>
        /// <returns>起動引数 (1 要素が 1 引数)</returns>
        public static IReadOnlyList<string> BuildArguments(string targetDirectory, int waitProcessId) =>
            [
                ApplyOption,
                TargetOption,
                targetDirectory,
                WaitPidOption,
                waitProcessId.ToString(CultureInfo.InvariantCulture),
            ];

        /// <summary>
        /// 起動引数が更新処理の指定か判定し、指定を読み取る
        /// </summary>
        /// <param name="args">起動引数</param>
        /// <param name="options">更新処理の指定。更新処理でない・不正な場合は <c>null</c></param>
        /// <returns>先頭が <see cref="ApplyOption"/> なら <c>true</c> (指定が不正でも <c>true</c>)</returns>
        public static bool TryParse(string[] args, out PortableUpdateOptions? options)
        {
            options = null;
            if (args == null || args.Length == 0 || !string.Equals(args[0], ApplyOption, StringComparison.Ordinal))
            {
                return false;
            }

            string? target = null;
            int? waitPid = null;
            for (int i = 1; i + 1 < args.Length; i += 2)
            {
                if (string.Equals(args[i], TargetOption, StringComparison.Ordinal))
                {
                    target = args[i + 1];
                }
                else if (string.Equals(args[i], WaitPidOption, StringComparison.Ordinal)
                    && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int pid))
                {
                    waitPid = pid;
                }
            }

            if (!string.IsNullOrWhiteSpace(target) && Path.IsPathFullyQualified(target))
            {
                options = new PortableUpdateOptions(target, waitPid);
            }

            return true;
        }

        /// <summary>
        /// 旧版の終了を待ってアプリフォルダーのファイルを置き換え、置き換えた版を起動する
        /// </summary>
        /// <param name="options">更新処理の指定。不正な引数だった場合は <c>null</c></param>
        public static void Run(PortableUpdateOptions? options)
        {
            if (options == null)
            {
                ShowError("更新処理の起動引数が不正です。");
                return;
            }

            ApplicationLogger.LogDirectory = PortableMode.GetLogDirectory(options.TargetDirectory);
            ApplicationLogger.Info("Update", $"Applying portable update to {options.TargetDirectory}");

            if (options.WaitProcessId is int pid)
            {
                WaitForProcessExit(pid);
            }

            while (!WaitForOtherInstances(options.TargetDirectory))
            {
                MessageBoxResult answer = MessageBox.Show(
                    "同じフォルダーの PDF Converter (pdfconv.exe を含む) が動いているため、更新できません。\n\n終了してから [OK] を押してください。[キャンセル] を押すと更新を中止します。",
                    Caption,
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);
                if (answer != MessageBoxResult.OK)
                {
                    ApplicationLogger.Info("Update", "Portable update was cancelled by the user.");
                    StartApp(options.TargetDirectory);
                    return;
                }
            }

            if (PortableUpdateApplier.TryApply(PortableMode.AppDirectory, options.TargetDirectory, out string? errorMessage))
            {
                ApplicationLogger.Info("Update", "Portable update was applied.");
            }
            else
            {
                ApplicationLogger.Error("Update", $"Portable update failed: {errorMessage}");
                ShowError(errorMessage ?? "更新に失敗しました。");
            }

            StartApp(options.TargetDirectory);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>旧版のプロセスの終了を待つ (上限を超えたら待たずに進め、置き換えの失敗に任せる)</summary>
        /// <param name="processId">プロセス ID</param>
        private static void WaitForProcessExit(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    if (!process.WaitForExit(ParentExitTimeout))
                    {
                        ApplicationLogger.Warning("Update", $"The previous process did not exit in time: {processId}");
                    }
                }
            }
            catch (ArgumentException)
            {
                // 既に終了している
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                ApplicationLogger.Warning("Update", $"Failed to wait for the previous process: {processId}", ex);
            }
        }

        /// <summary>
        /// アプリフォルダーから起動された他の PDF Converter が終了するのを、一定時間待つ
        /// </summary>
        /// <param name="targetDirectory">アプリフォルダー</param>
        /// <returns>動いているものが無くなった場合は <c>true</c></returns>
        private static bool WaitForOtherInstances(string targetDirectory)
        {
            DateTime deadline = DateTime.UtcNow + OtherInstancesTimeout;
            while (IsAnyInstanceRunning(targetDirectory))
            {
                if (DateTime.UtcNow >= deadline)
                {
                    return false;
                }

                Thread.Sleep(500);
            }

            return true;
        }

        /// <summary>アプリフォルダーから起動されたプロセス (この更新処理を除く) があるか調べる</summary>
        /// <param name="targetDirectory">アプリフォルダー</param>
        /// <returns>あれば <c>true</c></returns>
        private static bool IsAnyInstanceRunning(string targetDirectory)
        {
            string prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDirectory)) + Path.DirectorySeparatorChar;
            int ownId = Environment.ProcessId;
            foreach (string name in AppProcessNames)
            {
                foreach (Process process in Process.GetProcessesByName(name))
                {
                    using (process)
                    {
                        if (process.Id != ownId && IsUnder(process, prefix))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>プロセスの実行ファイルが指定したフォルダー直下にあるか判定する</summary>
        /// <param name="process">プロセス</param>
        /// <param name="directoryPrefix">フォルダーの絶対パス (末尾に区切り文字付き)</param>
        /// <returns>フォルダー直下なら <c>true</c>。調べられない場合は <c>false</c></returns>
        private static bool IsUnder(Process process, string directoryPrefix)
        {
            try
            {
                string? path = process.MainModule?.FileName;
                return path != null
                    && string.Equals(Path.GetDirectoryName(path) + Path.DirectorySeparatorChar, directoryPrefix, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
            {
                // 権限・ビット数の違い・終了済みで調べられないプロセスは、同じフォルダーのものとみなさない
                return false;
            }
        }

        /// <summary>アプリフォルダーのアプリを起動する</summary>
        /// <param name="targetDirectory">アプリフォルダー</param>
        private static void StartApp(string targetDirectory)
        {
            string appPath = Path.Combine(targetDirectory, PortableUpdatePackage.AppExecutableFileName);
            try
            {
                using (Process.Start(new ProcessStartInfo(appPath)
                {
                    WorkingDirectory = targetDirectory,
                    UseShellExecute = true,
                }))
                {
                }
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
            {
                ApplicationLogger.Error("Update", $"Failed to restart the application: {appPath}", ex);
                ShowError($"PDF Converter を起動できませんでした。\n\n{appPath}\n\n{ex.Message}");
            }
        }

        /// <summary>エラーをメッセージボックスで表示する</summary>
        /// <param name="message">メッセージ</param>
        private static void ShowError(string message)
        {
            MessageBox.Show(message, Caption, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// ポータブル版の更新処理の指定
    /// </summary>
    /// <param name="TargetDirectory">置き換えるアプリフォルダー (絶対パス)</param>
    /// <param name="WaitProcessId">終了を待つ旧版のプロセス ID (指定が無い場合は <c>null</c>)</param>
    public sealed record PortableUpdateOptions(string TargetDirectory, int? WaitProcessId);
}
