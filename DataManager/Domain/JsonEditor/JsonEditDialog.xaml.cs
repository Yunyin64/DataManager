using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DataManager.Domain.JsonEditor
{
    /// <summary>
    /// 复杂值（Object/Array）编辑对话框。
    /// 显示格式化 JSON 文本，允许用户编辑后验证并返回。
    /// </summary>
    public partial class JsonEditDialog : Window
    {
        /// <summary>编辑后的结果 JToken（确认后有值）</summary>
        public JToken? ResultToken { get; private set; }

        public JsonEditDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 设置要编辑的字段名和当前值。
        /// </summary>
        public void SetValue(string fieldName, JToken currentValue)
        {
            FieldNameRun.Text = fieldName;
            JsonTextBox.Text = currentValue.ToString(Formatting.Indented);
        }

        private void OnFormat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var token = JToken.Parse(JsonTextBox.Text);
                JsonTextBox.Text = token.ToString(Formatting.Indented);
                HideError();
            }
            catch (JsonReaderException ex)
            {
                ShowError($"JSON 语法错误：{ex.Message}");
            }
        }

        private void OnConfirm_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResultToken = JToken.Parse(JsonTextBox.Text);
                DialogResult = true;
                Close();
            }
            catch (JsonReaderException ex)
            {
                ShowError($"JSON 语法错误：{ex.Message}");
            }
        }

        private void OnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorText.Text = "";
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }
}
