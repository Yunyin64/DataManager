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

        /// <summary>当前文件的记录数</summary>
        public int RecordCount => Rows.Count;

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
            if (array.Count != Rows.Count)
            {
                LoadFile(file);
                return;
            }

            // 列和行数都一致 → 增量更新每行的值（按 RowIndex 匹配源数据行）
            var rowByIndex = Rows.ToDictionary(r => r.RowIndex);
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

            // 输出每行数据（Tab 分隔）
            foreach (var row in Rows)
            {
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

        // ── 单元格编辑 ──────────────────────────────────────

        /// <summary>
        /// 获取指定行/列对应的 JSON 节点类型。
        /// </summary>
        public JsonNodeType GetCellNodeType(int rowIndex, string columnName)
        {
            var row = Rows.FirstOrDefault(r => r.RowIndex == rowIndex);
            return row?.GetCellType(columnName) ?? JsonNodeType.Null;
        }

        /// <summary>
        /// 获取指定行/列的原始 JToken（用于复杂值编辑对话框）。
        /// </summary>
        public JToken? GetCellToken(int rowIndex, string columnName)
        {
            if (_currentFile?.RootToken is not JArray array)
                return null;

            if (rowIndex < 0 || rowIndex >= array.Count)
                return null;

            if (array[rowIndex] is JObject obj)
                return obj[columnName];

            return null;
        }

        /// <summary>
        /// 验证并提交简单值（String/Number/Boolean/Null）编辑。
        /// 返回 null 表示成功，返回错误消息字符串表示验证失败。
        /// </summary>
        public string? CommitCellEdit(int rowIndex, string columnName, string newText)
        {
            if (_currentFile?.RootToken is not JArray array)
                return "文件未加载";

            if (rowIndex < 0 || rowIndex >= array.Count)
                return "行索引无效";

            if (array[rowIndex] is not JObject obj)
                return "行数据异常";

            var currentToken = obj[columnName];
            var originalType = currentToken?.Type ?? JTokenType.Null;

            // 根据原始类型验证并构造新 JToken
            JToken newToken;
            switch (originalType)
            {
                case JTokenType.Integer:
                    if (long.TryParse(newText, out var longVal))
                        newToken = new JValue(longVal);
                    else if (double.TryParse(newText, out var dblVal))
                        newToken = new JValue(dblVal);
                    else
                        return "请输入数字";
                    break;

                case JTokenType.Float:
                    if (double.TryParse(newText, out var floatVal))
                        newToken = new JValue(floatVal);
                    else
                        return "请输入数字";
                    break;

                case JTokenType.Boolean:
                    if (bool.TryParse(newText, out var boolVal))
                        newToken = new JValue(boolVal);
                    else if (newText.Equals("1"))
                        newToken = new JValue(true);
                    else if (newText.Equals("0"))
                        newToken = new JValue(false);
                    else
                        return "请输入 True 或 False";
                    break;

                case JTokenType.Null:
                    // Null 列输入内容后变为 String
                    newToken = string.IsNullOrEmpty(newText)
                        ? JValue.CreateNull()
                        : new JValue(newText);
                    break;

                case JTokenType.String:
                default:
                    newToken = new JValue(newText);
                    break;
            }

            // 写回 JToken
            if (currentToken != null)
            {
                currentToken.Replace(newToken);
            }
            else
            {
                obj[columnName] = newToken;
            }

            // 标记文件为脏（通过反射设置，因为 IsDirty 没有公共 setter）
            MarkFileDirty();

            // 更新 ViewModel 行数据
            var row = Rows.FirstOrDefault(r => r.RowIndex == rowIndex);
            if (row != null)
            {
                var nodeType = ResolveNodeType(newToken);
                var displayText = FormatCellValue(newToken, nodeType);
                row.UpdateValue(columnName, displayText, nodeType);
            }

            return null; // 成功
        }

        /// <summary>
        /// 提交复杂值（Object/Array）编辑。直接传入解析后的 JToken。
        /// 返回 null 表示成功，返回错误消息字符串表示失败。
        /// </summary>
        public string? CommitComplexCellEdit(int rowIndex, string columnName, JToken newToken)
        {
            if (_currentFile?.RootToken is not JArray array)
                return "文件未加载";

            if (rowIndex < 0 || rowIndex >= array.Count)
                return "行索引无效";

            if (array[rowIndex] is not JObject obj)
                return "行数据异常";

            var currentToken = obj[columnName];
            if (currentToken != null)
            {
                currentToken.Replace(newToken);
            }
            else
            {
                obj[columnName] = newToken;
            }

            MarkFileDirty();

            // 更新 ViewModel 行数据
            var row = Rows.FirstOrDefault(r => r.RowIndex == rowIndex);
            if (row != null)
            {
                var nodeType = ResolveNodeType(newToken);
                var displayText = FormatCellValue(newToken, nodeType);
                row.UpdateValue(columnName, displayText, nodeType);
            }

            return null;
        }

        /// <summary>
        /// 标记当前文件为脏。
        /// </summary>
        private void MarkFileDirty()
        {
            if (_currentFile is Data.JsonDataFile concreteFile)
            {
                concreteFile.IsDirty = true;
            }
        }

        // ── 排序 ──────────────────────────────────────────

        /// <summary>
        /// 切换指定列的排序状态：无排序 → 升序 → 降序 → 无排序。
        /// </summary>
        public void ToggleSort(string columnName)
        {
            if (SortColumn == columnName)
            {
                // 同一列：切换方向
                SortDirection = SortDirection switch
                {
                    SortDirection.Ascending => SortDirection.Descending,
                    SortDirection.Descending => SortDirection.None,
                    _ => SortDirection.Ascending
                };
            }
            else
            {
                // 切换到新列：从升序开始
                SortColumn = columnName;
                SortDirection = SortDirection.Ascending;
            }

            ApplySort();
        }

        /// <summary>
        /// 应用当前排序状态到 Rows 集合。
        /// </summary>
        private void ApplySort()
        {
            if (Rows.Count == 0)
                return;

            if (SortDirection == SortDirection.None || SortColumn == null)
            {
                // 恢复原始顺序（按 RowIndex）
                var sorted = Rows.OrderBy(r => r.RowIndex).ToList();
                ReorderRows(sorted);
                return;
            }

            var colName = SortColumn;
            var ordered = SortDirection == SortDirection.Ascending
                ? Rows.OrderBy(r => GetSortKey(r, colName), SortKeyComparer.Instance).ToList()
                : Rows.OrderByDescending(r => GetSortKey(r, colName), SortKeyComparer.Instance).ToList();

            ReorderRows(ordered);
        }

        /// <summary>
        /// 获取行的排序键值。尝试按数值比较，否则按字符串。
        /// </summary>
        private static object? GetSortKey(JsonRowViewModel row, string columnName)
        {
            var value = row.GetValue(columnName);
            if (value is string s)
            {
                // 尝试解析为数值，支持数值列正确排序
                if (double.TryParse(s, out var num))
                    return num;
                return s;
            }
            return value;
        }

        /// <summary>
        /// 重新排列 Rows 集合（就地移动，不触发集合重建）。
        /// </summary>
        private void ReorderRows(List<JsonRowViewModel> sorted)
        {
            for (int i = 0; i < sorted.Count; i++)
            {
                var currentIndex = Rows.IndexOf(sorted[i]);
                if (currentIndex != i)
                {
                    Rows.Move(currentIndex, i);
                }
            }
        }

        // ── 值格式化 ──────────────────────────────────────

        /// <summary>
        /// 将 JToken 格式化为单元格显示文本。
        /// 简单值直接显示，复杂值浓缩为一行文本。
        /// </summary>
        private static string FormatCellValue(JToken token, JsonNodeType nodeType)
        {
            return nodeType switch
            {
                JsonNodeType.String  => token.Value<string>() ?? "",
                JsonNodeType.Number  => token.ToString(),
                JsonNodeType.Boolean => token.Value<bool>() ? "True" : "False",
                JsonNodeType.Null    => "null",
                JsonNodeType.Array   => FormatArraySummary((JArray)token),
                JsonNodeType.Object  => FormatObjectSummary((JObject)token),
                _ => token.ToString()
            };
        }

        /// <summary>
        /// 浓缩显示数组：[N] {第一个元素的值摘要}
        /// </summary>
        private static string FormatArraySummary(JArray array)
        {
            if (array.Count == 0)
                return "[]";

            var first = array[0];
            if (first is JObject obj)
            {
                // 显示第一个对象的所有值的摘要
                var values = obj.Properties()
                    .Select(p => FormatBriefValue(p.Value))
                    .ToList();
                var summary = string.Join(", ", values);
                return $"[{array.Count}] {{{summary}}}";
            }

            // 简单值数组
            var items = array.Take(3).Select(t => FormatBriefValue(t));
            var suffix = array.Count > 3 ? ", ..." : "";
            return $"[{array.Count}] {{{string.Join(", ", items)}{suffix}}}";
        }

        /// <summary>
        /// 浓缩显示对象：{N} {属性名列表}
        /// </summary>
        private static string FormatObjectSummary(JObject obj)
        {
            var keys = obj.Properties().Select(p => p.Name);
            return $"{{{obj.Count}}} {{{string.Join(", ", keys)}}}";
        }

        /// <summary>
        /// 简短值表示（用于嵌套值的摘要）。
        /// </summary>
        private static string FormatBriefValue(JToken token) => token.Type switch
        {
            JTokenType.String  => token.Value<string>() ?? "",
            JTokenType.Integer => token.ToString(),
            JTokenType.Float   => token.ToString(),
            JTokenType.Boolean => token.Value<bool>() ? "true" : "false",
            JTokenType.Null    => "null",
            JTokenType.Object  => $"{{{((JObject)token).Count}}}",
            JTokenType.Array   => $"[{((JArray)token).Count}]",
            _ => token.ToString()
        };

        /// <summary>
        /// 解析 JToken 类型到 JsonNodeType。
        /// </summary>
        private static JsonNodeType ResolveNodeType(JToken token) => token.Type switch
        {
            JTokenType.Object  => JsonNodeType.Object,
            JTokenType.Array   => JsonNodeType.Array,
            JTokenType.String  => JsonNodeType.String,
            JTokenType.Integer => JsonNodeType.Number,
            JTokenType.Float   => JsonNodeType.Number,
            JTokenType.Boolean => JsonNodeType.Boolean,
            JTokenType.Null    => JsonNodeType.Null,
            _ => JsonNodeType.String
        };
    }

    /// <summary>
    /// 排序键比较器。支持 double 和 string 混合比较。
    /// 数值优先于字符串，null 排在最后。
    /// </summary>
    internal class SortKeyComparer : IComparer<object?>
    {
        public static readonly SortKeyComparer Instance = new();

        public int Compare(object? x, object? y)
        {
            // null 排最后
            if (x == null && y == null) return 0;
            if (x == null) return 1;
            if (y == null) return -1;

            // 都是数值
            if (x is double dx && y is double dy)
                return dx.CompareTo(dy);

            // 都是字符串
            if (x is string sx && y is string sy)
                return string.Compare(sx, sy, StringComparison.OrdinalIgnoreCase);

            // 数值 vs 字符串：数值排前面
            if (x is double)
                return -1;
            if (y is double)
                return 1;

            return string.Compare(x.ToString(), y.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
