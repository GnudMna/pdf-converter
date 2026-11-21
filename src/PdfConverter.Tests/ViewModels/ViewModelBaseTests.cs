using System.ComponentModel;
using PdfConverter.ViewModels;
using Xunit;

namespace PdfConverter.Tests.ViewModels
{
    /// <summary>
    /// <see cref="ViewModelBase"/> の SetProperty と PropertyChanged 通知を検証する
    /// </summary>
    public class ViewModelBaseTests
    {
        private sealed class TestViewModel : ViewModelBase
        {
            private string _name;

            public string Name
            {
                get => _name;
                set => SetProperty(ref _name, value);
            }

            public void Notify(string propertyName) => OnPropertyChanged(propertyName);
        }

        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// プロパティ値が変更されたときに PropertyChanged イベントが発火することを検証する
        /// </summary>
        [Fact]
        public void SetProperty_WhenValueChanges_RaisesPropertyChanged()
        {
            var viewModel = new TestViewModel();
            string changedProperty = null;
            viewModel.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

            viewModel.Name = "test";

            Assert.Equal(nameof(TestViewModel.Name), changedProperty);
            Assert.Equal("test", viewModel.Name);
        }

        /// <summary>
        /// プロパティ値が変わらない場合に PropertyChanged イベントが発火しないことを検証する
        /// </summary>
        [Fact]
        public void SetProperty_WhenValueUnchanged_DoesNotRaisePropertyChanged()
        {
            var viewModel = new TestViewModel { Name = "same" };
            var eventCount = 0;
            viewModel.PropertyChanged += (_, __) => eventCount++;

            viewModel.Name = "same";

            Assert.Equal(0, eventCount);
        }

        /// <summary>
        /// OnPropertyChanged に明示したプロパティ名でイベントが発火することを検証する
        /// </summary>
        [Fact]
        public void OnPropertyChanged_RaisesEventWithExplicitName()
        {
            var viewModel = new TestViewModel();
            string changedProperty = null;
            viewModel.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

            viewModel.Notify("Custom");

            Assert.Equal("Custom", changedProperty);
        }
    }
}
