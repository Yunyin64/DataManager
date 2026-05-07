using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DataManager.Data.JsonNode;
using DataManager.Domain.JsonEditor;

namespace DataManager.Core.Utils.Converters
{
    /// <summary>
    /// 多值转换器：根据行 ViewModel 和列名，返回该单元格值对应的颜色画刷。
    /// Values[0] = JsonRowViewModel (DataContext of the row)
    /// ConverterParameter = 列名 (string)
    /// </summary>
    public class CellTypeToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush StringBrush = Frozen(new(Color.FromRgb(0xD1, 0x9A, 0x66)));
        private static readonly SolidColorBrush NumberBrush = Frozen(new(Color.FromRgb(0xC6, 0x78, 0xDD)));
        private static readonly SolidColorBrush BoolBrush = Frozen(new(Color.FromRgb(0x98, 0xC3, 0x79)));
        private static readonly SolidColorBrush NullBrush = Frozen(new(Color.FromRgb(0x5C, 0x63, 0x70)));
        private static readonly SolidColorBrush ComplexBrush = Frozen(new(Color.FromRgb(0x61, 0xAF, 0xEF)));
        private static readonly SolidColorBrush DefaultBrush = Frozen(new(Colors.Black));

        private static SolidColorBrush Frozen(SolidColorBrush b) { b.Freeze(); return b; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not JsonRowViewModel row || parameter is not string colName)
                return DefaultBrush;

            var cellType = row.GetCellType(colName);
            return cellType switch
            {
                JsonNodeType.String  => StringBrush,
                JsonNodeType.Number  => NumberBrush,
                JsonNodeType.Boolean => BoolBrush,
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
