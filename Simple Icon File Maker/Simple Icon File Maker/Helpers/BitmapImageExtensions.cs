using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace Simple_Icon_File_Maker.Helpers;

public static class BitmapImageExtensions
{
    public static async Task CheckAndSetSourceAsync(this BitmapImage bitmapImage, IRandomAccessStream fileStream)
    {
    try
    {
        await bitmapImage.SetSourceAsync(fileStream);
    }
    catch (Exception ex)
    {
        ContentDialog dialog = new ContentDialog()
        {
            Title = "Error",
            Content = "An error occurred: " + ex.Message,
            PrimaryButtonText = "Ok"
        };
            dialog.XamlRoot = MainWindow.Current.Content.XamlRoot;
        await dialog.ShowAsync();
        return;
    }
    }
}
