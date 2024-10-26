using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Simple_Icon_File_Maker.Converters;

internal partial class BoolToPrimaryButtonStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // take a bool if true return the primary button style else return the secondary button style

        if (value is true)
            return (Style)Application.Current.Resources["AccentButtonStyle"];

        return (Style)Application.Current.Resources["DefaultButtonStyle"];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
