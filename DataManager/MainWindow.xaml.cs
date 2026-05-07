using System.Windows;
using DataManager.Domain.Main;

namespace DataManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var viewModel = new MainViewModel();
            DataContext = viewModel;

            // 通知 App 层初始化 CLI 服务
            if (Application.Current is App app)
            {
                app.InitializeCli(viewModel);
            }
        }
    }
}
