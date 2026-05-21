using System.ComponentModel;
using System.Windows;
using DataManager.Domain.Main;

namespace DataManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>Lua 面板扩展的额外宽度</summary>
        private const double LuaPanelWidth = 358; // 350 内容 + 4 splitter + 4 边距

        private MainViewModel? _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            var viewModel = new MainViewModel();
            _viewModel = viewModel;
            DataContext = viewModel;

            // 监听 Lua 面板可见性变化，动态调整窗口宽度
            viewModel.LuaViewerVM.PropertyChanged += OnLuaViewerPropertyChanged;

            // 通知 App 层初始化 CLI 服务
            if (Application.Current is App app)
            {
                app.InitializeCli(viewModel);
            }
        }

        /// <summary>
        /// Lua 面板显隐时调整窗口宽度，确保 JsonEditor 宽度不受影响。
        /// </summary>
        private void OnLuaViewerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Domain.LuaViewer.LuaViewerViewModel.IsVisible))
                return;

            if (_viewModel == null)
                return;

            if (_viewModel.LuaViewerVM.IsVisible)
            {
                // 面板显示 → 窗口变宽
                Width += LuaPanelWidth;
            }
            else
            {
                // 面板隐藏 → 窗口缩回
                Width -= LuaPanelWidth;
            }
        }
    }
}
