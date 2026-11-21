using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PdfConverter.Models;
using PdfConverter.Services;

namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// バッチ / ヘッドレスモード向けのコマンドライン引数解析
    /// </summary>
    public static class BatchCliParser
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>引数不正時の終了コード</summary>
        public const int InvalidArgumentsExitCode = 2;

        /// <summary>実行時エラー時の終了コード</summary>
        public const int RuntimeErrorExitCode = 1;

        /// <summary>成功時の終了コード</summary>
        public const int SuccessExitCode = 0;

        /// <summary>パスの区切り文字 (<c>/</c> で始まる引数をスイッチかパスか判別するために使う)</summary>
        private static readonly char[] PathSeparators = { '/', '\\' };


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 起動引数がバッチ / ヘッドレスモードかどうかを判定し、必要なら解析する
        /// </summary>
        /// <param name="args">起動引数</param>
        /// <param name="options">解析結果 (GUI 起動の場合と解析失敗時は <c>null</c>)</param>
        /// <param name="errorMessage">解析失敗時のメッセージ (成功時と GUI 起動の場合は <c>null</c>)</param>
        /// <returns>true: バッチモードとして処理する (解析失敗を含む) / false: GUI 起動</returns>
        public static bool TryParse(string[] args, out BatchCliOptions? options, out string? errorMessage)
        {
            options = null;
            errorMessage = null;

            if (args == null || args.Length == 0)
            {
                return false;
            }

            if (!ContainsBatchSwitch(args))
            {
                return false;
            }

            options = new BatchCliOptions();
            bool parsingOptions = true;

            for (int index = 0; index < args.Length; index++)
            {
                string arg = args[index];
                if (string.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                if (parsingOptions && arg == "--")
                {
                    parsingOptions = false;
                    continue;
                }

                if (parsingOptions && IsSwitch(arg))
                {
                    if (!TryParseSwitch(args, ref index, options, out errorMessage))
                    {
                        options = null;
                        return true;
                    }

                    continue;
                }

                options.InputFiles.Add(arg.Trim().Trim('"'));
            }

            if (!Validate(options, out errorMessage))
            {
                options = null;
                return true;
            }

            return true;
        }

        /// <summary>バッチモードのヘルプテキストを返す</summary>
        /// <returns>ヘルプテキスト</returns>
        public static string GetHelpText()
        {
            return @"PDF Converter — バッチ / ヘッドレスモード

使い方:
  ""PDF Converter.exe"" --output <フォルダー> [オプション] <ファイル> [<ファイル> ...]
  ""PDF Converter.exe"" --export-pdf --output <フォルダーまたは.pdf> <Wordファイル> [...]

必須:
  --output, -o <path>       画像の出力先フォルダー、または PDF 出力先

オプション:
  --pages, -p <範囲>        出力ページ (例: 1-3,5)。省略時は全ページ
  --format, -f <形式>       png / jpeg / bmp (省略時は保存済み設定)
  --resolution-mode <方式>  default / width / height / dpi (省略時は保存済み設定)
  --resolution, -r <数値>   解像度の数値 (省略時は保存済み設定)
  --transparent             透明度を保持 (PNG のみ有効)
  --no-transparent          白背景に合成して出力
  --password <文字列>       パスワード付き PDF を開くパスワード (全入力ファイルに使用)
                            値は加工せず、次の引数を常にパスワードとして扱う
  --overwrite               既存ファイルを確認せず上書き
  --export-pdf              Word を PDF として保存 (画像変換は行わない)
  --quiet, -q               進捗メッセージを抑制
  --help, -h, /?, --?        このヘルプを表示

例:
  ""PDF Converter.exe"" -o ""C:\out"" ""C:\docs\report.pdf""
  ""PDF Converter.exe"" -o ""C:\out"" -f png -p 1-3 ""report.pdf""
  ""PDF Converter.exe"" --export-pdf -o ""C:\out"" ""proposal.docx""
  ""PDF Converter.exe"" -o ""C:\out"" -- ""C:\docs\a.pdf"" ""C:\docs\b.pdf""

複数ファイルを同じ出力フォルダーへ保存する場合、各ファイルは
<出力フォルダー>\<ファイル名>\page_1.png のようにサブフォルダーへ出力されます。
別フォルダーの同名ファイルなど、出力先が重なる場合は 2 件目以降に _2、_3 を付けます (例: x、x_2)。
入力が 1 件のみの場合は <出力フォルダー>\page_1.png 形式です。
ページ番号は総ページ数の桁数までゼロ埋めされます (例: 12 ページなら page_01.png)。

GUI モードでは従来どおり、引数にファイルパスを指定するだけで起動できます。";
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>バッチモードを示すスイッチが含まれるかどうかを判定する</summary>
        /// <param name="args">起動引数</param>
        /// <returns>true: バッチモードとして扱う</returns>
        private static bool ContainsBatchSwitch(string[] args)
        {
            foreach (string arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                if (IsSwitch(arg))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>スイッチ引数かどうかを判定する</summary>
        /// <remarks><c>/</c> で始まる引数は、<c>//server/share/a.pdf</c> や <c>/docs/a.pdf</c> のようにスイッチ名の部分に区切り文字を含むとパスとして扱う</remarks>
        /// <param name="arg">引数</param>
        /// <returns>true: スイッチ引数</returns>
        private static bool IsSwitch(string arg)
        {
            if (arg.StartsWith('-'))
            {
                return true;
            }

            if (!arg.StartsWith('/'))
            {
                return false;
            }

            int valueIndex = arg.IndexOf('=');
            string namePart = valueIndex < 0 ? arg : arg.Substring(0, valueIndex);
            return namePart.IndexOfAny(PathSeparators, 1) < 0;
        }

        /// <summary>スイッチ引数を解析する</summary>
        /// <param name="args">起動引数</param>
        /// <param name="index">現在のインデックス</param>
        /// <param name="options">解析結果</param>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>true: 解析成功</returns>
        private static bool TryParseSwitch(string[] args, ref int index, BatchCliOptions options, [NotNullWhen(false)] out string? errorMessage)
        {
            errorMessage = null;
            string arg = args[index];
            string switchName = NormalizeSwitchName(arg);
            string? value = null;

            if (TrySplitSwitchValue(arg, out string? inlineName, out string? inlineValue))
            {
                switchName = inlineName;

                // パスワードは前後の空白や " も値の一部なので、加工せずそのまま渡す
                value = switchName == "password" ? inlineValue : TrimOptionValue(inlineValue);
            }

            switch (switchName)
            {
                case "help":
                case "h":
                case "?":
                    if (!TryReadFlagValue(arg, value, out bool showHelp, out errorMessage))
                    {
                        return false;
                    }

                    options.ShowHelp = showHelp;
                    return true;
                case "batch":
                case "b":
                    return TryReadFlagValue(arg, value, out _, out errorMessage);
                case "quiet":
                case "q":
                    if (!TryReadFlagValue(arg, value, out bool quiet, out errorMessage))
                    {
                        return false;
                    }

                    options.Quiet = quiet;
                    return true;
                case "overwrite":
                    if (!TryReadFlagValue(arg, value, out bool overwrite, out errorMessage))
                    {
                        return false;
                    }

                    options.OverwriteExisting = overwrite;
                    return true;
                case "export-pdf":
                    if (!TryReadFlagValue(arg, value, out bool exportPdf, out errorMessage))
                    {
                        return false;
                    }

                    options.ExportPdfOnly = exportPdf;
                    return true;
                case "transparent":
                    if (!TryReadFlagValue(arg, value, out bool transparent, out errorMessage))
                    {
                        return false;
                    }

                    options.PreserveTransparency = transparent;
                    return true;
                case "no-transparent":
                    if (!TryReadFlagValue(arg, value, out bool noTransparent, out errorMessage))
                    {
                        return false;
                    }

                    options.PreserveTransparency = !noTransparent;
                    return true;
                case "output":
                case "o":
                    if (!TryReadOptionValue(args, ref index, value, out string? outputPath, out errorMessage))
                    {
                        return false;
                    }

                    options.OutputPath = outputPath;
                    return true;
                case "pages":
                case "p":
                    if (!TryReadOptionValue(args, ref index, value, out string? pageRange, out errorMessage))
                    {
                        return false;
                    }

                    options.PageRange = pageRange;
                    return true;
                case "format":
                case "f":
                    if (!TryReadOptionValue(args, ref index, value, out string? formatValue, out errorMessage))
                    {
                        return false;
                    }

                    if (!TryParseFormat(formatValue, out OutputImageFormat format, out errorMessage))
                    {
                        return false;
                    }

                    options.Format = format;
                    return true;
                case "password":
                    if (!TryReadPasswordValue(args, ref index, value, out string? password, out errorMessage))
                    {
                        return false;
                    }

                    options.Password = password;
                    return true;
                case "resolution-mode":
                    if (!TryReadOptionValue(args, ref index, value, out string? modeValue, out errorMessage))
                    {
                        return false;
                    }

                    if (!TryParseResolutionMode(modeValue, out ResolutionMode mode, out errorMessage))
                    {
                        return false;
                    }

                    options.ResolutionMode = mode;
                    return true;
                case "resolution":
                case "r":
                    if (!TryReadOptionValue(args, ref index, value, out string? resolutionValue, out errorMessage))
                    {
                        return false;
                    }

                    options.ResolutionValue = resolutionValue;
                    return true;
                default:
                    errorMessage = $"不明なオプションです: {arg}";
                    return false;
            }
        }

        /// <summary>スイッチ名を正規化する</summary>
        /// <param name="arg">引数</param>
        /// <returns>正規化後のスイッチ名</returns>
        private static string NormalizeSwitchName(string arg)
        {
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                return arg.Substring(2).Trim().ToLowerInvariant();
            }

            if (arg.StartsWith('/'))
            {
                return arg.Substring(1).Trim().ToLowerInvariant();
            }

            if (arg.StartsWith('-'))
            {
                return arg.Substring(1).Trim().ToLowerInvariant();
            }

            return arg.Trim().ToLowerInvariant();
        }

        /// <summary><c>-o=value</c> 形式のスイッチを分割する</summary>
        /// <param name="arg">引数</param>
        /// <param name="switchName">スイッチ名</param>
        /// <param name="value">値</param>
        /// <returns>true: 分割できた</returns>
        private static bool TrySplitSwitchValue(string arg, [NotNullWhen(true)] out string? switchName, [NotNullWhen(true)] out string? value)
        {
            switchName = null;
            value = null;

            int separatorIndex = arg.IndexOf('=');
            if (separatorIndex <= 1)
            {
                return false;
            }

            switchName = NormalizeSwitchName(arg.Substring(0, separatorIndex));
            value = arg.Substring(separatorIndex + 1);
            return true;
        }

        /// <summary>オプションの値の前後の空白と二重引用符を取り除く</summary>
        /// <param name="value">値</param>
        /// <returns>加工後の値</returns>
        private static string TrimOptionValue(string value)
        {
            return value.Trim().Trim('"');
        }

        /// <summary>フラグ系スイッチの値を読み取る (<c>--overwrite=false</c> 形式の値を反映する)</summary>
        /// <param name="arg">引数</param>
        /// <param name="inlineValue">同一引数内の値 (値の指定がなければ null)</param>
        /// <param name="flag">フラグの値 (値の指定がなければ true)</param>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>true: 読み取り成功</returns>
        private static bool TryReadFlagValue(
            string arg,
            string? inlineValue,
            out bool flag,
            [NotNullWhen(false)] out string? errorMessage)
        {
            errorMessage = null;
            flag = true;

            if (inlineValue == null)
            {
                return true;
            }

            if (!bool.TryParse(inlineValue, out flag))
            {
                errorMessage = $"オプション {arg} の値は true または false で指定してください。";
                return false;
            }

            return true;
        }

        /// <summary>オプションの値を読み取る</summary>
        /// <param name="args">起動引数</param>
        /// <param name="index">現在のインデックス</param>
        /// <param name="inlineValue">同一引数内の値</param>
        /// <param name="value">読み取った値</param>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>true: 読み取り成功</returns>
        private static bool TryReadOptionValue(
            string[] args,
            ref int index,
            string? inlineValue,
            [NotNullWhen(true)] out string? value,
            [NotNullWhen(false)] out string? errorMessage)
        {
            errorMessage = null;
            value = null;

            if (!string.IsNullOrWhiteSpace(inlineValue))
            {
                value = TrimOptionValue(inlineValue);
                return true;
            }

            if (index + 1 >= args.Length || IsSwitch(args[index + 1]))
            {
                errorMessage = $"オプション {args[index]} には値が必要です。";
                return false;
            }

            index++;
            value = TrimOptionValue(args[index]);
            return true;
        }

        /// <summary>パスワードの値を読み取る (前後の空白・二重引用符を取り除かず、<c>-</c> や <c>/</c> で始まる値も受け付ける)</summary>
        /// <remarks>別引数で指定した場合は、次の引数をスイッチかどうかにかかわらず値として扱う</remarks>
        /// <param name="args">起動引数</param>
        /// <param name="index">現在のインデックス</param>
        /// <param name="inlineValue">同一引数内の値</param>
        /// <param name="value">読み取った値</param>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>true: 読み取り成功</returns>
        private static bool TryReadPasswordValue(
            string[] args,
            ref int index,
            string? inlineValue,
            [NotNullWhen(true)] out string? value,
            [NotNullWhen(false)] out string? errorMessage)
        {
            errorMessage = null;
            value = inlineValue;
            string optionArg = args[index];

            if (value == null)
            {
                if (index + 1 >= args.Length)
                {
                    errorMessage = $"オプション {optionArg} には値が必要です。";
                    return false;
                }

                index++;
                value = args[index];
            }

            if (value.Length == 0)
            {
                errorMessage = $"オプション {optionArg} には値が必要です。";
                return false;
            }

            return true;
        }

        /// <summary>出力形式を解析する</summary>
        /// <param name="value">入力文字列</param>
        /// <param name="format">出力形式</param>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>true: 解析成功</returns>
        private static bool TryParseFormat(string value, out OutputImageFormat format, [NotNullWhen(false)] out string? errorMessage)
        {
            format = OutputImageFormat.Png;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                errorMessage = "出力形式を指定してください (png / jpeg / bmp)。";
                return false;
            }

            string normalized = value.Trim();
            if (string.Equals(normalized, "jpg", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "Jpeg";
            }

            if (Enum.TryParse(normalized, ignoreCase: true, result: out format) && Enum.IsDefined(format))
            {
                return true;
            }

            errorMessage = $"未対応の出力形式です: {value} (png / jpeg / bmp を指定してください)";
            return false;
        }

        /// <summary>解像度モードを解析する</summary>
        /// <param name="value">入力文字列</param>
        /// <param name="mode">解像度モード</param>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>true: 解析成功</returns>
        private static bool TryParseResolutionMode(string value, out ResolutionMode mode, [NotNullWhen(false)] out string? errorMessage)
        {
            mode = ResolutionMode.Width;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                errorMessage = "解像度モードを指定してください (default / width / height / dpi)。";
                return false;
            }

            if (Enum.TryParse(value.Trim(), ignoreCase: true, result: out mode) && Enum.IsDefined(mode))
            {
                return true;
            }

            errorMessage = $"未対応の解像度モードです: {value} (default / width / height / dpi を指定してください)";
            return false;
        }

        /// <summary>解析結果を検証する</summary>
        /// <param name="options">解析結果</param>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>true: 検証成功</returns>
        private static bool Validate(BatchCliOptions options, [NotNullWhen(false)] out string? errorMessage)
        {
            errorMessage = null;

            if (options.ShowHelp)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(options.OutputPath))
            {
                errorMessage = "出力先を --output で指定してください。";
                return false;
            }

            if (options.InputFiles.Count == 0)
            {
                errorMessage = "変換対象の PDF / Word ファイルを 1 件以上指定してください。";
                return false;
            }

            if (options.ExportPdfOnly && !string.IsNullOrWhiteSpace(options.PageRange))
            {
                errorMessage = "--export-pdf では --pages は使用できません。";
                return false;
            }

            if (!options.ExportPdfOnly
                && options.Format.HasValue
                && options.PreserveTransparency == true
                && !ImageBitmapHelper.SupportsTransparency(options.Format.Value))
            {
                errorMessage = "選択した出力形式では --transparent は使用できません。";
                return false;
            }

            return true;
        }
    }
}
