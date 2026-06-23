using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfApp22
{
    public class BooleanToYesNoConverter : IValueConverter
    {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "Да" : "Нет";
            }
            return "Нет"; // значение по умолчанию для некорректных данных
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Конвертация в обратную сторону не требуется для отображения
            throw new NotImplementedException();
        }
    }
}
