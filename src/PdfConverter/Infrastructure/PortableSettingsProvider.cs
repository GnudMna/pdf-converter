using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// ポータブル版のユーザー設定を、アプリフォルダー内の 1 つの XML ファイルに保存する
    /// </summary>
    /// <remarks>
    /// .NET 既定の <see cref="LocalFileSettingsProvider"/> は <c>%LOCALAPPDATA%</c> の版ごとのフォルダーに保存するため、
    /// ポータブル版ではこのプロバイダーに差し替える (<c>Settings</c> のコンストラクター)<br/>
    /// ファイルは版によらず 1 つのため、前の版からの引き継ぎ (<c>Upgrade</c>) は何もしない<br/>
    /// 読み込めないファイルは <see cref="ConfigurationErrorsException"/> (ファイル名付き) として報告し、
    /// 既定のプロバイダーと同じく <see cref="UserSettingsInitializer"/> の破損回復に任せる
    /// </remarks>
    public sealed class PortableSettingsProvider : SettingsProvider, IApplicationSettingsProvider
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>プロバイダー名</summary>
        public const string ProviderName = nameof(PortableSettingsProvider);

        /// <summary>ルート要素名</summary>
        private const string RootElementName = "settings";

        /// <summary>設定値の要素名</summary>
        private const string SettingElementName = "setting";

        /// <summary>設定名の属性名</summary>
        private const string NameAttributeName = "name";


        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>設定ファイルの絶対パス</summary>
        private readonly string _filePath;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 保存先のファイルを指定して初期化する
        /// </summary>
        /// <param name="filePath">設定ファイルの絶対パス</param>
        public PortableSettingsProvider(string filePath)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            _filePath = filePath;
            base.Initialize(ProviderName, null);
        }


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <inheritdoc/>
        public override string ApplicationName { get; set; } = ApplicationMetadata.ProductName;

        /// <summary>設定ファイルの絶対パス</summary>
        public string FilePath => _filePath;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        /// <exception cref="ConfigurationErrorsException">設定ファイルを読み込めない場合</exception>
        public override SettingsPropertyValueCollection GetPropertyValues(
            SettingsContext context,
            SettingsPropertyCollection collection)
        {
            ArgumentNullException.ThrowIfNull(collection);

            Dictionary<string, string> stored = ReadValues();
            var values = new SettingsPropertyValueCollection();
            foreach (SettingsProperty property in collection)
            {
                values.Add(CreateValue(property, stored));
            }

            return values;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 保存できない場所 (読み取り専用のメディアなど) でも操作を続けられるよう、書き込みの失敗はログに残して例外を送出しない
        /// </remarks>
        public override void SetPropertyValues(
            SettingsContext context,
            SettingsPropertyValueCollection collection)
        {
            ArgumentNullException.ThrowIfNull(collection);

            Dictionary<string, string> stored;
            try
            {
                stored = ReadValues();
            }
            catch (ConfigurationErrorsException ex)
            {
                // 保存時に壊れていた場合は、今回の値で作り直す
                ApplicationLogger.Warning("Settings", $"ユーザー設定ファイルを読み込めないため作り直します: {_filePath}", ex);
                stored = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            foreach (SettingsPropertyValue value in collection)
            {
                if (!IsUserScoped(value.Property))
                {
                    continue;
                }

                // 文字列以外 (バイナリ形式) の直列化は使っていないため保存しない
                switch (value.SerializedValue)
                {
                    case string text:
                        stored[value.Name] = text;
                        break;

                    case null:
                        stored.Remove(value.Name);
                        break;
                }
            }

            try
            {
                WriteValues(stored);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ApplicationLogger.Error("Settings", $"Failed to save portable user settings: {_filePath}", ex);
            }
        }

        /// <inheritdoc/>
        public SettingsPropertyValue GetPreviousVersion(SettingsContext context, SettingsProperty property)
        {
            ArgumentNullException.ThrowIfNull(property);

            // 版によらず同じファイルを使うため、前の版の値は現在の値と同じ
            return CreateValue(property, ReadValues());
        }

        /// <inheritdoc/>
        public void Reset(SettingsContext context)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ApplicationLogger.Error("Settings", $"Failed to reset portable user settings: {_filePath}", ex);
            }
        }

        /// <inheritdoc/>
        public void Upgrade(SettingsContext context, SettingsPropertyCollection properties)
        {
            // 版によらず同じファイルを使うため、引き継ぐものはない
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 保存済みの値があれば反映した設定値を作る
        /// </summary>
        /// <param name="property">設定項目</param>
        /// <param name="stored">保存済みの値 (設定名 → 直列化した値)</param>
        /// <returns>設定値。保存済みの値が無ければ既定値を返す</returns>
        private static SettingsPropertyValue CreateValue(
            SettingsProperty property,
            Dictionary<string, string> stored)
        {
            var value = new SettingsPropertyValue(property)
            {
                IsDirty = false,
            };

            if (IsUserScoped(property) && stored.TryGetValue(property.Name, out string? serialized))
            {
                value.SerializedValue = serialized;
            }

            return value;
        }

        /// <summary>ユーザー単位の設定項目か判定する</summary>
        /// <param name="property">設定項目</param>
        /// <returns>ユーザー単位なら <c>true</c></returns>
        private static bool IsUserScoped(SettingsProperty property)
        {
            return property.Attributes[typeof(UserScopedSettingAttribute)] != null;
        }

        /// <summary>
        /// 設定ファイルから保存済みの値を読み込む
        /// </summary>
        /// <returns>設定名 → 直列化した値。ファイルが無ければ空</returns>
        /// <exception cref="ConfigurationErrorsException">ファイルの形式が不正、または読み込めない場合</exception>
        private Dictionary<string, string> ReadValues()
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!File.Exists(_filePath))
            {
                return values;
            }

            XDocument document;
            try
            {
                document = XDocument.Load(_filePath);
            }
            catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
            {
                throw new ConfigurationErrorsException("ユーザー設定ファイルを読み込めません。", ex, _filePath, 0);
            }

            if (document.Root?.Name.LocalName != RootElementName)
            {
                throw new ConfigurationErrorsException("ユーザー設定ファイルの形式が不正です。", _filePath, 0);
            }

            foreach (XElement element in document.Root.Elements(SettingElementName))
            {
                string? name = (string?)element.Attribute(NameAttributeName);
                if (!string.IsNullOrEmpty(name))
                {
                    values[name] = element.Value;
                }
            }

            return values;
        }

        /// <summary>
        /// 保存する値を一時ファイルへ書き出してから設定ファイルと置き換える
        /// </summary>
        /// <param name="values">設定名 → 直列化した値</param>
        private void WriteValues(Dictionary<string, string> values)
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var root = new XElement(RootElementName);
            var names = new List<string>(values.Keys);
            names.Sort(StringComparer.Ordinal);
            foreach (string name in names)
            {
                root.Add(new XElement(
                    SettingElementName,
                    new XAttribute(NameAttributeName, name),
                    values[name]));
            }

            // 書き込み中に終了しても設定ファイルが壊れないよう、書き終えてから置き換える
            string temporaryPath = _filePath + ".tmp";
            new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(temporaryPath);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
    }
}
