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
using System.Linq;

namespace Simple_Icon_File_Maker.Controls;

public sealed partial class PreviewStack : UserControl
{
    private readonly string imagePath;
    private Dictionary<int, string> imagePaths = new();
    private List<IconSize> chosenSizes;
    private Size? SourceImageSize;
    private MagickImage mainImage;

    public bool IsZoomingPreview { get; set; } = false;

    public bool CanRefresh => CheckIfRefreshIsNeeded();

    public PreviewStack(string path, List<IconSize> sizes)
    {
        chosenSizes = new(sizes);
        imagePath = path;
        mainImage = new(path);
        InitializeComponent();
    }

    public void ClearChildren()
    {
        UIElementCollection uIElements = PreviewStackPanel.Children;

        foreach (UIElement element in uIElements)
            if (element is PreviewImage previewImage)
                previewImage.Clear();

        PreviewStackPanel.Children.Clear();
    }

    public async Task SaveIconAsync(string outputPath)
    {
        MagickImageCollection collection = new();

        foreach ((_, string path ) in imagePaths)
            collection.Add(path);

        await Task.Run(async () =>
        {
            await collection.WriteAsync(outputPath);

            IcoOptimizer icoOpti = new()
            {
                OptimalCompression = true
            };
            icoOpti.Compress(outputPath);
        });
    }

    public async Task SaveAllImagesAsync(string outputPath)
    {
        await SaveIconAsync(outputPath);

        string outputFolderPath = Path.GetDirectoryName(outputPath) ?? string.Empty;

        if (!Directory.Exists(outputFolderPath) || string.IsNullOrWhiteSpace(outputFolderPath))
            return;

        StorageFolder outputFolder = await StorageFolder.GetFolderFromPathAsync(outputFolderPath);

        foreach ((_, string path) in imagePaths)
        {
            StorageFile imageFile = await StorageFile.GetFileFromPathAsync(path);
            
            if (imageFile is null)
                continue;

            await imageFile.CopyAsync(outputFolder);
        }
    }

    public async Task<bool> GeneratePreviewImagesAsync()
    {
        ImagesProcessingProgressRing.Visibility = Visibility.Visible;
        ImagesProcessingProgressRing.IsActive = true;

        string? openedPath = Path.GetDirectoryName(imagePath);
        string? name = Path.GetFileNameWithoutExtension(imagePath);

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

        foreach (IconSize iconSize in chosenSizes)
        {
            iconSize.IsEnabled = true;
            if (iconSize.SideLength > smallerSide)
                iconSize.IsEnabled = false;
        }

        if (string.IsNullOrWhiteSpace(imagePath) == true)
        {
            ClearOutputImages();
            return false;
        }

        try
        {
            _ = await imgFactory.CreateAsync(imagePath);
        }
        catch (Exception)
        {
            ClearOutputImages();
            return false;
        }

        using IMagickImage<ushort> firstPassImage = await imgFactory.CreateAsync(imagePath);
        IMagickGeometry size = geoFactory.Create(
            Math.Max(SourceImageSize.Value.Width, SourceImageSize.Value.Height));
        size.IgnoreAspectRatio = false;
        size.FillArea = true;

        firstPassImage.Extent(size, Gravity.Center, MagickColor.FromRgba(0, 0, 0, 0));

        await firstPassImage.WriteAsync(croppedImagePath);

        MagickImageCollection collection = new();

        List<int> selectedSizes = chosenSizes.Where(s => s.IsSelected == true).Select(s => s.SideLength).ToList();

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

            collection.Add(iconPath);
            imagePaths.Add(sideLength, iconPath);
        }

        try
        {
            await UpdatePreviewsAsync();
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

    private void ClearOutputImages()
    {
        PreviewStackPanel.Children.Clear();

        ImagesProcessingProgressRing.IsActive = false;
        ImagesProcessingProgressRing.Visibility = Visibility.Collapsed;
    }

    public async Task UpdatePreviewsAsync()
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
        UpdateSizeAndZoom();
        await Task.CompletedTask;
    }

    private bool CheckIfRefreshIsNeeded()
    {
        if (imagePaths.Count < 1)
            return true;

        List<int> selectedSideLengths = chosenSizes
                                            .Where(i => i.IsSelected)
                                            .Select(i => i.SideLength)
                                            .ToList();

        List<int> generatedSideLengths = imagePaths.Keys.ToList();

        if (selectedSideLengths.Count != generatedSideLengths.Count)
            return true;

        return generatedSideLengths.All(selectedSideLengths.Contains);
    }

    public void UpdateSizeAndZoom()
    {
        UIElementCollection previewBoxes = PreviewStackPanel.Children;

        foreach (var child in previewBoxes)
        {
            if (child is PreviewImage img)
            {
                if (!double.IsNaN(ActualWidth) && ActualWidth > 40)
                    img.ZoomedWidthSpace = (int)ActualWidth - 24;
                img.ZoomPreview = IsZoomingPreview;
            }
        }
    }
}
