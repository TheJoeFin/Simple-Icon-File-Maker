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
        List<IconSize> selectedSizes = [.. sizes.Where(x => x.IsSelected && x.IsEnabled && x.SideLength <= SmallerSourceSide)];
        ChosenSizes.Clear();
        ChosenSizes = [.. selectedSizes];

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
        using MagickImageCollection collection = [];

        foreach ((_, string path) in imagePaths)
            collection.Add(path);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = Path.Combine(Path.GetDirectoryName(imagePath) ?? string.Empty,
                $"{Path.GetFileNameWithoutExtension(imagePath)}.ico");
        }

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

    public async Task SaveAllImagesAsync(string outputPath = "")
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = Path.Combine(Path.GetDirectoryName(imagePath) ?? string.Empty,
                $"{Path.GetFileNameWithoutExtension(imagePath)}.ico");
        }

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

            NameCollisionOption option = NameCollisionOption.ReplaceExisting;

            await imageFile.CopyAsync(outputFolder, newName, option);
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
        if (ChosenSizes.Count == 1)
          LoadingText.Text = $"Generating {ChosenSizes.Count} preview for {name}...";
   else
       LoadingText.Text = $"Generating {ChosenSizes.Count} previews for {name}...";

        TextAndProgressBar.Visibility = Visibility.Visible;

      if (Directory.Exists(iconRootString) == false)
        Directory.CreateDirectory(iconRootString);

 MagickGeometryFactory geoFactory = new();

        progress.Report(10);
   ImagesProgressBar.Value = 10;
        SourceImageSize ??= new Size((int)mainImage.Width, (int)mainImage.Height);

      SmallerSourceSide = Math.Min((int)mainImage.Width, (int)mainImage.Height);

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

        byte[]? croppedImageBytes = null;
   
        try
    {
            progress.Report(15);
            ImagesProgressBar.Value = 15;
            
        // Prepare the base image in memory instead of writing to disk
        using (var firstPassImage = mainImage.Clone())
          {
    IMagickGeometry size = geoFactory.Create(
   (uint)Math.Max(SourceImageSize.Value.Width, SourceImageSize.Value.Height));
              size.IgnoreAspectRatio = false;
       size.FillArea = true;

      MagickColor transparent = new("#00000000");
       firstPassImage.Extent(size, Gravity.Center, transparent);
         
      // Keep the image in memory as byte array
      croppedImageBytes = firstPassImage.ToByteArray(MagickFormat.Png32);
  }

       List<int> selectedSizes = [.. ChosenSizes
     .Where(s => s.IsSelected == true)
   .Select(s => s.SideLength)];

    int baseAtThisPoint = 20;
 progress.Report(baseAtThisPoint);
            ImagesProgressBar.Value = baseAtThisPoint;

    int totalImages = selectedSizes.Count;
  
   // Use concurrent collection for thread-safe additions
            var generatedPaths = new System.Collections.Concurrent.ConcurrentBag<(string, string)>();
            int processedCount = 0;
    
     // Process images in parallel
         await Parallel.ForEachAsync(selectedSizes, new ParallelOptions 
   { 
        MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, selectedSizes.Count)
 }, async (sideLength, ct) =>
            {
    if (smallerSide < sideLength)
 return;

       // Create image from byte array (thread-safe)
         using var image = new MagickImage(croppedImageBytes);
  
                IMagickGeometry iconSize = geoFactory.Create((uint)sideLength, (uint)sideLength);
  iconSize.IgnoreAspectRatio = false;

                if (smallerSide > sideLength)
              {
         // Process synchronously - these are CPU-bound operations
         image.Scale(iconSize);
    image.Sharpen();
        }

   string iconPath = $"{iconRootString}\\{Random.Shared.Next()}Image{sideLength}.png";

                if (File.Exists(iconPath))
              File.Delete(iconPath);

   await image.WriteAsync(iconPath, MagickFormat.Png32);

 generatedPaths.Add((sideLength.ToString(), iconPath));

     // Update progress (thread-safe increment)
      int current = Interlocked.Increment(ref processedCount);
       int percentageComplete = baseAtThisPoint + (int)((float)current / totalImages * (100 - baseAtThisPoint));
         
     // UI updates must be on UI thread
   await Task.Run(() =>
         {
        progress.Report(percentageComplete);
  });
            });

            // Add all generated paths to the list
  imagePaths.AddRange(generatedPaths.OrderBy(p => int.Parse(p.Item1)));
     
            // Update progress bar on UI thread
            ImagesProgressBar.Value = 100;
      progress.Report(100);

            await UpdatePreviewsAsync();
     return true;
  }
        catch (Exception ex)
        {
      Debug.WriteLine("Generating Icons Exception " + ex.Message);
        ClearOutputImages();
       return false;
     }
     finally
    {
         TextAndProgressBar.Visibility = Visibility.Collapsed;
        }
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

        using MagickImageCollection collection = new(imagePath);
        List<(string, string)> iconImages = [];

        int largestWidth = (int)collection.Select(x => x.Width).Max();
        int largestHeight = (int)collection.Select(x => x.Height).Max();

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

            iconImages.Add((image.ToString(), imagePath));
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
        foreach ((string sideLength, string path) pair in imagePaths)
        {
            if (pair.path is not string imagePath)
                continue;

            if (!int.TryParse(pair.sideLength, out int sideLength))
                continue;

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

        List<int> selectedSideLengths = [.. ChosenSizes
                                            .Where(i => i.IsSelected)
                                            .Select(i => i.SideLength)];

        List<int> generatedSideLengths = [];

        foreach ((string sideLength, string path) pair in imagePaths)
            if (int.TryParse(pair.sideLength, out int sideLength))
                generatedSideLengths.Add(sideLength);

        if (selectedSideLengths.Count != generatedSideLengths.Count)
            return true;

        return !generatedSideLengths.All(selectedSideLengths.Contains);
    }

    public void UpdateSizeAndZoom()
    {
        UIElementCollection previewBoxes = PreviewStackPanel.Children;

        foreach (UIElement? child in previewBoxes)
        {
            if (child is PreviewImage img)
            {
                if (!double.IsNaN(ActualWidth) && ActualWidth > 40)
                    img.ZoomedWidthSpace = (int)ActualWidth - 40;
                img.ZoomPreview = IsZoomingPreview;
            }
        }
    }
}
