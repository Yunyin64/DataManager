using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DataManager.Core.Base.Interface;
using DataManager.Data.JsonNode;
using Newtonsoft.Json.Linq;

namespace DataManager.Domain.JsonEditor
{
    /// <summary>
    /// JSON 编辑器 ViewModel。将 JSON 数据解析为 DataGrid 的列和行。
    /// </summary>
    public partial class JsonEditorViewModel : Core.Base.ViewModelBase
    {
        /// <summary>列名集合（按 JSON Object 属性顺序）</summary>
        public ObservableCollection<string> Columns { get; } = new();

        /// <summary>行数据集合</summary>
        public ObservableCollection<JsonRowViewModel> Rows { get; } = new();

        /// <summary>不支持的 JSON 结构时显示的提示消息</summary>
        [ObservableProperty]
        private string? _unsupportedMessage;

        /// <summary>是否有数据可显示（用于控制 DataGrid 和提示文本的可见性）</summary>
        [ObservableProperty]
        private bool _hasData;

        /// <summary>当前文件的记录数</summary>
        public int RecordCount => Rows.Count;

        /// <summary>
        /// 加载指定 JSON 文件到 DataGrid。
        /// </summary>
        public void LoadFile(IJsonDataFile file)
        {
            Clear();

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
        }

        /// <summary>
        /// 清空当前数据。
        /// </summary>
        public void Clear()
        {
            Columns.Clear();
            Rows.Clear();
            UnsupportedMessage = null;
            HasData = false;
            OnPropertyChanged(nameof(RecordCount));
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
}
