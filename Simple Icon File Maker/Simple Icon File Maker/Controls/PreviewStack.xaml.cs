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
    private Size? SourceImageSize;
    private readonly MagickImage mainImage;
    private readonly string iconRootString;
    public List<IconSize> ChosenSizes { get; private set; }

    public bool IsZoomingPreview { get; set; } = false;

    public bool CanRefresh => CheckIfRefreshIsNeeded();

    public PreviewStack(string path, List<IconSize> sizes)
    {
        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        iconRootString = sf.Path;

        ChosenSizes = new(sizes);
        imagePath = path;
        mainImage = new(path);

        InitializeComponent();
    }

    public async Task<bool> InitializeAsync()
    {
        string extension = Path.GetExtension(imagePath);

        if (extension.Equals(".ico", StringComparison.InvariantCultureIgnoreCase))
            return await OpenIconFile();
        else
            return await GeneratePreviewImagesAsync();
    }

    public bool ChooseTheseSizes(IEnumerable<IconSize> sizes)
    {
        List<IconSize> selectedSizes = sizes.Where(x => x.IsSelected && x.IsEnabled).ToList();
        ChosenSizes.Clear();
        ChosenSizes = new(selectedSizes);

        return CheckIfRefreshIsNeeded();
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

        string outputBaseFileName = Path.GetFileNameWithoutExtension(outputPath);
        StorageFolder outputFolder = await StorageFolder.GetFolderFromPathAsync(outputFolderPath);

        foreach ((_, string path) in imagePaths)
        {
            StorageFile imageFile = await StorageFile.GetFileFromPathAsync(path);

            if (imageFile is null)
                continue;

            string justFileName = Path.GetFileNameWithoutExtension(path);
            // get the numbers from the right side of the string which is the side length
            // this is because random numbers are generated to do the composition stuff
            // ex: we want to turn "904466899Image16.png" into "outputName-16.png"
            string sideLength = justFileName.Split("Image")[1];
            string newName = $"{outputBaseFileName}-{sideLength}.png";

            await imageFile.CopyAsync(outputFolder, newName);
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

        string croppedImagePath = Path.Combine(iconRootString, $"{name}Cropped.png");
        string iconOutputString = Path.Combine(openedPath, $"{name}.ico");
        if (Directory.Exists(iconRootString) == false)
            Directory.CreateDirectory(iconRootString);

        MagickImageFactory imgFactory = new();
        MagickGeometryFactory geoFactory = new();

        SourceImageSize ??= new Size(mainImage.Width, mainImage.Height);

        int smallerSide = Math.Min(SourceImageSize.Value.Width, SourceImageSize.Value.Height);

        imagePaths.Clear();
        PreviewStackPanel.Children.Clear();

        foreach (IconSize iconSize in ChosenSizes)
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

        List<int> selectedSizes = ChosenSizes.Where(s => s.IsSelected == true).Select(s => s.SideLength).ToList();

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

    private async Task<bool> OpenIconFile()
    {
        if (string.IsNullOrEmpty(imagePath))
        {
            return false;
        }

        ImagesProcessingProgressRing.Visibility = Visibility.Visible;
        ImagesProcessingProgressRing.IsActive = true;

        ChosenSizes.Clear();
        imagePaths.Clear();
        PreviewStackPanel.Children.Clear();

        MagickImageCollection collection = new(imagePath);
        Dictionary<int, string> iconImages = new();

        foreach (MagickImage image in collection.Cast<MagickImage>())
        {
            Debug.WriteLine($"Image: {image.Width}x{image.Height}");
            string imageName = $"{Path.GetFileNameWithoutExtension(imagePath)}-{image.Width}.png";

            string pathForSingleImage = Path.Combine(iconRootString,imageName);
            await image.WriteAsync(pathForSingleImage, MagickFormat.Png32);

            imagePaths.Add(image.Width, pathForSingleImage);

            iconImages.Add(image.Width, imagePath);
            IconSize iconSizeOfIconFrame = new(image.Width)
            {
                IsSelected = true,
            };
            ChosenSizes.Add(iconSizeOfIconFrame);

            int sideLength = image.Width;
            StorageFile imageSF = await StorageFile.GetFileFromPathAsync(pathForSingleImage);

            PreviewImage previewImage = new(imageSF, sideLength, imageName);
            PreviewStackPanel.Children.Add(previewImage);
        }

        ImagesProcessingProgressRing.Visibility = Visibility.Collapsed;
        ImagesProcessingProgressRing.IsActive = false;
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

        List<int> selectedSideLengths = ChosenSizes
                                            .Where(i => i.IsSelected)
                                            .Select(i => i.SideLength)
                                            .ToList();

        List<int> generatedSideLengths = imagePaths.Keys.ToList();

        if (selectedSideLengths.Count != generatedSideLengths.Count)
            return true;

        return !generatedSideLengths.All(selectedSideLengths.Contains);
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
