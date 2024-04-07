using ImageMagick.ImageOptimizers;
using ImageMagick;
using Microsoft.UI.Xaml.Controls;
using Simple_Icon_File_Maker.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using System.Drawing;
using Microsoft.UI.Xaml;

namespace Simple_Icon_File_Maker.Controls;

public sealed partial class PreviewStack : UserControl
{
    private bool isZoomingPreview = false;
    private string imagePath = string.Empty;
    private Dictionary<int, string> imagePaths = new();
    private List<IconSize> LastRefreshSizes { get; set; } = new();
    private Size? SourceImageSize;
    private MagickImage mainImage;


    public PreviewStack(string path)
    {
        imagePath = path;
        mainImage = new(path);
        InitializeComponent();
    }

    public PreviewStack(Dictionary<int, string> imagePathsProp)
    {
        imagePaths = imagePathsProp;

        InitializeComponent();
    }
    private async Task<bool> GenerateIcons(string path, bool updatePreviews = false, bool saveAllFiles = false)
    {
        LastRefreshSizes.Clear();
        // ImagesProcessingProgressRing.Visibility = Visibility.Visible;
        // ImagesProcessingProgressRing.IsActive = true;

        string? openedPath = Path.GetDirectoryName(path);
        string? name = Path.GetFileNameWithoutExtension(path);

        if (openedPath is null || name is null)
            return false;

        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;

        IReadOnlyList<StorageFile> allFiles = await sf.GetFilesAsync();
        foreach (StorageFile? file in allFiles)
            await file?.DeleteAsync();

        string iconRootString = sf.Path;
        string croppedImagePath = Path.Combine(iconRootString, $"{name}Cropped.png");
        string iconOutputString = Path.Combine(openedPath, $"{name}.ico");
        if (Directory.Exists(iconRootString) == false)
            Directory.CreateDirectory(iconRootString);

        MagickImageFactory imgFactory = new();
        MagickGeometryFactory geoFactory = new();

        SourceImageSize ??= new Size(mainImage.Width, mainImage.Height);

        int smallerSide = Math.Min(SourceImageSize.Value.Width, SourceImageSize.Value.Height);

        foreach (IconSize iconSize in IconSizes)
        {
            iconSize.IsEnabled = true;
            if (iconSize.SideLength > smallerSide)
                iconSize.IsEnabled = false;
        }

        foreach (IconSize iconSize in IconSizes)
            if (iconSize.IsSelected)
                LastRefreshSizes.Add(new(iconSize));

        if (string.IsNullOrWhiteSpace(ImagePath) == true)
        {
            ClearOutputImages();
            return false;
        }

        try
        {
            _ = await imgFactory.CreateAsync(ImagePath);
        }
        catch (Exception)
        {
            ClearOutputImages();
            return false;
        }

        using IMagickImage<ushort> firstPassImage = await imgFactory.CreateAsync(ImagePath);
        IMagickGeometry size = geoFactory.Create(
            Math.Max(SourceImageSize.Value.Width, SourceImageSize.Value.Height));
        size.IgnoreAspectRatio = false;
        size.FillArea = true;

        firstPassImage.Extent(size, Gravity.Center, MagickColor.FromRgba(0, 0, 0, 0));

        await firstPassImage.WriteAsync(croppedImagePath);

        MagickImageCollection collection = new();
        Dictionary<int, string> imagePaths = new();

        List<int> selectedSizes = IconSizes.Where(s => s.IsSelected == true).Select(s => s.SideLength).ToList();

        foreach (int sideLength in selectedSizes)
        {
            using IMagickImage<ushort> image = await imgFactory.CreateAsync(croppedImagePath);
            if (smallerSide < sideLength)
                continue;

            IMagickGeometry iconSize = geoFactory.Create(sideLength, sideLength);
            iconSize.IgnoreAspectRatio = false;

            if (smallerSide > sideLength)
            {
                image.Scale(iconSize);
                image.Sharpen();
            }

            string iconPath = $"{iconRootString}\\{Random.Shared.Next()}Image{sideLength}.png";
            string outputImagePath = $"{openedPath}\\{name}{sideLength}.png";

            if (File.Exists(iconPath))
                File.Delete(iconPath);

            await image.WriteAsync(iconPath, MagickFormat.Png32);

            if (saveAllFiles == true)
                await image.WriteAsync(outputImagePath, MagickFormat.Png32);

            collection.Add(iconPath);
            imagePaths.Add(sideLength, iconPath);
        }

        try
        {
            if (updatePreviews == true)
                await UpdatePreviewsAsync(imagePaths);
            else
            {
                await collection.WriteAsync(iconOutputString);

                IcoOptimizer icoOpti = new()
                {
                    OptimalCompression = true
                };
                icoOpti.Compress(iconOutputString);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Generating Icons Exception " + ex.Message);
            return false;
        }
        finally
        {
            ImagesProcessingProgressRing.IsActive = false;
            ImagesProcessingProgressRing.Visibility = Visibility.Collapsed;
        }
        return true;
    }

    private async Task UpdatePreviewsAsync()
    {
        string originalName = Path.GetFileNameWithoutExtension(imagePath);
        foreach (var pair in imagePaths)
        {
            if (pair.Value is not string imagePath)
                return;

            int sideLength = pair.Key;

            StorageFile imageSF = await StorageFile.GetFileFromPathAsync(imagePath);

            PreviewImage image = new(imageSF, sideLength, originalName);

            PreviewStackPanel.Children.Add(image);
        }
        SetPreviewsZoom(isZoomingPreview);
        await Task.CompletedTask;
    }
    
    private async Task SourceImageUpdated(string fileName)
    {
        PreviewStackPanel.Children.Clear();
        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        string pathAndName = Path.Combine(sf.Path, fileName);
        bool success = await GenerateIcons(pathAndName, true);

        SaveBTN.IsEnabled = success;
        SaveAllBTN.IsEnabled = success;

        if (success)
            ConfigUiShow();
        else
            ConfigUiWelcome();
    }

    private void SetPreviewsZoom(bool zoomLevel)
    {
        UIElementCollection previewBoxes = PreviewStackPanel.Children;

        foreach (var child in previewBoxes)
        {
            if (child is PreviewImage img)
            {
                if (!double.IsNaN(PreviewCard.ActualWidth) && PreviewCard.ActualWidth > 40)
                    img.ZoomedWidthSpace = (int)PreviewCard.ActualWidth - 24;
                img.ZoomPreview = isZoomingPreview;
            }
        }
    }

    private bool CheckIfRefreshIsNeeded()
    {
        if (LastRefreshSizes.Count < 1)
            return false;

        List<IconSize> currentSizes = new(IconSizes.Where(i => i.IsSelected).ToList());
        bool isCurrentUnChanged = true;

        for (int i = 0; i < currentSizes.Count; i++)
        {
            if (!currentSizes[i].Equals(LastRefreshSizes[i]))
            {
                isCurrentUnChanged = false;
                break;
            }
        }

        return isCurrentUnChanged;
    }
}
