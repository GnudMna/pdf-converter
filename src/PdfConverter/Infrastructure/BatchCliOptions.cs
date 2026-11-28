using System.Collections.Generic;
using PdfConverter.Models;

namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// バッチ / ヘッドレスモードのコマンドライン引数
    /// </summary>
    public sealed class BatchCliOptions
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>ヘルプを表示して終了する</summary>
        public bool ShowHelp { get; set; }

        /// <summary>Word 文書を PDF として出力する (画像変換は行わない)</summary>
        public bool ExportPdfOnly { get; set; }

        /// <summary>既存ファイルを確認せず上書きする</summary>
        public bool OverwriteExisting { get; set; }

        /// <summary>進捗メッセージを抑制する</summary>
        public bool Quiet { get; set; }

        /// <summary>出力先フォルダーまたは PDF ファイルのパス</summary>
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>出力ページ範囲 (<c>null</c> の場合は全ページ)</summary>
        public string? PageRange { get; set; }

        /// <summary>出力画像形式 (<c>null</c> の場合は保存済み設定を使用)</summary>
        public OutputImageFormat? Format { get; set; }

        /// <summary>解像度の指定方法 (<c>null</c> の場合は保存済み設定を使用)</summary>
        public ResolutionMode? ResolutionMode { get; set; }

        /// <summary>解像度の数値 (<c>null</c> の場合は保存済み設定を使用)</summary>
        public string? ResolutionValue { get; set; }

        /// <summary>パスワードで保護された PDF を開くパスワード (<c>null</c> の場合は指定なし。すべての入力ファイルに使う)</summary>
        public string? Password { get; set; }

        /// <summary>パスワードを読み取るファイルのパス (<c>--password-file</c>。<c>null</c> の場合は指定なし)</summary>
        public string? PasswordFilePath { get; set; }

        /// <summary>パスワードを読み取る環境変数の名前 (<c>--password-env</c>。<c>null</c> の場合は指定なし)</summary>
        public string? PasswordEnvironmentVariable { get; set; }

        /// <summary>パスワードを標準入力から読み取るかどうか (<c>--password-stdin</c>)</summary>
        public bool PasswordFromStandardInput { get; set; }

        /// <summary>透明度を保持するかどうか (<c>null</c> の場合は保存済み設定を使用)</summary>
        public bool? PreserveTransparency { get; set; }

        /// <summary>JPEG 品質 (1〜100。<c>null</c> の場合は保存済み設定を使用)</summary>
        public int? JpegQuality { get; set; }

        /// <summary>変換対象の入力ファイル一覧</summary>
        public IList<string> InputFiles { get; } = new List<string>();
    }
}
