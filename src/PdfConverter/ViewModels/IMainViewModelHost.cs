using PdfConverter.ViewModels.Coordinators;

namespace PdfConverter.ViewModels
{
    /// <summary>
    /// Coordinator がメイン画面の状態を読み書きするための複合インターフェース。<br/>
    /// 実装は <see cref="MainViewModelCoordinatorHost"/> が担当する。
    /// </summary>
    public interface IMainViewModelHost : IPreviewCoordinatorHost, ISaveCoordinatorHost
    {
    }
}
