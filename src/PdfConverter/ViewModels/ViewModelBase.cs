using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PdfConverter.ViewModels
{
    /// <summary>
    /// <see cref="INotifyPropertyChanged"/> の共通実装を提供する ViewModel 基底クラス
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /********************************************************************************/
        /*                               イベントハンドラ                               */
        /********************************************************************************/
        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged;


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 指定したプロパティの変更を通知する
        /// </summary>
        /// <param name="propertyName">変更されたプロパティの名前</param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// フィールドを更新し、値が変わった場合に変更通知を発行する
        /// </summary>
        /// <typeparam name="T">プロパティの型</typeparam>
        /// <param name="field">バッキングフィールド</param>
        /// <param name="value">新しい値</param>
        /// <param name="propertyName">プロパティ名</param>
        /// <returns>true: 値が更新された / false: 値が更新されなかった</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
