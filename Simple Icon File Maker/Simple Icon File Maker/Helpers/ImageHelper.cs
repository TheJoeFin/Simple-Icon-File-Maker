using ImageMagick;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;

namespace Simple_Icon_File_Maker.Helpers;

public static class ImageHelper
{
    public static async Task<MagickImage?> LoadImageAsync(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return null;

        try
        {
            MagickImage image;
            // For .ico files, load the largest frame instead of the first one
            if (Path.GetExtension(imagePath).Equals(".ico", StringComparison.InvariantCultureIgnoreCase))
            {
                MagickImageCollection collection = new(imagePath);
                // Find the largest frame by area (width * height)
                MagickImage? largestFrame = collection.Cast<MagickImage>()
          .OrderByDescending(img => (int)img.Width * (int)img.Height)
                    .FirstOrDefault();

                if (largestFrame != null)
                {
                    // Create a new image from the largest frame to avoid disposal issues
                    image = (MagickImage)largestFrame.Clone();
                }
                else
                {
                    // Fallback to the first frame if something goes wrong
                    image = new(imagePath);
                }
            }
            else
            {
                image = new(imagePath);
            }

            // If the image is smaller than 512px, scale it up using NearestNeighbor
            // to maintain sharp pixels when displayed
            int smallerDimension = (int)Math.Min(image.Width, image.Height);
            if (smallerDimension is < 512 and > 0)
            {
                // Scale up to 512px using NearestNeighbor (point sampling) to keep pixels sharp
                int targetSize = 512;
                image.FilterType = FilterType.Point; // Point filter = NearestNeighbor
                image.Resize((uint)targetSize, (uint)targetSize);
            }

            return image;
        }
        catch
        {
            return null;
        }
    }

    public static int GetSmallerImageSide(string imagePath)
    {
        try
        {
            MagickImage image = new(imagePath);
            return (int)Math.Min(image.Width, image.Height);
        }
        catch
        {
            return 0;
        }
    }

    public static async Task<string> ApplyGrayscaleAsync(string imagePath, Image? displayImage = null)
    {
        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        string fileName = Path.GetFileNameWithoutExtension(imagePath);
        string extension = Path.GetExtension(imagePath);
        string grayFilePath = Path.Combine(sf.Path, $"{fileName}_gray{extension}");
        MagickImage image = new(imagePath);

        image.Grayscale();
        await image.WriteAsync(grayFilePath);

        if (displayImage != null)
            displayImage.Source = image.ToImageSource();

        return grayFilePath;
    }

    public static async Task<string> ApplyBlackWhiteOtsuAsync(string imagePath, Image? displayImage = null)
    {
        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        string fileName = Path.GetFileNameWithoutExtension(imagePath);
        string extension = Path.GetExtension(imagePath);
        string bwFilePath = Path.Combine(sf.Path, $"{fileName}_bw{extension}");
        MagickImage image = new(imagePath);

        image.Grayscale();
        image.AutoThreshold(AutoThresholdMethod.OTSU);
        await image.WriteAsync(bwFilePath);

        if (displayImage != null)
            displayImage.Source = image.ToImageSource();

        return bwFilePath;
    }

    public static async Task<string> ApplyBlackWhiteKapurAsync(string imagePath, Image? displayImage = null)
    {
        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        string fileName = Path.GetFileNameWithoutExtension(imagePath);
        string extension = Path.GetExtension(imagePath);
        string bwkFilePath = Path.Combine(sf.Path, $"{fileName}_bwk{extension}");
        MagickImage image = new(imagePath);

        image.Grayscale();
        image.AutoThreshold(AutoThresholdMethod.Kapur);
        await image.WriteAsync(bwkFilePath);

        if (displayImage != null)
            displayImage.Source = image.ToImageSource();

        return bwkFilePath;
    }

    public static async Task<string> ApplyInvertAsync(string imagePath, Image? displayImage = null)
    {
        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        string fileName = Path.GetFileNameWithoutExtension(imagePath);
        string extension = Path.GetExtension(imagePath);
        string invFilePath = Path.Combine(sf.Path, $"{fileName}_inv{extension}");
        MagickImage image = new(imagePath);

        image.Negate(Channels.RGB);
        await image.WriteAsync(invFilePath);

        if (displayImage != null)
            displayImage.Source = image.ToImageSource();

        return invFilePath;
    }
}
