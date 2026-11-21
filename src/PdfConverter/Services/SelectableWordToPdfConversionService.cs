using System;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// 設定に応じてMicrosoft WordまたはLibreOfficeへWord → PDF変換を委譲するサービス
    /// </summary>
    public sealed class SelectableWordToPdfConversionService : IWordToPdfConversionService
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>Microsoft Wordへの変換サービス</summary>
        private readonly IWordToPdfConversionService _microsoftWordService;

        /// <summary>LibreOfficeへの変換サービス</summary>
        private readonly IWordToPdfConversionService _libreOfficeService;

        /// <summary>Word → PDF変換設定</summary>
        private readonly IWordToPdfConversionSettings _settings;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定した変換サービスと設定を使用して委譲変換を行う
        /// </summary>
        /// <param name="microsoftWordService">Microsoft Wordへの変換サービス</param>
        /// <param name="libreOfficeService">LibreOfficeへの変換サービス</param>
        /// <param name="settings">Word → PDF変換設定</param>
        public SelectableWordToPdfConversionService(
            IWordToPdfConversionService microsoftWordService,
            IWordToPdfConversionService libreOfficeService,
            IWordToPdfConversionSettings settings)
        {
            _microsoftWordService = microsoftWordService;
            _libreOfficeService = libreOfficeService;
            _settings = settings;
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public Task<string> ConvertToPdfAsync(string wordFilePath, CancellationToken cancellationToken = default)
        {
            switch (_settings.Backend)
            {
                case WordToPdfBackend.MicrosoftWord:
                    return _microsoftWordService.ConvertToPdfAsync(wordFilePath, cancellationToken);
                case WordToPdfBackend.LibreOffice:
                    return _libreOfficeService.ConvertToPdfAsync(wordFilePath, cancellationToken);
                default:
                    throw new InvalidOperationException($"未対応の Word → PDF変換エンジンです: {_settings.Backend}");
            }
        }
    }
}
