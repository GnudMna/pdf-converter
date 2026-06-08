using System;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// 設定に応じて Microsoft Word または LibreOffice へ Word → PDF 変換を委譲するサービス
    /// </summary>
    public sealed class SelectableWordToPdfConversionService : IWordToPdfConversionService
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>Microsoft Word への変換サービス</summary>
        private readonly IWordToPdfConversionService _microsoftWordService;

        /// <summary>LibreOffice への変換サービス</summary>
        private readonly IWordToPdfConversionService _libreOfficeService;

        /// <summary>Word → PDF 変換設定</summary>
        private readonly IWordToPdfConversionSettings _settings;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定した変換サービスと設定を使用して委譲変換を行う
        /// </summary>
        /// <param name="microsoftWordService">Microsoft Word への変換サービス</param>
        /// <param name="libreOfficeService">LibreOffice への変換サービス</param>
        /// <param name="settings">Word → PDF 変換設定</param>
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
                    throw new InvalidOperationException($"未対応の Word → PDF 変換エンジンです: {_settings.Backend}");
            }
        }
    }
}
