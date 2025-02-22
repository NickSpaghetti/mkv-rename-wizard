namespace MkvRenameWizard.Converters;

using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public class LongestStringWidthConverter : IValueConverter
{
    private readonly int _defaultStringWidth = 200;
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not IEnumerable<string> strings){
            return _defaultStringWidth;
        }
        var longestString = strings.OrderByDescending(s => s.Length).FirstOrDefault();
        if (longestString == null) {
            return _defaultStringWidth; // Default width
        }                                              
        var formattedText = new FormattedText(
            longestString,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal),
            14, // Adjust this value to match your font size
            Brushes.Black);

        return formattedText.Width + 20; // Add some padding
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return _defaultStringWidth;
    }
}
