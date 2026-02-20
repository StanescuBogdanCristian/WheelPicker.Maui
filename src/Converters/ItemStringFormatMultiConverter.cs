using SBC.WheelPicker.Helpers;
using System.Globalization;

namespace SBC.WheelPicker.Converters;

internal class ItemStringFormatMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // values[0] = item, values[1] = ItemStringFormat (may be null)
        object? item = values.Length > 0 ? values[0] : null;
        string? format = values.Length > 1 ? values[1] as string : null;

        if (item is null)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(format))
            return item.ToString() ?? string.Empty;

        format = StringHelper.NormalizeStringFormat(format);
        if (string.IsNullOrWhiteSpace(format))
            return item.ToString() ?? string.Empty;

        try
        {
            return string.Format(culture, format, item);
        }
        catch
        {
            // Never crash the UI for a malformed format.
            return item.ToString() ?? string.Empty;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
