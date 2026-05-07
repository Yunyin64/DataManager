using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;

namespace DataManager.Data.JsonNode
{
    /// <summary>
    /// JSON 节点类型枚举。
    /// </summary>
    public enum JsonNodeType
    {
        Object,
        Array,
        String,
        Number,
        Boolean,
        Null
    }

    /// <summary>
    /// JSON 节点的 ViewModel 包装。把 JToken 树递归包装为可绑定的 ViewModel 树。
    /// </summary>
    public partial class JsonNodeVM : Core.Base.ViewModelBase
    {
        [ObservableProperty]
        private string _key = string.Empty;

        [ObservableProperty]
        private string _displayValue = string.Empty;

        [ObservableProperty]
        private JsonNodeType _nodeType;

        [ObservableProperty]
        private bool _isExpanded;

        /// <summary>底层 JToken 引用</summary>
        public JToken Token { get; }

        /// <summary>父节点</summary>
        public JsonNodeVM? Parent { get; }

        /// <summary>子节点集合</summary>
        public ObservableCollection<JsonNodeVM> Children { get; } = new();

        /// <summary>
        /// 从 JToken 创建节点 VM。
        /// </summary>
        public JsonNodeVM(JToken token, string key, JsonNodeVM? parent = null)
        {
            Token = token;
            Key = key;
            Parent = parent;
            NodeType = ResolveNodeType(token);
            DisplayValue = ResolveDisplayValue(token);

            BuildChildren();
        }

        /// <summary>
        /// 从 JToken 根节点创建整棵 VM 树。
        /// </summary>
        public static JsonNodeVM FromToken(JToken root, string rootKey = "(root)")
        {
            return new JsonNodeVM(root, rootKey);
        }

        /// <summary>
        /// 获取或设置当前节点的原始值（仅对值类型节点有效）。
        /// </summary>
        public object? Value
        {
            get => (Token as JValue)?.Value;
            set
            {
                if (Token is JValue jVal)
                {
                    jVal.Value = value;
                    DisplayValue = ResolveDisplayValue(Token);
                    NodeType = ResolveNodeType(Token);
                    OnPropertyChanged(nameof(Value));
                }
            }
        }

        /// <summary>
        /// 重建子节点（当底层 Token 变化时调用）。
        /// </summary>
        public void Rebuild()
        {
            DisplayValue = ResolveDisplayValue(Token);
            NodeType = ResolveNodeType(Token);
            Children.Clear();
            BuildChildren();
        }

        // ── 私有方法 ──────────────────────────────────────

        private void BuildChildren()
        {
            switch (Token)
            {
                case JObject obj:
                    foreach (var prop in obj.Properties())
                        Children.Add(new JsonNodeVM(prop.Value, prop.Name, this));
                    break;

                case JArray arr:
                    for (int i = 0; i < arr.Count; i++)
                        Children.Add(new JsonNodeVM(arr[i], $"[{i}]", this));
                    break;
            }
        }

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

        private static string ResolveDisplayValue(JToken token) => token.Type switch
        {
            JTokenType.Object  => $"{{ {((JObject)token).Count} items }}",
            JTokenType.Array   => $"[ {((JArray)token).Count} items ]",
            JTokenType.Null    => "null",
            _ => token.ToString()
        };
    }
}
