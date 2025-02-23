using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace MkvRenameWizard.Converters
{
    public class IntToBoolConverter : IValueConverter
    {
        public static readonly IntToBoolConverter Instance = new IntToBoolConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return index >= 0;
            }

            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}