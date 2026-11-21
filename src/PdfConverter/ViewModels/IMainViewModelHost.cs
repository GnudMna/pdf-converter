using PdfConverter.ViewModels.Coordinators;

namespace PdfConverter.ViewModels
{
    /// <summary>
    /// Coordinator が MainViewModel の状態を読み書きするための複合インターフェース
    /// </summary>
    public interface IMainViewModelHost : IPreviewCoordinatorHost, ISaveCoordinatorHost
    {
    }
}
