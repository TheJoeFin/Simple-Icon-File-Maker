using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace Simple_Icon_File_Maker.Helpers;

public static class BitmapImageExtensions
{
    public static async Task CheckAndSetSourceAsync(this BitmapImage bitmapImage, MainWindow mainWindow, IRandomAccessStream fileStream)
    {
        try
        {
            await bitmapImage.SetSourceAsync(fileStream);
        }
        catch (Exception ex)
        {
            mainWindow.ShowErrorInfoBar(ex.Message);
        }
    }
}
