using Contributions.ViewModels.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Contributions.Views.Windows
{
    /// <summary>
    /// メインウィンドウ。
    /// </summary>
    public partial class MainWindow : INavigationWindow
    {
        public MainWindowViewModel ViewModel { get; }

        /// <summary>
        /// MainWindowを生成する。
        /// </summary>
        public MainWindow(
            MainWindowViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService,
            ISnackbarService snackbarService
        )
        {
            ViewModel = viewModel;
            DataContext = this;

            SystemThemeWatcher.Watch(this);

            InitializeComponent();
            SetPageService(navigationViewPageProvider);

            navigationService.SetNavigationControl(RootNavigation);
            snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        }

        #region INavigationWindow methods

        /// <summary>
        /// ナビゲーションビューを取得する。
        /// </summary>
        public INavigationView GetNavigation() => RootNavigation;

        /// <summary>
        /// 指定ページへ遷移する。
        /// </summary>
        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        /// <summary>
        /// ページプロバイダーを設定する。
        /// </summary>
        public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) => RootNavigation.SetPageProviderService(navigationViewPageProvider);

        /// <summary>
        /// ウィンドウを表示する。
        /// </summary>
        public void ShowWindow() => Show();

        /// <summary>
        /// ウィンドウを閉じる。
        /// </summary>
        public void CloseWindow() => Close();

        #endregion INavigationWindow methods

        /// <summary>
        /// Raises the closed event.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Make sure that closing this window will begin the process of closing the application.
            Application.Current.Shutdown();
        }

        /// <summary>
        /// INavigationWindow向けのナビゲーション取得。
        /// </summary>
        INavigationView INavigationWindow.GetNavigation()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// DIコンテナのサービスプロバイダーを設定する。
        /// </summary>
        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }
    }
}
