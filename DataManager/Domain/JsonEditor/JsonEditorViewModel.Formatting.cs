using DataManager.Data.JsonNode;
using Newtonsoft.Json.Linq;

namespace DataManager.Domain.JsonEditor
{
    /// <summary>
    /// JsonEditorViewModel — 排序、值格式化。
    /// </summary>
    public partial class JsonEditorViewModel
    {
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
        /// 应用当前排序状态到 Rows 集合（占位行始终在末尾）。
        /// </summary>
        private void ApplySort()
        {
            if (Rows.Count == 0)
                return;

            // 只排序非占位行
            var dataRows = Rows.Where(r => !r.IsPlaceholder).ToList();
            var placeholders = Rows.Where(r => r.IsPlaceholder).ToList();

            if (dataRows.Count == 0)
                return;

            List<JsonRowViewModel> sorted;
            if (SortDirection == SortDirection.None || SortColumn == null)
            {
                // 恢复原始顺序（按 RowIndex）
                sorted = dataRows.OrderBy(r => r.RowIndex).ToList();
            }
            else
            {
                var colName = SortColumn;
                sorted = SortDirection == SortDirection.Ascending
                    ? dataRows.OrderBy(r => GetSortKey(r, colName), SortKeyComparer.Instance).ToList()
                    : dataRows.OrderByDescending(r => GetSortKey(r, colName), SortKeyComparer.Instance).ToList();
            }

            // 合并：数据行 + 占位行
            sorted.AddRange(placeholders);
            ReorderRows(sorted);
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
