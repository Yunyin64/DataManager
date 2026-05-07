using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using DataManager.Core.Utils.Converters;

namespace DataManager.Domain.JsonEditor
{
    /// <summary>
    /// JSON 编辑/显示面板。负责根据 ViewModel 的 Columns 动态生成 DataGrid 列。
    /// </summary>
    public partial class JsonEditorView : UserControl
    {
        private static readonly CellTypeToColorConverter ColorConverter = new();

        public JsonEditorView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is JsonEditorViewModel oldVM)
            {
                oldVM.Columns.CollectionChanged -= OnColumnsChanged;
            }

            if (e.NewValue is JsonEditorViewModel newVM)
            {
                newVM.Columns.CollectionChanged += OnColumnsChanged;
                RebuildColumns(newVM);
            }
        }

        private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (DataContext is JsonEditorViewModel vm)
            {
                RebuildColumns(vm);
            }
        }

        /// <summary>
        /// 根据 ViewModel 的 Columns 集合重新生成 DataGrid 列。
        /// </summary>
        private void RebuildColumns(JsonEditorViewModel vm)
        {
            DataGridView.Columns.Clear();

            // 行号列
            var rowIndexCol = new DataGridTextColumn
            {
                Header = "#",
                Binding = new Binding("RowIndex"),
                Width = new DataGridLength(45),
                IsReadOnly = true
            };
            DataGridView.Columns.Add(rowIndexCol);

            // 数据列 — 使用 DataGridTemplateColumn 支持颜色绑定
            foreach (var colName in vm.Columns)
            {
                var col = new DataGridTemplateColumn
                {
                    Header = colName,
                    Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                    IsReadOnly = true,
                    CellTemplate = CreateCellTemplate(colName)
                };

                DataGridView.Columns.Add(col);
            }
        }

        /// <summary>
        /// 为指定列名创建带颜色绑定的 DataTemplate。
        /// </summary>
        private static DataTemplate CreateCellTemplate(string columnName)
        {
            // 使用 XAML 字符串解析创建 DataTemplate，
            // 这样可以在 TextBlock 中同时绑定 Text 和 Foreground
            var xaml = $@"
                <DataTemplate
                    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                    xmlns:conv=""clr-namespace:DataManager.Core.Utils.Converters;assembly=DataManager"">
                    <DataTemplate.Resources>
                        <conv:CellTypeToColorConverter x:Key=""ColorConv""/>
                    </DataTemplate.Resources>
                    <TextBlock Text=""{{Binding [{columnName}]}}""
                               Foreground=""{{Binding Converter={{StaticResource ColorConv}}, ConverterParameter={columnName}}}""
                               Padding=""4,2""
                               VerticalAlignment=""Center""/>
                </DataTemplate>";

            return (DataTemplate)XamlReader.Parse(xaml);
        }
    }
}
