using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// バッチモードのパスワードを、<c>--password-file</c>・<c>--password-env</c>・<c>--password-stdin</c> の指定先から読み取る
    /// </summary>
    public static class BatchPasswordResolver
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 指定先からパスワードを読み取り、<see cref="BatchCliOptions.Password"/> へ設定する (指定がなければ何もしない)
        /// </summary>
        /// <param name="options">解析済みのオプション</param>
        /// <param name="openStandardInput">標準入力を開く処理 (<c>--password-stdin</c> のときだけ呼ぶ)</param>
        /// <param name="getEnvironmentVariable">環境変数を取得する処理</param>
        /// <param name="errorMessage">読み取り失敗時のメッセージ (成功時は <c>null</c>)</param>
        /// <returns>true: 成功 (指定がない場合を含む)</returns>
        public static bool TryResolve(
            BatchCliOptions options,
            Func<Stream> openStandardInput,
            Func<string, string?> getEnvironmentVariable,
            [NotNullWhen(false)] out string? errorMessage)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(openStandardInput);
            ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

            errorMessage = null;
            string? password;

            if (options.PasswordFilePath != null)
            {
                if (!TryReadFile(options.PasswordFilePath, out password, out errorMessage))
                {
                    return false;
                }
            }
            else if (options.PasswordEnvironmentVariable != null)
            {
                password = getEnvironmentVariable(options.PasswordEnvironmentVariable);
                if (string.IsNullOrEmpty(password))
                {
                    errorMessage = $"環境変数 {options.PasswordEnvironmentVariable} が設定されていないか、空です。";
                    return false;
                }
            }
            else if (options.PasswordFromStandardInput)
            {
                if (!TryReadStandardInput(openStandardInput, out password, out errorMessage))
                {
                    return false;
                }
            }
            else
            {
                return true;
            }

            options.Password = password;
            return true;
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>ファイルの 1 行目をパスワードとして読み取る</summary>
        /// <param name="path">パスワードファイルのパス</param>
        /// <param name="password">読み取ったパスワード</param>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>true: 読み取り成功</returns>
        private static bool TryReadFile(string path, [NotNullWhen(true)] out string? password, [NotNullWhen(false)] out string? errorMessage)
        {
            password = null;
            errorMessage = null;

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return TryReadFirstLine(stream, $"パスワードファイル {path}", out password, out errorMessage);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                errorMessage = $"パスワードファイルを読み取れませんでした: {path} ({ex.Message})";
                return false;
            }
        }

        /// <summary>標準入力の 1 行目をパスワードとして読み取る</summary>
        /// <param name="openStandardInput">標準入力を開く処理</param>
        /// <param name="password">読み取ったパスワード</param>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>true: 読み取り成功</returns>
        private static bool TryReadStandardInput(Func<Stream> openStandardInput, [NotNullWhen(true)] out string? password, [NotNullWhen(false)] out string? errorMessage)
        {
            password = null;
            errorMessage = null;

            try
            {
                using Stream stream = openStandardInput();
                return TryReadFirstLine(stream, "標準入力", out password, out errorMessage);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                errorMessage = $"標準入力からパスワードを読み取れませんでした: {ex.Message}";
                return false;
            }
        }

        /// <summary>ストリームの 1 行目 (UTF-8) を読み取る。行末の改行は含めず、前後の空白や引用符は加工しない</summary>
        /// <param name="stream">読み取り元</param>
        /// <param name="sourceName">エラーメッセージ用の読み取り元の名前</param>
        /// <param name="password">読み取ったパスワード</param>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>true: 読み取り成功 (1 行目が空の場合は失敗)</returns>
        private static bool TryReadFirstLine(Stream stream, string sourceName, [NotNullWhen(true)] out string? password, [NotNullWhen(false)] out string? errorMessage)
        {
            using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
            password = reader.ReadLine();
            if (string.IsNullOrEmpty(password))
            {
                password = null;
                errorMessage = $"{sourceName}にパスワードがありません (1 行目にパスワードを書いてください)。";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
