using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// アプリのバージョン情報と同梱 OSS のクレジット表示用メタデータ
    /// </summary>
    public static class ApplicationMetadata
    {
        /********************************************************************************/
        /*                                  定数                                        */
        /********************************************************************************/
        /// <summary>ライセンス全文ファイル名</summary>
        public const string ThirdPartyNoticesFileName = "THIRD-PARTY-NOTICES.txt";

        private static readonly IReadOnlyList<ThirdPartyComponent> BundledComponentsList = new[]
        {
            new ThirdPartyComponent("Docnet.Core", "2.6.0", "MIT", "PDF レンダリング (.NET ラッパー)"),
            new ThirdPartyComponent("PDFium", "Docnet.Core 同梱", "Apache 2.0", "PDF ネイティブエンジン"),
            new ThirdPartyComponent("Microsoft.Extensions.DependencyInjection", "10.0.8", "MIT", "DI コンテナ"),
            new ThirdPartyComponent("Microsoft.Extensions.DependencyInjection.Abstractions", "10.0.8", "MIT", "DI 抽象化"),
            new ThirdPartyComponent("Microsoft.Bcl.AsyncInterfaces", "10.0.8", "MIT", "非同期インターフェース"),
            new ThirdPartyComponent("Ookii.Dialogs.Wpf", "5.0.1", "BSD-3-Clause", "フォルダー選択ダイアログ"),
            new ThirdPartyComponent("System.Runtime.CompilerServices.Unsafe", "6.1.2", "MIT", "低レベル型操作"),
            new ThirdPartyComponent("System.Threading.Tasks.Extensions", "4.6.3", "MIT", "タスク拡張"),
        };


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>製品名</summary>
        public static string ProductName =>
            GetAssemblyAttribute<AssemblyProductAttribute>()?.Product ?? "PDF Converter";

        /// <summary>表示用バージョン</summary>
        public static string Version =>
            GetAssemblyAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

        /// <summary>著作権表示</summary>
        public static string Copyright =>
            GetAssemblyAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;

        /// <summary>公開者名</summary>
        public static string Publisher =>
            GetAssemblyAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;

        /// <summary>アプリ本体のライセンス名</summary>
        public static string ApplicationLicenseName => "MIT License";

        /// <summary>同梱 OSS 一覧</summary>
        public static IReadOnlyList<ThirdPartyComponent> BundledComponents => BundledComponentsList;

        /// <summary>About ダイアログ向けの同梱 OSS 一覧テキスト</summary>
        public static string BundledComponentsText =>
            string.Join(
                Environment.NewLine,
                BundledComponentsList.Select(component => $"・{component.DisplayLine}"));

        /// <summary>外部依存ソフトウェアの説明</summary>
        public static string ExternalDependenciesText =>
            "・.NET Framework 4.8.1（ランタイム）" + Environment.NewLine +
            "・Microsoft Word または LibreOffice（Word 文書の変換時、別途インストールが必要）";

        /// <summary>ライセンス全文ファイルの絶対パス</summary>
        public static string ThirdPartyNoticesFilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ThirdPartyNoticesFileName);

        /// <summary>ライセンス全文ファイルが存在するかどうか</summary>
        public static bool IsThirdPartyNoticesAvailable =>
            File.Exists(ThirdPartyNoticesFilePath);


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        private static T GetAssemblyAttribute<T>()
            where T : Attribute
        {
            return typeof(ApplicationMetadata).Assembly.GetCustomAttribute<T>();
        }
    }

    /// <summary>
    /// 同梱オープンソースコンポーネントの表示用情報
    /// </summary>
    public sealed class ThirdPartyComponent
    {
        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        public ThirdPartyComponent(string name, string version, string license, string purpose)
        {
            Name = name;
            Version = version;
            License = license;
            Purpose = purpose;
        }


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>コンポーネント名</summary>
        public string Name { get; }

        /// <summary>バージョン</summary>
        public string Version { get; }

        /// <summary>ライセンス (SPDX 式)</summary>
        public string License { get; }

        /// <summary>用途</summary>
        public string Purpose { get; }

        /// <summary>About ダイアログ向けの 1 行表示</summary>
        public string DisplayLine => $"{Name} {Version} ({License}) — {Purpose}";
    }
}
