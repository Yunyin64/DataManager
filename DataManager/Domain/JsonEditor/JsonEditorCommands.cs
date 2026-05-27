using System.Windows.Input;

namespace DataManager.Domain.JsonEditor
{
    /// <summary>
    /// JSON 编辑器的静态路由命令定义。
    /// </summary>
    public static class JsonEditorCommands
    {
        /// <summary>复制选中单元格内容（Ctrl+C）</summary>
        public static readonly RoutedUICommand CopyCommand =
            new("复制", "Copy", typeof(JsonEditorCommands),
                new InputGestureCollection { new KeyGesture(Key.C, ModifierKeys.Control) });

        /// <summary>粘贴到选中单元格（Ctrl+V）</summary>
        public static readonly RoutedUICommand PasteCommand =
            new("粘贴", "Paste", typeof(JsonEditorCommands),
                new InputGestureCollection { new KeyGesture(Key.V, ModifierKeys.Control) });
    }
}
