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
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>同梱 OSS のライセンス全文ファイル名</summary>
        public const string ThirdPartyNoticesFileName = "THIRD-PARTY-NOTICES.txt";

        /// <summary>本アプリの使用許諾ファイル名 (インストール版に同梱)</summary>
        public const string LicenseFileName = "LICENSE.txt";

        /// <summary>利用者向けドキュメントのフォルダー名 (インストール版に同梱)</summary>
        public const string UserDocsDirectoryName = "docs";

        /// <summary>利用者向けドキュメントの表紙のファイル名</summary>
        public const string UserDocsIndexFileName = "index.html";

        /// <summary>self-contained 配布で同梱する .NET ランタイムのコンポーネント名</summary>
        public const string DotNetRuntimeComponentName = ".NET Runtime / Windows Desktop Runtime";

        /// <summary>
        /// 同梱 OSS 一覧 (配布物に含まれる NuGet パッケージと .NET ランタイム)
        /// </summary>
        /// <remarks>
        /// NuGet パッケージを追加・更新・削除したときはこの一覧と README の表も更新する<br/>
        /// 一覧と実際の依存パッケージのずれは単体テストで検出する
        /// </remarks>
        private static readonly IReadOnlyList<ThirdPartyComponent> BundledComponentsList = new[]
        {
            new ThirdPartyComponent("PDFiumCore", "157.0.8100", "Apache 2.0", "PDF レンダリング (.NET バインディング)"),
            new ThirdPartyComponent("PDFium", "157.0.8100", "BSD-3-Clause / Apache 2.0", "PDF ネイティブエンジン (pdfium-binaries)"),
            new ThirdPartyComponent("Microsoft.Extensions.DependencyInjection", "10.0.14", "MIT", "DI コンテナ"),
            new ThirdPartyComponent("Microsoft.Extensions.DependencyInjection.Abstractions", "10.0.14", "MIT", "DI 抽象化"),
            new ThirdPartyComponent("Ookii.Dialogs.Wpf", "5.0.1", "BSD-3-Clause", "フォルダー選択ダイアログ"),
            new ThirdPartyComponent(DotNetRuntimeComponentName, Environment.Version.ToString(), "MIT", "実行ランタイム (self-contained で同梱)"),
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

        /// <summary>公開者名 (AssemblyCompany は設定フォルダー名になるため、別の属性で持つ)</summary>
        public static string Publisher =>
            typeof(ApplicationMetadata).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => attribute.Key == "Publisher")?.Value ?? string.Empty;

        /// <summary>アプリ本体のライセンス名</summary>
        public static string ApplicationLicenseName => "All Rights Reserved";

        /// <summary>同梱 OSS 一覧</summary>
        public static IReadOnlyList<ThirdPartyComponent> BundledComponents => BundledComponentsList;

        /// <summary>About ダイアログ向けの同梱 OSS 一覧テキスト</summary>
        public static string BundledComponentsText =>
            string.Join(
                Environment.NewLine,
                BundledComponentsList.Select(component => $"・{component.DisplayLine}"));

        /// <summary>外部依存ソフトウェアの説明</summary>
        public static string ExternalDependenciesText =>
            "・Microsoft Word または LibreOffice (Word 文書の変換時、別途インストールが必要)";

        /// <summary>ライセンス全文ファイルの絶対パス</summary>
        public static string ThirdPartyNoticesFilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ThirdPartyNoticesFileName);

        /// <summary>ライセンス全文ファイルが存在するかどうか</summary>
        public static bool IsThirdPartyNoticesAvailable =>
            File.Exists(ThirdPartyNoticesFilePath);

        /// <summary>本アプリの使用許諾ファイルの絶対パス</summary>
        public static string LicenseFilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LicenseFileName);

        /// <summary>本アプリの使用許諾ファイルが存在するかどうか</summary>
        public static bool IsLicenseAvailable =>
            File.Exists(LicenseFilePath);

        /// <summary>利用者向けドキュメントの表紙の絶対パス</summary>
        public static string UserDocsIndexFilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, UserDocsDirectoryName, UserDocsIndexFileName);

        /// <summary>利用者向けドキュメントが存在するかどうか</summary>
        public static bool IsUserDocsAvailable =>
            File.Exists(UserDocsIndexFilePath);


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// このアセンブリに付与された属性を取得する
        /// </summary>
        /// <typeparam name="T">取得する属性の型</typeparam>
        /// <returns>属性。付与されていない場合は <c>null</c></returns>
        private static T? GetAssemblyAttribute<T>()
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
        /// <summary>
        /// コンポーネントの名前・バージョン・ライセンス・用途を指定して初期化する
        /// </summary>
        /// <param name="name">コンポーネント名</param>
        /// <param name="version">バージョン</param>
        /// <param name="license">ライセンス名</param>
        /// <param name="purpose">用途</param>
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

        /// <summary>ライセンス名 (複数ある場合は <c>/</c> 区切り)</summary>
        public string License { get; }

        /// <summary>用途</summary>
        public string Purpose { get; }

        /// <summary>About ダイアログ向けの 1 行表示</summary>
        public string DisplayLine => $"{Name} {Version} ({License}) — {Purpose}";
    }
}
