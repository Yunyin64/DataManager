using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using DataManager.Core.Utils.Converters;
using DataManager.Data.JsonNode;

namespace DataManager.Domain.JsonEditor
{
    /// <summary>
    /// JSON 编辑/显示面板。负责根据 ViewModel 的 Columns 动态生成 DataGrid 列。
    /// 支持简单值原地编辑和复杂值弹出对话框编辑。
    /// </summary>
    public partial class JsonEditorView : UserControl
    {
        private static readonly CellTypeToColorConverter ColorConverter = new();

        /// <summary>标记是否正在通过对话框编辑（避免重复触发）</summary>
        private bool _isDialogEditing;

        public JsonEditorView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;

            // 监听列头点击事件用于排序
            DataGridView.AddHandler(
                ButtonBase.ClickEvent,
                new RoutedEventHandler(OnColumnHeaderClick));

            // 监听行选中变化，更新 SelectedRowId
            DataGridView.CurrentCellChanged += OnCurrentCellChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is JsonEditorViewModel oldVM)
            {
                oldVM.Columns.CollectionChanged -= OnColumnsChanged;
                oldVM.PropertyChanged -= OnViewModelPropertyChanged;
            }

            if (e.NewValue is JsonEditorViewModel newVM)
            {
                newVM.Columns.CollectionChanged += OnColumnsChanged;
                newVM.PropertyChanged += OnViewModelPropertyChanged;
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
        /// 监听排序状态变化，更新列头的排序指示器。
        /// </summary>
        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(JsonEditorViewModel.SortColumn)
                or nameof(JsonEditorViewModel.SortDirection))
            {
                UpdateSortIndicators();
            }
        }

        /// <summary>
        /// 列头点击事件处理：触发排序切换。
        /// </summary>
        private void OnColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not DataGridColumnHeader header)
                return;

            // 忽略行号列的点击
            if (header.Column == null || header.Column.Header is not string headerText)
                return;

            // 去掉排序箭头还原真实列名
            var columnName = StripSortArrow(headerText);

            // 忽略 # 行号列
            if (columnName == "#")
                return;

            if (DataContext is JsonEditorViewModel vm)
            {
                vm.ToggleSort(columnName);
            }
        }

        /// <summary>
        /// 行选中变化时，从当前行取 "ID" 列值写入 ViewModel。
        /// 编辑中不更新，避免进入编辑模式时误触发面板隐藏。
        /// </summary>
        private void OnCurrentCellChanged(object? sender, EventArgs e)
        {
            if (DataContext is not JsonEditorViewModel vm)
                return;

            // 编辑中不更新选中行 ID
            if (vm.IsEditing)
            {
                System.Diagnostics.Debug.WriteLine($"[LuaPanel] OnCurrentCellChanged skipped: IsEditing=true");
                return;
            }

            var currentItem = DataGridView.CurrentItem;

            // CurrentItem 为 null 时忽略（DataGrid 内部状态切换，如进入编辑模式前的瞬态）
            if (currentItem == null)
            {
                System.Diagnostics.Debug.WriteLine($"[LuaPanel] OnCurrentCellChanged skipped: currentItem is null");
                return;
            }

            if (currentItem is JsonRowViewModel row)
            {
                var newId = row.GetValue("ID")?.ToString();
                System.Diagnostics.Debug.WriteLine($"[LuaPanel] OnCurrentCellChanged: newId={newId ?? "null"}, oldId={vm.SelectedRowId ?? "null"}");
                // 相同 ID 不重复触发
                if (newId != vm.SelectedRowId)
                    vm.SelectedRowId = newId;
            }
        }

        /// <summary>
        /// 更新列头显示的排序方向指示器（▲/▼）。
        /// </summary>
        private void UpdateSortIndicators()
        {
            if (DataContext is not JsonEditorViewModel vm)
                return;

            foreach (var col in DataGridView.Columns)
            {
                if (col.Header is not string headerText)
                    continue;

                var baseName = StripSortArrow(headerText);

                if (baseName == vm.SortColumn && vm.SortDirection != SortDirection.None)
                {
                    var arrow = vm.SortDirection == SortDirection.Ascending ? " ▲" : " ▼";
                    col.Header = baseName + arrow;
                }
                else
                {
                    col.Header = baseName;
                }
            }
        }

        /// <summary>
        /// 去掉列头末尾的排序箭头符号。
        /// </summary>
        private static string StripSortArrow(string header)
        {
            if (header.EndsWith(" ▲") || header.EndsWith(" ▼"))
                return header[..^2];
            return header;
        }

        /// <summary>
        /// 行加载时在行头显示连续序号（1, 2, 3...），方便查看总行数。
        /// </summary>
        private void DataGridView_LoadingRow(object? sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        // ── 编辑事件处理 ──────────────────────────────────────

        /// <summary>
        /// 开始编辑时判断：复杂值（Object/Array）取消内联编辑，弹出对话框。
        /// </summary>
        private void DataGridView_BeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
        {
            if (_isDialogEditing)
                return;

            if (DataContext is not JsonEditorViewModel vm)
                return;

            if (e.Row.Item is not JsonRowViewModel row)
                return;

            // 获取列名
            var columnName = GetColumnName(e.Column);
            if (columnName == null || columnName == "#")
            {
                e.Cancel = true;
                return;
            }

            // 标记正在编辑（暂停自动重载）
            vm.IsEditing = true;

            // 判断节点类型
            var nodeType = vm.GetCellNodeType(row.RowIndex, columnName);
            if (nodeType == JsonNodeType.Object || nodeType == JsonNodeType.Array)
            {
                // 取消内联编辑
                e.Cancel = true;

                // 弹出对话框编辑
                Dispatcher.BeginInvoke(new Action(() => OpenComplexEditDialog(vm, row.RowIndex, columnName)));
            }
        }

        /// <summary>
        /// 单元格编辑结束时验证并提交。
        /// </summary>
        private void DataGridView_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            if (DataContext is not JsonEditorViewModel vm)
                return;

            // 编辑结束，恢复自动重载
            vm.IsEditing = false;

            if (e.EditAction == DataGridEditAction.Cancel)
                return;

            if (e.Row.Item is not JsonRowViewModel row)
                return;

            var columnName = GetColumnName(e.Column);
            if (columnName == null)
                return;

            // 从 EditingElement 中查找 TextBox（TemplateColumn 的 EditingElement 是 ContentPresenter）
            var textBox = FindVisualChild<TextBox>(e.EditingElement);
            if (textBox != null)
            {
                var newText = textBox.Text;
                var error = vm.CommitCellEdit(row.RowIndex, columnName, newText);

                if (error != null)
                {
                    // 验证失败：标红边框，取消提交
                    textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD3, 0x2F, 0x2F));
                    textBox.BorderThickness = new Thickness(2);
                    textBox.ToolTip = error;
                    e.Cancel = true;
                    vm.IsEditing = true; // 仍然在编辑状态
                }
            }
        }

        /// <summary>
        /// 打开复杂值编辑对话框。
        /// </summary>
        private void OpenComplexEditDialog(JsonEditorViewModel vm, int rowIndex, string columnName)
        {
            var token = vm.GetCellToken(rowIndex, columnName);
            if (token == null)
                return;

            _isDialogEditing = true;
            try
            {
                var dialog = new JsonEditDialog
                {
                    Owner = Window.GetWindow(this)
                };
                dialog.SetValue(columnName, token);

                if (dialog.ShowDialog() == true && dialog.ResultToken != null)
                {
                    vm.CommitComplexCellEdit(rowIndex, columnName, dialog.ResultToken);
                }
            }
            finally
            {
                _isDialogEditing = false;
            }
        }

        /// <summary>
        /// 从 DataGridColumn 获取真实列名（去掉排序箭头）。
        /// </summary>
        private static string? GetColumnName(DataGridColumn? column)
        {
            if (column?.Header is not string headerText)
                return null;
            return StripSortArrow(headerText);
        }

        // ── 列构建 ──────────────────────────────────────────

        /// <summary>
        /// 根据 ViewModel 的 Columns 集合重新生成 DataGrid 列。
        /// </summary>
        private void RebuildColumns(JsonEditorViewModel vm)
        {
            DataGridView.Columns.Clear();

            // 行号列（只读）
            var rowIndexCol = new DataGridTextColumn
            {
                Header = "#",
                Binding = new Binding("RowIndex"),
                Width = new DataGridLength(45),
                IsReadOnly = true
            };
            DataGridView.Columns.Add(rowIndexCol);

            // 数据列 — 使用 DataGridTemplateColumn 支持颜色绑定和编辑
            foreach (var colName in vm.Columns)
            {
                var col = new DataGridTemplateColumn
                {
                    Header = colName,
                    Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                    IsReadOnly = false,
                    CellTemplate = CreateCellTemplate(colName),
                    CellEditingTemplate = CreateCellEditingTemplate(colName),
                    CanUserSort = false
                };

                DataGridView.Columns.Add(col);
            }

            // 列重建后恢复排序指示器
            UpdateSortIndicators();
        }

        /// <summary>
        /// 为指定列名创建显示模板（带颜色绑定的 TextBlock）。
        /// </summary>
        private static DataTemplate CreateCellTemplate(string columnName)
        {
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

        /// <summary>
        /// 为指定列名创建编辑模板（TextBox）。
        /// </summary>
        private static DataTemplate CreateCellEditingTemplate(string columnName)
        {
            var xaml = $@"
                <DataTemplate
                    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                    <TextBox Text=""{{Binding [{columnName}], UpdateSourceTrigger=PropertyChanged}}""
                             Padding=""3,1""
                             VerticalAlignment=""Center""
                             BorderThickness=""1""
                             BorderBrush=""#0078D4""/>
                </DataTemplate>";

            return (DataTemplate)XamlReader.Parse(xaml);
        }

        // ── 辅助方法 ──────────────────────────────────────────

        /// <summary>
        /// 在 Visual Tree 中递归查找指定类型的子元素。
        /// </summary>
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent is T target)
                return target;

            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
