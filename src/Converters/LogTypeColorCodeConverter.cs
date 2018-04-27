using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace LagoVista.GitHelper.Converters
{
    public class LogTypeColorCodeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogType logType)
            {
                switch(logType)
                {
                    case LogType.Error: return Brushes.Red;
                    case LogType.Warning: return Brushes.Yellow;
                    case LogType.Success: return Brushes.Lime;
                }
            }

            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
