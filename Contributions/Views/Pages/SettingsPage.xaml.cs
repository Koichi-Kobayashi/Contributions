using Contributions.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Contributions.Views.Pages
{
    /// <summary>
    /// 設定ページ。
    /// </summary>
    public partial class SettingsPage : INavigableView<SettingsViewModel>
    {
        public SettingsViewModel ViewModel { get; }

        /// <summary>
        /// SettingsPageを生成する。
        /// </summary>
        public SettingsPage(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }
    }
}
