using ImageMagick;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Simple_Icon_File_Maker;

public static class MagickImageExtensions
{
    public static async Task<ImageSource> ToImageSource(this MagickImage image)
    {
        using MemoryStream memoryStream = new();
        await image.WriteAsync(memoryStream, MagickFormat.Png);
        memoryStream.Position = 0;

        BitmapImage bitmapImage = new();
        await bitmapImage.SetSourceAsync(memoryStream.AsRandomAccessStream());

        return bitmapImage;
    }
}
