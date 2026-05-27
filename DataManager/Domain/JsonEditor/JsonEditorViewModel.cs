using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataManager.Core.Base.Interface;
using DataManager.Data.JsonNode;
using Newtonsoft.Json.Linq;

namespace DataManager.Domain.JsonEditor
{
    /// <summary>
    /// 排序方向枚举。
    /// </summary>
    public enum SortDirection
    {
        None,
        Ascending,
        Descending
    }

    /// <summary>
    /// JSON 编辑器 ViewModel。将 JSON 数据解析为 DataGrid 的列和行。
    /// </summary>
    public partial class JsonEditorViewModel : Core.Base.ViewModelBase
    {
        /// <summary>列名集合（按 JSON Object 属性顺序）</summary>
        public ObservableCollection<string> Columns { get; } = new();

        /// <summary>行数据集合</summary>
        public ObservableCollection<JsonRowViewModel> Rows { get; } = new();

        /// <summary>当前加载的文件引用（用于编辑写回）</summary>
        private IJsonDataFile? _currentFile;

        /// <summary>是否正在编辑中（用于暂停自动重载）</summary>
        [ObservableProperty]
        private bool _isEditing;

        /// <summary>不支持的 JSON 结构时显示的提示消息</summary>
        [ObservableProperty]
        private string? _unsupportedMessage;

        /// <summary>是否有数据可显示（用于控制 DataGrid 和提示文本的可见性）</summary>
        [ObservableProperty]
        private bool _hasData;

        /// <summary>当前排序列名（null 表示无排序）</summary>
        [ObservableProperty]
        private string? _sortColumn;

        /// <summary>当前排序方向</summary>
        [ObservableProperty]
        private SortDirection _sortDirection = SortDirection.None;

        /// <summary>当前选中行的 ID 字段值（用于驱动 Lua 面板联动）</summary>
        [ObservableProperty]
        private string? _selectedRowId;

        /// <summary>当前文件的记录数（不含占位行）</summary>
        public int RecordCount => Rows.Count(r => !r.IsPlaceholder);

        /// <summary>
        /// 加载指定 JSON 文件到 DataGrid。
        /// </summary>
        public void LoadFile(IJsonDataFile file)
        {
            Clear();
            _currentFile = file;

            if (file.RootToken == null)
            {
                UnsupportedMessage = "文件未加载";
                HasData = false;
                return;
            }

            // 仅支持 Array of Objects
            if (file.RootToken is not JArray array || array.Count == 0)
            {
                if (file.RootToken is JArray { Count: 0 })
                {
                    UnsupportedMessage = "空数组，无数据可显示";
                }
                else
                {
                    UnsupportedMessage = "暂不支持该 JSON 结构（仅支持 Array of Objects）";
                }
                HasData = false;
                return;
            }

            // 检查第一个元素是否为 Object
            if (array[0] is not JObject firstObj)
            {
                UnsupportedMessage = "暂不支持该 JSON 结构（数组元素不是 Object）";
                HasData = false;
                return;
            }

            // 从第一个 Object 提取列名
            foreach (var prop in firstObj.Properties())
            {
                Columns.Add(prop.Name);
            }

            // 生成行数据
            for (int i = 0; i < array.Count; i++)
            {
                if (array[i] is JObject obj)
                {
                    var row = new JsonRowViewModel { RowIndex = i };

                    foreach (var colName in Columns)
                    {
                        var token = obj[colName];
                        if (token == null)
                        {
                            row.SetValue(colName, "null", JsonNodeType.Null);
                        }
                        else
                        {
                            var nodeType = ResolveNodeType(token);
                            var displayText = FormatCellValue(token, nodeType);
                            row.SetValue(colName, displayText, nodeType);
                        }
                    }

                    Rows.Add(row);
                }
            }

            UnsupportedMessage = null;
            HasData = true;
            OnPropertyChanged(nameof(RecordCount));

            // 追加空白占位行（视觉填充）
            AppendPlaceholderRows();

            // 如果有排序状态，应用排序
            ApplySort();
        }

        /// <summary>
        /// 增量刷新数据。仅更新现有行的单元格值，不重建列和行集合。
        /// 当列结构或行数变化时自动回退到完整 LoadFile。
        /// 这样可以保持 DataGrid 的列宽、滚动位置等 UI 状态不变。
        /// </summary>
        public void RefreshData(IJsonDataFile file)
        {
            // 当前无数据 → 走完整加载
            if (!HasData)
            {
                LoadFile(file);
                return;
            }

            if (file.RootToken == null || file.RootToken is not JArray array || array.Count == 0)
            {
                LoadFile(file);
                return;
            }

            if (array[0] is not JObject firstObj)
            {
                LoadFile(file);
                return;
            }

            // 检查列结构是否变化
            var newColumns = firstObj.Properties().Select(p => p.Name).ToList();
            if (!ColumnsMatch(newColumns))
            {
                LoadFile(file);
                return;
            }

            // 行数变化 → 走完整加载
            var dataRowCount = Rows.Count(r => !r.IsPlaceholder);
            if (array.Count != dataRowCount)
            {
                LoadFile(file);
                return;
            }

            // 列和行数都一致 → 增量更新每行的值（按 RowIndex 匹配源数据行）
            var rowByIndex = Rows.Where(r => !r.IsPlaceholder).ToDictionary(r => r.RowIndex);
            for (int i = 0; i < array.Count; i++)
            {
                if (array[i] is JObject obj && rowByIndex.TryGetValue(i, out var row))
                {
                    foreach (var colName in Columns)
                    {
                        var token = obj[colName];
                        if (token == null)
                        {
                            row.UpdateValue(colName, "null", JsonNodeType.Null);
                        }
                        else
                        {
                            var nodeType = ResolveNodeType(token);
                            var displayText = FormatCellValue(token, nodeType);
                            row.UpdateValue(colName, displayText, nodeType);
                        }
                    }
                }
            }

            // 数据更新后重新应用排序
            ApplySort();
        }

        /// <summary>
        /// 判断新列名列表是否与当前 Columns 一致。
        /// </summary>
        private bool ColumnsMatch(List<string> newColumns)
        {
            if (newColumns.Count != Columns.Count)
                return false;

            for (int i = 0; i < newColumns.Count; i++)
            {
                if (newColumns[i] != Columns[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 清空当前数据。
        /// </summary>
        public void Clear()
        {
            Columns.Clear();
            Rows.Clear();
            _currentFile = null;
            SortColumn = null;
            SortDirection = SortDirection.None;
            UnsupportedMessage = null;
            HasData = false;
            OnPropertyChanged(nameof(RecordCount));
        }

        // ── 复制简洁信息 ──────────────────────────────────────

        /// <summary>
        /// 将当前加载的数据以 Tab 分隔的简洁表格格式复制到剪贴板。
        /// 包含列头和所有行数据。
        /// </summary>
        [RelayCommand]
        private void CopySimpleSummary()
        {
            if (!HasData || Columns.Count == 0 || Rows.Count == 0)
                return;

            var sb = new StringBuilder();

            // 输出列头（Tab 分隔）
            sb.AppendLine(string.Join("\t", Columns));

            // 输出每行数据（Tab 分隔，跳过占位行）
            foreach (var row in Rows)
            {
                if (row.IsPlaceholder)
                    continue;

                var cells = new List<string>();
                foreach (var col in Columns)
                {
                    var value = row.GetValue(col);
                    cells.Add(value?.ToString() ?? "");
                }
                sb.AppendLine(string.Join("\t", cells));
            }

            Clipboard.SetText(sb.ToString());
        }
    }
}
