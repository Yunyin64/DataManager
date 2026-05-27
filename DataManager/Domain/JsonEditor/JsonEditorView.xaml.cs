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

            // 注册复制/粘贴命令绑定
            CommandBindings.Add(new CommandBinding(
                JsonEditorCommands.CopyCommand, OnCopyExecuted, OnCopyCanExecute));
            CommandBindings.Add(new CommandBinding(
                JsonEditorCommands.PasteCommand, OnPasteExecuted, OnPasteCanExecute));
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
                // 占位行不更新选中 ID
                if (row.IsPlaceholder)
                    return;

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
        /// 行加载时在行头显示连续序号（1, 2, 3...），占位行不显示。
        /// </summary>
        private void DataGridView_LoadingRow(object? sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is JsonRowViewModel row && row.IsPlaceholder)
            {
                e.Row.Header = "";
            }
            else
            {
                e.Row.Header = (e.Row.GetIndex() + 1).ToString();
            }
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

            // 占位行不可编辑
            if (row.IsPlaceholder)
            {
                e.Cancel = true;
                return;
            }

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

            // 行号列（只读）— 使用 TemplateColumn 支持占位行显示 "+" 按钮
            var rowIndexCol = new DataGridTemplateColumn
            {
                Header = "#",
                Width = new DataGridLength(45),
                IsReadOnly = true,
                CellTemplate = CreateRowIndexCellTemplate()
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

        /// <summary>
        /// 创建行号列的 CellTemplate：数据行显示 RowIndex，占位行显示 "+" 按钮。
        /// </summary>
        private DataTemplate CreateRowIndexCellTemplate()
        {
            var xaml = @"
                <DataTemplate
                    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                    <Grid>
                        <!-- 数据行：显示行号 -->
                        <TextBlock Text=""{Binding RowIndex}""
                                   Padding=""4,2""
                                   VerticalAlignment=""Center""
                                   HorizontalAlignment=""Center"">
                            <TextBlock.Style>
                                <Style TargetType=""TextBlock"">
                                    <Setter Property=""Visibility"" Value=""Visible""/>
                                    <Style.Triggers>
                                        <DataTrigger Binding=""{Binding IsPlaceholder}"" Value=""True"">
                                            <Setter Property=""Visibility"" Value=""Collapsed""/>
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </TextBlock.Style>
                        </TextBlock>
                        <!-- 占位行：显示 + 按钮 -->
                        <Button Content=""+""
                                FontSize=""14""
                                FontWeight=""Bold""
                                Padding=""0""
                                Width=""22"" Height=""22""
                                HorizontalAlignment=""Center""
                                VerticalAlignment=""Center""
                                Cursor=""Hand""
                                ToolTip=""新增一行数据""
                                Command=""{Binding DataContext.AddNewRowCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"">
                            <Button.Style>
                                <Style TargetType=""Button"">
                                    <Setter Property=""Visibility"" Value=""Collapsed""/>
                                    <Setter Property=""Background"" Value=""#E8F5E9""/>
                                    <Setter Property=""Foreground"" Value=""#388E3C""/>
                                    <Setter Property=""BorderBrush"" Value=""#A5D6A7""/>
                                    <Setter Property=""BorderThickness"" Value=""1""/>
                                    <Style.Triggers>
                                        <DataTrigger Binding=""{Binding IsPlaceholder}"" Value=""True"">
                                            <Setter Property=""Visibility"" Value=""Visible""/>
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </Button.Style>
                        </Button>
                    </Grid>
                </DataTemplate>";

            return (DataTemplate)XamlReader.Parse(xaml);
        }

        // ── 复制/粘贴快捷键 ──────────────────────────────────────

        private void OnCopyCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = DataGridView.CurrentCell.IsValid && DataContext is JsonEditorViewModel { HasData: true };
        }

        /// <summary>
        /// Ctrl+C：复制当前选中单元格的文本值到剪贴板。
        /// 多个选中单元格时用 Tab 分隔，多行用换行分隔。
        /// </summary>
        private void OnCopyExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataContext is not JsonEditorViewModel vm)
                return;

            var selectedCells = DataGridView.SelectedCells;
            if (selectedCells == null || selectedCells.Count == 0)
            {
                // 没有选中区域，尝试复制当前单元格
                var current = DataGridView.CurrentCell;
                if (!current.IsValid)
                    return;

                var text = GetCellText(current);
                if (text != null)
                    Clipboard.SetText(text);
                return;
            }

            // 按行分组选中单元格（跳过占位行）
            var rowGroups = new SortedDictionary<int, SortedDictionary<int, string>>();
            foreach (var cell in selectedCells)
            {
                if (cell.Item is not JsonRowViewModel row || row.IsPlaceholder)
                    continue;

                var colIndex = DataGridView.Columns.IndexOf(cell.Column);
                if (!rowGroups.ContainsKey(row.RowIndex))
                    rowGroups[row.RowIndex] = new SortedDictionary<int, string>();

                var cellText = GetCellTextFromRowAndColumn(row, cell.Column);
                rowGroups[row.RowIndex][colIndex] = cellText ?? "";
            }

            // 构建输出文本
            var sb = new System.Text.StringBuilder();
            foreach (var rowPair in rowGroups)
            {
                sb.AppendLine(string.Join("\t", rowPair.Value.Values));
            }

            var result = sb.ToString().TrimEnd('\r', '\n');
            if (!string.IsNullOrEmpty(result))
                Clipboard.SetText(result);
        }

        private void OnPasteCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = DataGridView.CurrentCell.IsValid
                           && DataContext is JsonEditorViewModel { HasData: true }
                           && Clipboard.ContainsText();
        }

        /// <summary>
        /// Ctrl+V：将剪贴板文本粘贴到当前选中的单元格。
        /// 支持多行多列粘贴（Tab 分隔列，换行分隔行）。
        /// </summary>
        private void OnPasteExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataContext is not JsonEditorViewModel vm)
                return;

            if (!Clipboard.ContainsText())
                return;

            var current = DataGridView.CurrentCell;
            if (!current.IsValid || current.Item is not JsonRowViewModel startRow)
                return;

            var columnName = GetColumnName(current.Column);
            if (columnName == null || columnName == "#")
                return;

            var clipText = Clipboard.GetText();
            var lines = clipText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // 去掉末尾空行
            if (lines.Length > 1 && string.IsNullOrEmpty(lines[^1]))
                lines = lines[..^1];

            // 找到起始列索引
            var startColIdx = vm.Columns.IndexOf(columnName);
            if (startColIdx < 0)
                return;

            // 找到起始行在 Rows 中的位置
            var startRowIdx = vm.Rows.IndexOf(startRow);
            if (startRowIdx < 0)
                return;

            // 逐行逐列粘贴
            for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
            {
                var targetRowIdx = startRowIdx + lineIdx;
                if (targetRowIdx >= vm.Rows.Count)
                    break;

                var targetRow = vm.Rows[targetRowIdx];
                var cells = lines[lineIdx].Split('\t');

                for (int cellIdx = 0; cellIdx < cells.Length; cellIdx++)
                {
                    var targetColIdx = startColIdx + cellIdx;
                    if (targetColIdx >= vm.Columns.Count)
                        break;

                    var targetColName = vm.Columns[targetColIdx];
                    var newText = cells[cellIdx];

                    // 调用 ViewModel 的编辑提交方法
                    vm.CommitCellEdit(targetRow.RowIndex, targetColName, newText);
                }
            }
        }

        /// <summary>
        /// 从 DataGridCellInfo 获取单元格的文本值。
        /// </summary>
        private string? GetCellText(DataGridCellInfo cellInfo)
        {
            if (cellInfo.Item is not JsonRowViewModel row)
                return null;
            return GetCellTextFromRowAndColumn(row, cellInfo.Column);
        }

        /// <summary>
        /// 从行 ViewModel 和 DataGridColumn 获取文本值。
        /// </summary>
        private string? GetCellTextFromRowAndColumn(JsonRowViewModel row, DataGridColumn? column)
        {
            var colName = GetColumnName(column);
            if (colName == null)
                return null;
            if (colName == "#")
                return row.RowIndex.ToString();
            return row.GetValue(colName)?.ToString() ?? "";
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
