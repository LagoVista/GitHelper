using GitHelper.Build;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Linq;

namespace LagoVista.GitHelper.Converters
{
    public class ColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CurrentStatus fileStatus)
            {
                switch (fileStatus)
                {
                    case CurrentStatus.Conflicts:
                        return Brushes.Red;
                    case CurrentStatus.Dirty:
                        return Brushes.Green;
                    case CurrentStatus.Untouched:
                        return Brushes.Gray;
                }
            }

            if(value is Models.Package package)
            {
                if (parameter != null && parameter.ToString() == "foreground")
                {
                    if (package.VersionCount > 1)
                        return Brushes.White;

                    if (package.CanUpgarde)
                        return Brushes.Black;

                    return Brushes.White;
                }
                else
                {
                    if (package.VersionCount > 1)
                        return Brushes.Crimson;

                    if (package.CanUpgarde)
                        return Brushes.Gold;

                    return Brushes.Green;
                }
            }

            if (value is BuildStatus buildStatus)
            {
                if (parameter != null && parameter.ToString() == "foreground")
                {
                    switch (buildStatus)
                    {
                        case BuildStatus.Built:
                            return Brushes.White;
                        case BuildStatus.Error:
                            return Brushes.White;
                    }

                    return Brushes.Black;
                }
                else
                {
                    switch (buildStatus)
                    {
                        case BuildStatus.Built:
                            return Brushes.Green;
                        case BuildStatus.Error:
                            return Brushes.Crimson;
                    }

                    return Brushes.Gold;
                }
            }

            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
