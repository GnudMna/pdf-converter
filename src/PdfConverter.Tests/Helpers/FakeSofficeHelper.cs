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

        /// <summary>成功時の PowerShell スクリプトの param 行</summary>
        private const string SuccessfulScriptParamLine = "param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)";


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

        /// <summary>
        /// 大量の標準出力 / 標準エラーを書き出してから成功する偽 <c>soffice.exe</c> ラッパーを作成する
        /// </summary>
        /// <returns>実行可能なラッパーファイルの絶対パス</returns>
        public static string CreateSuccessfulVerboseWrapper()
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-fake-soffice-{Guid.NewGuid():N}.ps1");
            string cmdPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-fake-soffice-{Guid.NewGuid():N}.cmd");
            string script = GetVerboseSuccessfulScriptContent();
            File.WriteAllText(scriptPath, script);
            File.WriteAllText(cmdPath, $"@powershell -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" %*{Environment.NewLine}");
            return cmdPath;
        }

        /// <summary>
        /// 受け取った <c>-env:UserInstallation</c> 引数を記録ファイルへ 1 行ずつ追記する偽 <c>soffice.exe</c> ラッパーを作成する
        /// </summary>
        /// <param name="recordPath">引数を追記する記録ファイルのパス</param>
        /// <param name="success">成功を模倣するかどうか</param>
        /// <returns>実行可能なラッパーファイルの絶対パス</returns>
        public static string CreateProfileRecordingWrapper(string recordPath, bool success)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-fake-soffice-{Guid.NewGuid():N}.ps1");
            string cmdPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-fake-soffice-{Guid.NewGuid():N}.cmd");
            // PowerShell の -File 起動では -env:値 が -env と 値 の 2 つに分割されて渡されるため、結合して記録する
            string recordLines = $@"
$profileArg = $null
for ($i = 0; $i -lt $Args.Count; $i++) {{
    $token = $Args[$i].Trim('""')
    if ($token.StartsWith('-env:UserInstallation=')) {{ $profileArg = $token; break }}
    if ($token -eq '-env' -and ($i + 1) -lt $Args.Count) {{ $profileArg = '-env:' + $Args[$i + 1].Trim('""'); break }}
}}
Add-Content -LiteralPath '{recordPath}' -Value $profileArg
";
            string script = success
                ? GetSuccessfulScriptContent().Replace(SuccessfulScriptParamLine, SuccessfulScriptParamLine + recordLines)
                : $"{SuccessfulScriptParamLine}{recordLines}exit {FailureExitCode}";
            File.WriteAllText(scriptPath, script);
            File.WriteAllText(cmdPath, $"@powershell -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" %*{Environment.NewLine}");
            return cmdPath;
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

        /// <summary>大量出力を伴う成功時の PowerShell スクリプト本文を返す</summary>
        private static string GetVerboseSuccessfulScriptContent()
        {
            return @"
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)
for ($i = 0; $i -lt 500; $i++) {
    [Console]::Out.WriteLine(""stdout line $i"")
    [Console]::Error.WriteLine(""stderr line $i"")
}
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
