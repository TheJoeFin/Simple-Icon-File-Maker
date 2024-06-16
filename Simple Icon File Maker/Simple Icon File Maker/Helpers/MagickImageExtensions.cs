using ImageMagick;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Simple_Icon_File_Maker;

public static class MagickImageExtensions
{
    public static ImageSource ToImageSource(this MagickImage image)
    {
        using MemoryStream memoryStream = new();
        image.Write(memoryStream);
        memoryStream.Position = 0;

        BitmapImage bitmapImage = new();
        bitmapImage.SetSource(memoryStream.AsRandomAccessStream());

        return bitmapImage;
    }
}
