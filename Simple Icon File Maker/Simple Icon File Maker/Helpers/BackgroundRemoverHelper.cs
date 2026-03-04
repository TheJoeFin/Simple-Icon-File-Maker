using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Simple_Icon_File_Maker.Helpers;

public static class BackgroundRemoverHelper
{
    public static async Task<bool> IsAvailableAsync()
    {
        try
        {
            AIFeatureReadyState readyState = ImageObjectExtractor.GetReadyState();

            if (readyState == AIFeatureReadyState.Ready)
                return true;

            if (readyState == AIFeatureReadyState.NotReady)
            {
                var result = await ImageObjectExtractor.EnsureReadyAsync();
                return result.Status == AIFeatureReadyResultState.Success;
            }

            // NotSupportedOnCurrentSystem or DisabledByUser
            return false;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<string> RemoveBackgroundAsync(string imagePath)
    {
        StorageFile inputFile = await StorageFile.GetFileFromPathAsync(imagePath);
        SoftwareBitmap sourceBitmap;

        using (IRandomAccessStream stream = await inputFile.OpenAsync(FileAccessMode.Read))
        {
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            sourceBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }

        using ImageObjectExtractor extractor = await ImageObjectExtractor.CreateWithSoftwareBitmapAsync(sourceBitmap);

        // Hint with the entire image rect as the region of interest
        ImageObjectExtractorHint hint = new(
            includeRects: [new RectInt32(0, 0, sourceBitmap.PixelWidth, sourceBitmap.PixelHeight)],
            includePoints: [],
            excludePoints: []);

        SoftwareBitmap mask = extractor.GetSoftwareBitmapObjectMask(hint);

        SoftwareBitmap resultBitmap = ApplyMaskAsAlpha(sourceBitmap, mask);

        StorageFolder cacheFolder = ApplicationData.Current.LocalCacheFolder;
        string fileName = Path.GetFileNameWithoutExtension(imagePath);
        string outputFileName = $"{fileName}_nobg.png";
        StorageFile outputFile = await cacheFolder.CreateFileAsync(outputFileName, CreationCollisionOption.ReplaceExisting);

        using (IRandomAccessStream outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite))
        {
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream);
            encoder.SetSoftwareBitmap(resultBitmap);
            await encoder.FlushAsync();
        }

        return outputFile.Path;
    }

    private static SoftwareBitmap ApplyMaskAsAlpha(SoftwareBitmap original, SoftwareBitmap mask)
    {
        int width = original.PixelWidth;
        int height = original.PixelHeight;

        SoftwareBitmap gray = mask.BitmapPixelFormat == BitmapPixelFormat.Gray8
            ? mask
            : SoftwareBitmap.Convert(mask, BitmapPixelFormat.Gray8);

        byte[] originalPixels = new byte[4 * width * height];
        byte[] maskPixels = new byte[width * height];
        original.CopyToBuffer(originalPixels.AsBuffer());
        gray.CopyToBuffer(maskPixels.AsBuffer());

        byte[] resultPixels = new byte[4 * width * height];
        for (int i = 0; i < maskPixels.Length; i++)
        {
            int px = i * 4;
            int m = 255 - maskPixels[i];
            resultPixels[px + 0] = (byte)(originalPixels[px + 0] * m / 255);
            resultPixels[px + 1] = (byte)(originalPixels[px + 1] * m / 255);
            resultPixels[px + 2] = (byte)(originalPixels[px + 2] * m / 255);
            resultPixels[px + 3] = (byte)(originalPixels[px + 3] * m / 255);
        }

        SoftwareBitmap result = new(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
        result.CopyFromBuffer(resultPixels.AsBuffer());
        return result;
    }
}
