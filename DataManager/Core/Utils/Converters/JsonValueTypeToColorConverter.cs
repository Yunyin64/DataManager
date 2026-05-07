using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DataManager.Data.JsonNode;

namespace DataManager.Core.Utils.Converters
{
    /// <summary>
    /// 将 JsonNodeType 转换为对应的前景色画刷。
    /// ConverterParameter 传入列名，Value 传入 JsonRowViewModel。
    /// </summary>
    public class JsonValueTypeToColorConverter : IValueConverter
    {
        // 缓存画刷实例
        private static readonly SolidColorBrush StringBrush = new(Color.FromRgb(0xD1, 0x9A, 0x66));
        private static readonly SolidColorBrush NumberBrush = new(Color.FromRgb(0xC6, 0x78, 0xDD));
        private static readonly SolidColorBrush BoolTrueBrush = new(Color.FromRgb(0x98, 0xC3, 0x79));
        private static readonly SolidColorBrush BoolFalseBrush = new(Color.FromRgb(0xE0, 0x6C, 0x75));
        private static readonly SolidColorBrush NullBrush = new(Color.FromRgb(0x5C, 0x63, 0x70));
        private static readonly SolidColorBrush ComplexBrush = new(Color.FromRgb(0x61, 0xAF, 0xEF));
        private static readonly SolidColorBrush DefaultBrush = new(Colors.Black);

        static JsonValueTypeToColorConverter()
        {
            StringBrush.Freeze();
            NumberBrush.Freeze();
            BoolTrueBrush.Freeze();
            BoolFalseBrush.Freeze();
            NullBrush.Freeze();
            ComplexBrush.Freeze();
            DefaultBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not JsonNodeType nodeType)
                return DefaultBrush;

            return nodeType switch
            {
                JsonNodeType.String  => StringBrush,
                JsonNodeType.Number  => NumberBrush,
                JsonNodeType.Boolean => BoolTrueBrush, // 简化处理，不区分 true/false
                JsonNodeType.Null    => NullBrush,
                JsonNodeType.Object  => ComplexBrush,
                JsonNodeType.Array   => ComplexBrush,
                _ => DefaultBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
