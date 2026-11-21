using Moq;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using PdfConverter.ViewModels;
using Xunit;

namespace PdfConverter.Tests.ViewModels
{
    /// <summary>
    /// <see cref="WordConversionSettingsViewModel"/> の Word 変換設定ロジックを検証する
    /// </summary>
    public class WordConversionSettingsViewModelTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 保存済み設定から初期値が読み込まれることを検証する
        /// </summary>
        [Fact]
        public void Constructor_LoadsPersistedSettings()
        {
            var settings = new TrackingWordToPdfConversionSettings
            {
                Backend = WordToPdfBackend.LibreOffice,
                LibreOfficePath = @"C:\LibreOffice\program\soffice.exe",
                PdfFormat = WordToPdfPdfFormat.PdfA,
                OptimizeFor = WordToPdfOptimizeFor.Online,
                ExportBookmarks = false,
                ExportComments = true,
            };

            var viewModel = new WordConversionSettingsViewModel(settings, null);

            Assert.Equal(WordToPdfBackend.LibreOffice, viewModel.Backend);
            Assert.Equal(@"C:\LibreOffice\program\soffice.exe", viewModel.LibreOfficePath);
            Assert.Equal(WordToPdfPdfFormat.PdfA, viewModel.PdfFormat);
            Assert.Equal(WordToPdfOptimizeFor.Online, viewModel.OptimizeFor);
            Assert.False(viewModel.ExportBookmarks);
            Assert.True(viewModel.ExportComments);
            Assert.True(viewModel.IsLibreOfficePathVisible);
        }

        /// <summary>
        /// 変換エンジンの選択肢が 2 種類公開されることを検証する
        /// </summary>
        [Fact]
        public void BackendOptions_ContainsTwoBackends()
        {
            var viewModel = CreateViewModel();

            Assert.Equal(2, viewModel.BackendOptions.Count);
        }

        /// <summary>
        /// PDF 形式の選択肢が 2 種類公開されることを検証する
        /// </summary>
        [Fact]
        public void PdfFormatOptions_ContainsTwoFormats()
        {
            var viewModel = CreateViewModel();

            Assert.Equal(2, viewModel.PdfFormatOptions.Count);
        }

        /// <summary>
        /// 出力最適化モードの選択肢が 2 種類公開されることを検証する
        /// </summary>
        [Fact]
        public void OptimizeForOptions_ContainsTwoModes()
        {
            var viewModel = CreateViewModel();

            Assert.Equal(2, viewModel.OptimizeForOptions.Count);
        }

        /// <summary>
        /// 変換エンジンを LibreOffice に変更したときにパス入力欄が表示されることを検証する
        /// </summary>
        [Fact]
        public void Backend_LibreOffice_ShowsLibreOfficePath()
        {
            var settings = new TrackingWordToPdfConversionSettings();
            var viewModel = new WordConversionSettingsViewModel(settings, null);

            viewModel.Backend = WordToPdfBackend.LibreOffice;

            Assert.True(viewModel.IsLibreOfficePathVisible);
        }

        /// <summary>
        /// 変換エンジンを Microsoft Word に変更したときにパス入力欄が非表示になることを検証する
        /// </summary>
        [Fact]
        public void Backend_MicrosoftWord_HidesLibreOfficePath()
        {
            var settings = new TrackingWordToPdfConversionSettings
            {
                Backend = WordToPdfBackend.LibreOffice,
            };
            var viewModel = new WordConversionSettingsViewModel(settings, null);

            viewModel.Backend = WordToPdfBackend.MicrosoftWord;

            Assert.False(viewModel.IsLibreOfficePathVisible);
        }

        /// <summary>
        /// 変換エンジン変更時に設定が保存され、読み込み済み文書が再読み込みされることを検証する
        /// </summary>
        [Fact]
        public void Backend_Change_PersistsAndReloadsDocument()
        {
            var settings = new TrackingWordToPdfConversionSettings();
            int reloadCount = 0;
            var viewModel = new WordConversionSettingsViewModel(settings, () => reloadCount++);

            viewModel.Backend = WordToPdfBackend.LibreOffice;

            Assert.Equal(WordToPdfBackend.LibreOffice, settings.Backend);
            Assert.Equal(1, settings.SaveCount);
            Assert.Equal(1, reloadCount);
        }

        /// <summary>
        /// Microsoft Word 利用時の LibreOffice パス変更では保存のみ行い、再読み込みしないことを検証する
        /// </summary>
        [Fact]
        public void LibreOfficePath_Change_WhenMicrosoftWordBackend_SavesWithoutReload()
        {
            var settings = new TrackingWordToPdfConversionSettings();
            int reloadCount = 0;
            var viewModel = new WordConversionSettingsViewModel(settings, () => reloadCount++);

            viewModel.LibreOfficePath = @"C:\LibreOffice\program\soffice.exe";

            Assert.Equal(@"C:\LibreOffice\program\soffice.exe", settings.LibreOfficePath);
            Assert.Equal(1, settings.SaveCount);
            Assert.Equal(0, reloadCount);
        }

        /// <summary>
        /// LibreOffice 利用時のパス変更では設定保存後に文書を再読み込みすることを検証する
        /// </summary>
        [Fact]
        public void LibreOfficePath_Change_WhenLibreOfficeBackend_ReloadsDocument()
        {
            var settings = new TrackingWordToPdfConversionSettings
            {
                Backend = WordToPdfBackend.LibreOffice,
            };
            int reloadCount = 0;
            var viewModel = new WordConversionSettingsViewModel(settings, () => reloadCount++);

            viewModel.LibreOfficePath = @"C:\LibreOffice\program\soffice.exe";

            Assert.Equal(1, settings.SaveCount);
            Assert.Equal(1, reloadCount);
        }

        /// <summary>
        /// LibreOffice パスに null を設定したときに空文字へ正規化されることを検証する
        /// </summary>
        [Fact]
        public void LibreOfficePath_Null_NormalizesToEmpty()
        {
            var settings = new TrackingWordToPdfConversionSettings
            {
                LibreOfficePath = @"C:\existing\soffice.exe",
            };
            var viewModel = new WordConversionSettingsViewModel(settings, null);

            viewModel.LibreOfficePath = null;

            Assert.Equal(string.Empty, viewModel.LibreOfficePath);
            Assert.Equal(string.Empty, settings.LibreOfficePath);
        }

        /// <summary>
        /// PDF 形式変更時に設定が保存され、読み込み済み文書が再読み込みされることを検証する
        /// </summary>
        [Fact]
        public void PdfFormat_Change_PersistsAndReloadsDocument()
        {
            var settings = new TrackingWordToPdfConversionSettings();
            int reloadCount = 0;
            var viewModel = new WordConversionSettingsViewModel(settings, () => reloadCount++);

            viewModel.PdfFormat = WordToPdfPdfFormat.PdfA;

            Assert.Equal(WordToPdfPdfFormat.PdfA, settings.PdfFormat);
            Assert.Equal(1, settings.SaveCount);
            Assert.Equal(1, reloadCount);
        }

        /// <summary>
        /// 出力最適化モード変更時に設定が保存され、読み込み済み文書が再読み込みされることを検証する
        /// </summary>
        [Fact]
        public void OptimizeFor_Change_PersistsAndReloadsDocument()
        {
            var settings = new TrackingWordToPdfConversionSettings();
            int reloadCount = 0;
            var viewModel = new WordConversionSettingsViewModel(settings, () => reloadCount++);

            viewModel.OptimizeFor = WordToPdfOptimizeFor.Online;

            Assert.Equal(WordToPdfOptimizeFor.Online, settings.OptimizeFor);
            Assert.Equal(1, settings.SaveCount);
            Assert.Equal(1, reloadCount);
        }

        /// <summary>
        /// しおり出力設定変更時に設定が保存され、読み込み済み文書が再読み込みされることを検証する
        /// </summary>
        [Fact]
        public void ExportBookmarks_Change_PersistsAndReloadsDocument()
        {
            var settings = new TrackingWordToPdfConversionSettings();
            int reloadCount = 0;
            var viewModel = new WordConversionSettingsViewModel(settings, () => reloadCount++);

            viewModel.ExportBookmarks = false;

            Assert.False(settings.ExportBookmarks);
            Assert.Equal(1, settings.SaveCount);
            Assert.Equal(1, reloadCount);
        }

        /// <summary>
        /// コメント出力設定変更時に設定が保存され、読み込み済み文書が再読み込みされることを検証する
        /// </summary>
        [Fact]
        public void ExportComments_Change_PersistsAndReloadsDocument()
        {
            var settings = new TrackingWordToPdfConversionSettings();
            int reloadCount = 0;
            var viewModel = new WordConversionSettingsViewModel(settings, () => reloadCount++);

            viewModel.ExportComments = true;

            Assert.True(settings.ExportComments);
            Assert.Equal(1, settings.SaveCount);
            Assert.Equal(1, reloadCount);
        }

        /// <summary>
        /// 同一値への再設定では保存も再読み込みも行われないことを検証する
        /// </summary>
        [Fact]
        public void Backend_SameValue_DoesNotPersistOrReload()
        {
            var settings = new TrackingWordToPdfConversionSettings();
            int reloadCount = 0;
            var viewModel = new WordConversionSettingsViewModel(settings, () => reloadCount++);

            viewModel.Backend = WordToPdfBackend.MicrosoftWord;

            Assert.Equal(0, settings.SaveCount);
            Assert.Equal(0, reloadCount);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        private static WordConversionSettingsViewModel CreateViewModel()
        {
            return new WordConversionSettingsViewModel(WordToPdfConversionSettingsTestHelper.Create().Object, null);
        }
    }
}
