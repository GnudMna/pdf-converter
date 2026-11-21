using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PdfConverter.Services
{
    /// <summary>
    /// セマンティックバージョンの正規化と比較 (major.minor.patch)
    /// </summary>
    public static class SemVerComparer
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 先頭の <c>v</c> / <c>V</c> を除去する
        /// </summary>
        /// <param name="value">タグまたはバージョン文字列</param>
        /// <returns>プレフィックス除去後の文字列</returns>
        public static string StripVersionPrefix(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            if (value[0] == 'v' || value[0] == 'V')
            {
                return value.Substring(1);
            }

            return value;
        }

        /// <summary>
        /// バージョン文字列を解析する (<c>v</c> 付き可)
        /// </summary>
        /// <param name="value">バージョン文字列</param>
        /// <param name="version">解析結果</param>
        /// <returns>解析に成功すれば <c>true</c></returns>
        public static bool TryParse(string? value, [NotNullWhen(true)] out Version? version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = StripVersionPrefix(value.Trim());
            int plusIndex = normalized.IndexOf('+');
            if (plusIndex >= 0)
            {
                normalized = normalized.Substring(0, plusIndex);
            }

            if (normalized.Contains('-'))
            {
                // プレリリース付きは非対応 (数値部のみでは誤解を招くため失敗とする)
                return false;
            }

            string[] parts = normalized.Split('.');
            if (parts.Length < 2 || parts.Length > 4)
            {
                return false;
            }

            if (!TryParseComponent(parts[0], out int major) ||
                !TryParseComponent(parts[1], out int minor))
            {
                return false;
            }

            int build = 0;
            int revision = 0;
            if (parts.Length >= 3 && !TryParseComponent(parts[2], out build))
            {
                return false;
            }

            if (parts.Length == 4 && !TryParseComponent(parts[3], out revision))
            {
                return false;
            }

            version = parts.Length switch
            {
                2 => new Version(major, minor),
                3 => new Version(major, minor, build),
                _ => new Version(major, minor, build, revision),
            };
            return true;
        }

        /// <summary>
        /// <paramref name="left"/> が <paramref name="right"/> より新しいか判定する
        /// </summary>
        /// <param name="left">左側</param>
        /// <param name="right">右側</param>
        /// <returns>left &gt; right なら <c>true</c></returns>
        public static bool IsNewer(Version left, Version right)
        {
            ArgumentNullException.ThrowIfNull(left);
            ArgumentNullException.ThrowIfNull(right);

            return Normalize(left).CompareTo(Normalize(right)) > 0;
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        private static bool TryParseComponent(string text, out int value) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0;

        private static Version Normalize(Version version) =>
            new Version(
                version.Major,
                version.Minor,
                version.Build < 0 ? 0 : version.Build,
                version.Revision < 0 ? 0 : version.Revision);
    }
}
