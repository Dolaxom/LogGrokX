using System;
using System.Globalization;
using System.Windows.Data;

namespace LogGrokX.Controls
{
    public class GroupContinuationToOpacityConverter : IValueConverter
    {
        public double ContinuationOpacity { get; set; } = 0.18;

        public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is true ? ContinuationOpacity : 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
