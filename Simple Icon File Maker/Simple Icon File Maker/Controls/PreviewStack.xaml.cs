using ImageMagick;
using ImageMagick.Factories;
using ImageMagick.ImageOptimizers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_Icon_File_Maker.Models;
using System.Diagnostics;
using System.Drawing;
using Windows.Storage;

namespace Simple_Icon_File_Maker.Controls;

public sealed partial class PreviewStack : UserControl
{
    private string imagePath;
    private readonly List<(string, string)> imagePaths = [];
    private Size? SourceImageSize;
    private readonly MagickImage mainImage;
    private readonly string iconRootString;
    public List<IconSize> ChosenSizes { get; private set; }

    public bool IsZoomingPreview { get; set; } = false;

    public bool CanRefresh => CheckIfRefreshIsNeeded();

    public int SmallerSourceSide { get; private set; }

    public PreviewStack(string path, List<IconSize> sizes, bool showTitle = false)
    {
        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        iconRootString = sf.Path;

        ChosenSizes = [.. sizes];
        imagePath = path;
        mainImage = new(path);

        InitializeComponent();

        if (showTitle)
            FileNameText.Text = Path.GetFileName(imagePath);
    }

    public async Task<bool> InitializeAsync(IProgress<int> progress)
    {
        string extension = Path.GetExtension(imagePath);

        if (extension.Equals(".ico", StringComparison.InvariantCultureIgnoreCase))
            return await OpenIconFile(progress);
        else
            return await GeneratePreviewImagesAsync(progress);
    }

    public bool ChooseTheseSizes(IEnumerable<IconSize> sizes)
    {
        // Optimized: Avoid LINQ and multiple iterations
        ChosenSizes.Clear();
        
     foreach (IconSize size in sizes)
     {
         if (size.IsSelected && size.IsEnabled && size.SideLength <= SmallerSourceSide)
          ChosenSizes.Add(size);
        }

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

    public async Task SaveIconAsync(string outputPath = "")
    {
        // Optimized: Use for loop instead of foreach with tuple deconstruction
        MagickImageCollection collection = [];

        for (int i = 0; i < imagePaths.Count; i++)
    collection.Add(imagePaths[i].Item2);

        if (string.IsNullOrWhiteSpace(outputPath))
     {
            outputPath = Path.Combine(
       Path.GetDirectoryName(imagePath) ?? string.Empty,
       $"{Path.GetFileNameWithoutExtension(imagePath)}.ico");
        }

        // Optimized: Remove unnecessary async wrapper
        await Task.Run(() =>
        {
  collection.Write(outputPath);

            IcoOptimizer icoOpti = new()
            {
       OptimalCompression = true
            };
    icoOpti.Compress(outputPath);
        });
    }

    public async Task SaveAllImagesAsync(string outputPath = "")
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
   outputPath = Path.Combine(
     Path.GetDirectoryName(imagePath) ?? string.Empty,
        $"{Path.GetFileNameWithoutExtension(imagePath)}.ico");
   }

        await SaveIconAsync(outputPath);

    string outputFolderPath = Path.GetDirectoryName(outputPath) ?? string.Empty;

    if (!Directory.Exists(outputFolderPath) || string.IsNullOrWhiteSpace(outputFolderPath))
 return;

 string outputBaseFileName = Path.GetFileNameWithoutExtension(outputPath);
   StorageFolder outputFolder = await StorageFolder.GetFolderFromPathAsync(outputFolderPath);

        for (int i = 0; i < imagePaths.Count; i++)
        {
   string path = imagePaths[i].Item2;
     StorageFile imageFile = await StorageFile.GetFileFromPathAsync(path);

  if (imageFile is null)
        continue;

            string justFileName = Path.GetFileNameWithoutExtension(path);
            // Get the numbers from the right side of the string which is the side length
   // This is because random numbers are generated to do the composition stuff
            // Ex: we want to turn "904466899Image16.png" into "outputName-16.png"
    int imageIndex = justFileName.IndexOf("Image", StringComparison.Ordinal);
            if (imageIndex < 0)
                continue;

     string sideLength = justFileName.Substring(imageIndex + 5);
     string newName = $"{outputBaseFileName}-{sideLength}.png";

            await imageFile.CopyAsync(outputFolder, newName, NameCollisionOption.ReplaceExisting);
        }
  }

    public async Task<bool> GeneratePreviewImagesAsync(IProgress<int> progress, string path = "")
    {
  if (!string.IsNullOrWhiteSpace(path))
            imagePath = path;

     string? openedPath = Path.GetDirectoryName(imagePath);
        string? name = Path.GetFileNameWithoutExtension(imagePath);

        if (openedPath is null || name is null)
  return false;

        ImagesProgressBar.Value = 0;
 progress.Report(0);
  
        LoadingText.Text = ChosenSizes.Count == 1 
   ? $"Generating {ChosenSizes.Count} preview for {name}..." 
     : $"Generating {ChosenSizes.Count} previews for {name}...";

        TextAndProgressBar.Visibility = Visibility.Visible;

        string croppedImagePath = Path.Combine(iconRootString, $"{name}Cropped.png");
        
        if (!Directory.Exists(iconRootString))
          Directory.CreateDirectory(iconRootString);

        MagickImageFactory imgFactory = new();
        MagickGeometryFactory geoFactory = new();

   progress.Report(10);
     ImagesProgressBar.Value = 10;
        SourceImageSize ??= new Size((int)mainImage.Width, (int)mainImage.Height);

        SmallerSourceSide = Math.Min((int)mainImage.Width, (int)mainImage.Height);

        int smallerSide = Math.Min(SourceImageSize.Value.Width, SourceImageSize.Value.Height);

        imagePaths.Clear();
        PreviewStackPanel.Children.Clear();

        // Optimized: Single-pass update with conditional assignment
        for (int i = 0; i < ChosenSizes.Count; i++)
        {
   ChosenSizes[i].IsEnabled = ChosenSizes[i].SideLength <= smallerSide;
        }

        if (string.IsNullOrWhiteSpace(imagePath))
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

        progress.Report(15);
        ImagesProgressBar.Value = 15;
     
        using IMagickImage<ushort> firstPassImage = await imgFactory.CreateAsync(imagePath);
        IMagickGeometry size = geoFactory.Create(
          (uint)Math.Max(SourceImageSize.Value.Width, SourceImageSize.Value.Height));
        size.IgnoreAspectRatio = false;
        size.FillArea = true;

        MagickColor transparent = new("#00000000");
        firstPassImage.Extent(size, Gravity.Center, transparent);

        await firstPassImage.WriteAsync(croppedImagePath, MagickFormat.Png32);

        // Optimized: Use for loop instead of LINQ for filtering
        List<int> selectedSizes = new(ChosenSizes.Count);
      for (int i = 0; i < ChosenSizes.Count; i++)
        {
  if (ChosenSizes[i].IsSelected && ChosenSizes[i].SideLength <= smallerSide)
                selectedSizes.Add(ChosenSizes[i].SideLength);
   }

    int baseAtThisPoint = 20;
      progress.Report(baseAtThisPoint);
   ImagesProgressBar.Value = baseAtThisPoint;

    int totalImages = selectedSizes.Count;
 if (totalImages == 0)
        {
        TextAndProgressBar.Visibility = Visibility.Collapsed;
 return true;
        }

        int halfChunkPerImage = (int)((100 - baseAtThisPoint) / (float)(totalImages * 2));

        // Optimized: Pre-allocate collection capacity
        if (imagePaths.Capacity < totalImages)
          imagePaths.Capacity = totalImages;

        int currentLocation = 0;

        foreach (int sideLength in selectedSizes)
        {
    using IMagickImage<ushort> image = await imgFactory.CreateAsync(croppedImagePath);

    currentLocation++;
int progressValue = baseAtThisPoint + (currentLocation * halfChunkPerImage);
  progress.Report(progressValue);
      ImagesProgressBar.Value = progressValue;

  IMagickGeometry iconSize = geoFactory.Create((uint)sideLength, (uint)sideLength);
            iconSize.IgnoreAspectRatio = false;

            // Optimized: Remove unnecessary Task.Run wrapper for short CPU operations
         if (smallerSide > sideLength)
       {
   image.Scale(iconSize);
     image.Sharpen();
  }

    string iconPath = Path.Combine(iconRootString, $"{Random.Shared.Next()}Image{sideLength}.png");

            if (File.Exists(iconPath))
     File.Delete(iconPath);

   await image.WriteAsync(iconPath, MagickFormat.Png32);

          imagePaths.Add((sideLength.ToString(), iconPath));

    currentLocation++;
 progressValue = baseAtThisPoint + (currentLocation * halfChunkPerImage);
       progress.Report(progressValue);
            ImagesProgressBar.Value = progressValue;
        }

        try
        {
            await UpdatePreviewsAsync();
   }
        catch (Exception ex)
{
     Debug.WriteLine($"Generating Icons Exception: {ex.Message}");
        return false;
        }
        finally
   {
         TextAndProgressBar.Visibility = Visibility.Collapsed;
        }
        
        return true;
    }

    private async Task<bool> OpenIconFile(IProgress<int> progress)
    {
        if (string.IsNullOrEmpty(imagePath))
            return false;

        TextAndProgressBar.Visibility = Visibility.Visible;
        ImagesProgressBar.Value = 0;

        progress.Report(0);
        ChosenSizes.Clear();
        imagePaths.Clear();
        PreviewStackPanel.Children.Clear();

        MagickImageCollection collection = new(imagePath);

        // Optimized: Replace LINQ with manual loop for simple aggregation
        int largestWidth = 0;
     int largestHeight = 0;
        
  foreach (IMagickImage img in collection)
        {
     if (img.Width > largestWidth)
        largestWidth = (int)img.Width;
   if (img.Height > largestHeight)
       largestHeight = (int)img.Height;
        }

     SmallerSourceSide = Math.Min(largestWidth, largestHeight);

        int currentLocation = 0;
        int totalImages = collection.Count;
        
  foreach (MagickImage image in collection.Cast<MagickImage>())
        {
   Debug.WriteLine($"Image: {image}");
         string imageName = $"{image}.png";

string pathForSingleImage = Path.Combine(iconRootString, imageName);
          await image.WriteAsync(pathForSingleImage, MagickFormat.Png32);

            imagePaths.Add((((int)image.Width).ToString(), pathForSingleImage));

            IconSize iconSizeOfIconFrame = new((int)image.Width)
       {
                IsSelected = true,
       };
  ChosenSizes.Add(iconSizeOfIconFrame);

            int sideLength = (int)image.Width;
         StorageFile imageSF = await StorageFile.GetFileFromPathAsync(pathForSingleImage);

  PreviewImage previewImage = new(imageSF, sideLength, imageName);
            PreviewStackPanel.Children.Add(previewImage);

         currentLocation++;
int percentageComplete = (int)((float)currentLocation / totalImages * 100);
      progress.Report(percentageComplete);
       ImagesProgressBar.Value = percentageComplete;
   }

TextAndProgressBar.Visibility = Visibility.Collapsed;
        return true;
    }

    private void ClearOutputImages()
    {
        PreviewStackPanel.Children.Clear();
        TextAndProgressBar.Visibility = Visibility.Collapsed;
    }

    public async Task UpdatePreviewsAsync()
    {
        string originalName = Path.GetFileNameWithoutExtension(imagePath);
        
        // Optimized: Use indexed for loop for better performance
  for (int i = 0; i < imagePaths.Count; i++)
        {
            (string sideLength, string path) = imagePaths[i];

            if (!int.TryParse(sideLength, out int sideLengthValue))
   continue;

       StorageFile imageSF = await StorageFile.GetFileFromPathAsync(path);

    PreviewImage image = new(imageSF, sideLengthValue, originalName);

   PreviewStackPanel.Children.Add(image);
        }
  
        UpdateSizeAndZoom();
    }

    private bool CheckIfRefreshIsNeeded()
    {
        if (imagePaths.Count < 1)
            return true;

   // Optimized: Use for loops instead of LINQ for better performance
        List<int> selectedSideLengths = new(ChosenSizes.Count);
        for (int i = 0; i < ChosenSizes.Count; i++)
        {
   if (ChosenSizes[i].IsSelected)
      selectedSideLengths.Add(ChosenSizes[i].SideLength);
}

        List<int> generatedSideLengths = new(imagePaths.Count);
 for (int i = 0; i < imagePaths.Count; i++)
{
            if (int.TryParse(imagePaths[i].Item1, out int sideLength))
     generatedSideLengths.Add(sideLength);
      }

        if (selectedSideLengths.Count != generatedSideLengths.Count)
   return true;

        // Optimized: Use HashSet for O(1) lookups instead of O(n²) All().Contains()
        HashSet<int> generatedSet = new(generatedSideLengths);
        for (int i = 0; i < selectedSideLengths.Count; i++)
   {
      if (!generatedSet.Contains(selectedSideLengths[i]))
           return true;
   }

   return false;
    }

    public void UpdateSizeAndZoom()
    {
        UIElementCollection previewBoxes = PreviewStackPanel.Children;

     // Optimized: Use indexed for loop instead of foreach
      for (int i = 0; i < previewBoxes.Count; i++)
    {
            if (previewBoxes[i] is PreviewImage img)
          {
                if (!double.IsNaN(ActualWidth) && ActualWidth > 40)
        img.ZoomedWidthSpace = (int)ActualWidth - 40;
         img.ZoomPreview = IsZoomingPreview;
            }
        }
    }
}
