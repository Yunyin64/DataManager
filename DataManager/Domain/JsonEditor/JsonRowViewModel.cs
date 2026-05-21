using System.ComponentModel;
using System.Dynamic;
using DataManager.Data.JsonNode;

namespace DataManager.Domain.JsonEditor
{
    /// <summary>
    /// DataGrid 行的动态 ViewModel。
    /// 继承 DynamicObject，使 DataGrid 可通过动态属性名绑定到任意列。
    /// </summary>
    public class JsonRowViewModel : DynamicObject, INotifyPropertyChanged
    {
        private readonly Dictionary<string, object?> _values = new();
        private readonly Dictionary<string, JsonNodeType> _cellTypes = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>行索引（从 0 开始）</summary>
        public int RowIndex { get; set; }

        /// <summary>每个单元格的值类型，用于颜色区分</summary>
        public IReadOnlyDictionary<string, JsonNodeType> CellTypes => _cellTypes;

        /// <summary>
        /// 设置指定列的值和类型。
        /// </summary>
        public void SetValue(string columnName, object? value, JsonNodeType nodeType)
        {
            _values[columnName] = value;
            _cellTypes[columnName] = nodeType;
        }

        /// <summary>
        /// 更新指定列的值和类型，并触发属性变更通知。
        /// 用于增量刷新场景（不重建行对象，仅更新数据）。
        /// </summary>
        public void UpdateValue(string columnName, object? value, JsonNodeType nodeType)
        {
            _values[columnName] = value;
            _cellTypes[columnName] = nodeType;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(columnName));
        }

        /// <summary>
        /// 获取指定列的值。
        /// </summary>
        public object? GetValue(string columnName)
        {
            return _values.TryGetValue(columnName, out var val) ? val : null;
        }

        /// <summary>
        /// 获取指定列的节点类型。
        /// </summary>
        public JsonNodeType GetCellType(string columnName)
        {
            return _cellTypes.TryGetValue(columnName, out var t) ? t : JsonNodeType.Null;
        }

        // ── DynamicObject 重写 ──────────────────────────────

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            result = _values.TryGetValue(binder.Name, out var val) ? val : null;
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object? value)
        {
            _values[binder.Name] = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(binder.Name));
            return true;
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return _values.Keys;
        }

        // ── Indexer（备用绑定路径）──────────────────────────

        /// <summary>
        /// 索引器访问，支持 Binding Path=[ColumnName] 语法。
        /// </summary>
        public object? this[string columnName]
        {
            get => _values.TryGetValue(columnName, out var val) ? val : null;
            set
            {
                _values[columnName] = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"Item[{columnName}]"));
            }
        }
    }
}
