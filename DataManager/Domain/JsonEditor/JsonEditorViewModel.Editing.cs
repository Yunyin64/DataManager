using CommunityToolkit.Mvvm.Input;
using DataManager.Data.JsonNode;
using Newtonsoft.Json.Linq;

namespace DataManager.Domain.JsonEditor
{
    /// <summary>
    /// JsonEditorViewModel — 单元格编辑、占位行、新增行。
    /// </summary>
    public partial class JsonEditorViewModel
    {
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

            // 标记文件为脏
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

        // ── 占位行 ──────────────────────────────────────────

        /// <summary>占位空行数量</summary>
        private const int PlaceholderRowCount = 8;

        /// <summary>
        /// 在 Rows 末尾追加空白占位行，用于视觉填充。
        /// </summary>
        private void AppendPlaceholderRows()
        {
            for (int i = 0; i < PlaceholderRowCount; i++)
            {
                var placeholder = new JsonRowViewModel
                {
                    RowIndex = -1,
                    IsPlaceholder = true
                };
                Rows.Add(placeholder);
            }
        }

        /// <summary>
        /// 移除所有占位行。
        /// </summary>
        private void RemovePlaceholderRows()
        {
            for (int i = Rows.Count - 1; i >= 0; i--)
            {
                if (Rows[i].IsPlaceholder)
                    Rows.RemoveAt(i);
            }
        }

        // ── 新增行 ──────────────────────────────────────────

        /// <summary>
        /// 新增一行默认数据。根据现有数据的第一行分析每列类型，生成对应默认值。
        /// string → 字段名，number → 0，boolean → false，array → []，object → {}，null → null。
        /// </summary>
        [RelayCommand]
        private void AddNewRow()
        {
            if (_currentFile?.RootToken is not JArray array)
                return;

            // 分析第一行的类型
            var template = array.Count > 0 ? array[0] as JObject : null;

            var newObj = new JObject();
            foreach (var colName in Columns)
            {
                var sampleToken = template?[colName];
                var defaultToken = CreateDefaultToken(colName, sampleToken);
                newObj[colName] = defaultToken;
            }

            // 追加到 JSON 数组
            var newIndex = array.Count;
            array.Add(newObj);

            // 标记脏
            MarkFileDirty();

            // 移除占位行 → 追加新数据行 → 重新追加占位行
            RemovePlaceholderRows();

            var row = new JsonRowViewModel { RowIndex = newIndex };
            foreach (var colName in Columns)
            {
                var token = newObj[colName]!;
                var nodeType = ResolveNodeType(token);
                var displayText = FormatCellValue(token, nodeType);
                row.SetValue(colName, displayText, nodeType);
            }
            Rows.Add(row);

            AppendPlaceholderRows();

            OnPropertyChanged(nameof(RecordCount));
            ApplySort();
        }

        /// <summary>
        /// 根据样本 Token 创建默认值。
        /// </summary>
        private static JToken CreateDefaultToken(string columnName, JToken? sample)
        {
            if (sample == null)
                return JValue.CreateNull();

            return sample.Type switch
            {
                JTokenType.String => new JValue(columnName),
                JTokenType.Integer => new JValue(0),
                JTokenType.Float => new JValue(0.0),
                JTokenType.Boolean => new JValue(false),
                JTokenType.Array => new JArray(),
                JTokenType.Object => new JObject(),
                JTokenType.Null => JValue.CreateNull(),
                _ => new JValue(columnName)
            };
        }
    }
}
