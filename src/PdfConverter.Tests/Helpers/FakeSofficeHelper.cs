using System;
using System.IO;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// LibreOffice 変換サービスのテスト用に、<c>soffice.exe</c> の振る舞いを模倣するラッパーを生成する
    /// </summary>
    internal static class FakeSofficeHelper
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>失敗を模倣する終了コード</summary>
        public const int FailureExitCode = 7;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 成功時に入力ファイル名に対応する PDF を出力ディレクトリへ書き出す偽 <c>soffice.exe</c> ラッパーを作成する
        /// </summary>
        /// <returns>実行可能なラッパーファイルの絶対パス</returns>
        public static string CreateSuccessfulWrapper()
        {
            return CreateWrapper(success: true);
        }

        /// <summary>
        /// 非ゼロ終了コードで失敗する偽 <c>soffice.exe</c> ラッパーを作成する
        /// </summary>
        /// <returns>実行可能なラッパーファイルの絶対パス</returns>
        public static string CreateFailingWrapper()
        {
            return CreateWrapper(success: false);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 一時ディレクトリに偽 <c>soffice.exe</c> ラッパーを作成する
        /// </summary>
        /// <param name="success">成功を模倣するかどうか</param>
        /// <returns>実行可能なラッパーファイルの絶対パス</returns>
        private static string CreateWrapper(bool success)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-fake-soffice-{Guid.NewGuid():N}.ps1");
            string cmdPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-fake-soffice-{Guid.NewGuid():N}.cmd");
            string script = success
                ? GetSuccessfulScriptContent()
                : $"exit {FailureExitCode}";
            File.WriteAllText(scriptPath, script);
            File.WriteAllText(cmdPath, $"@powershell -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" %*{Environment.NewLine}");
            return cmdPath;
        }

        /// <summary>成功時の PowerShell スクリプト本文を返す</summary>
        private static string GetSuccessfulScriptContent()
        {
            return @"
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)
$outDir = $null
$inputPath = $null
for ($i = 0; $i -lt $Args.Count; $i++) {
    $token = $Args[$i].Trim('""')
    if ($token -eq '--outdir' -and ($i + 1) -lt $Args.Count) {
        $outDir = $Args[$i + 1].Trim('""')
        $i++
    }
    elseif (-not $token.StartsWith('-')) {
        $inputPath = $token
    }
}
if (-not $outDir -or -not $inputPath) { exit 1 }
$name = [IO.Path]::GetFileNameWithoutExtension($inputPath)
[IO.File]::WriteAllText((Join-Path $outDir ""$name.pdf""), '%PDF-1.4 fake')
exit 0
";
        }
    }
}
