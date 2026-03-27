using ImageMagick;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUIEx;

namespace Simple_Icon_File_Maker.Controls;

public sealed partial class PreviewImage : UserControl
{
    private readonly string OriginalName = string.Empty;
    private readonly StorageFile _imageFile;
    private readonly int _sideLength = 0;

    public PreviewImage(StorageFile imageFile, int sideLength, string originalName, bool showCheckerBackground)
    {
        InitializeComponent();
        _imageFile = imageFile;
        _sideLength = sideLength;
        ShowCheckerBackground = showCheckerBackground;
        ToolTipService.SetToolTip(this, $"{sideLength} x {sideLength}");
        OriginalName = originalName;
        ActualThemeChanged += (_, _) =>
        {
            int size = isZooming ? ZoomedWidthSpace : _sideLength;
            mainImageCanvas.Background = _showCheckerBackground
                ? CreateCheckerBrush(size, ActualTheme)
                : new SolidColorBrush(Colors.Transparent);
        };
    }

    private bool isZooming = false;
    private bool _showCheckerBackground = true;
    public int ZoomedWidthSpace = 100;

    public bool ShowCheckerBackground
    {
        get => _showCheckerBackground;
        set
        {
            if (value != _showCheckerBackground)
            {
                _showCheckerBackground = value;
                int size = isZooming ? ZoomedWidthSpace : _sideLength;
                mainImageCanvas.Background = _showCheckerBackground
                    ? CreateCheckerBrush(size, ActualTheme)
                    : new SolidColorBrush(Colors.Transparent);
            }
        }
    }

    public bool ZoomPreview
    {
        get => isZooming;
        set
        {
            if (value != isZooming)
            {
                isZooming = value;
                LoadImageOnToCanvas();
                InvalidateMeasure();
            }
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double dim = Math.Min(finalSize.Width, finalSize.Height);
        mainImageCanvas.Arrange(new Rect(new Point((finalSize.Width - dim) / 2, (finalSize.Height - dim) / 2), new Size(dim, dim)));
        return finalSize;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int size = isZooming ? ZoomedWidthSpace : _sideLength;
        return new Size(size, size);
    }

    private async void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        FileSavePicker savePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        string extension = Path.GetExtension(_imageFile.Path);
        savePicker.FileTypeChoices.Add("Image", [extension]);
        savePicker.SuggestedFileName = $"{OriginalName}-{_sideLength}x{_sideLength}";
        savePicker.DefaultFileExtension = extension;

        InitializeWithWindow.Initialize(savePicker, App.MainWindow.GetWindowHandle());

        StorageFile file = await savePicker.PickSaveFileAsync();

        if (file is null)
            return;

        try
        {
            StorageFolder folder = await file.GetParentAsync();
            if (folder is null)
                return;

            MagickImage magickImage = new(_imageFile.Path);
            await magickImage.WriteAsync(file.Path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save out single image: {ex.Message}");
        }
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        LoadImageOnToCanvas();
    }

    private void LoadImageOnToCanvas()
    {
        mainImageCanvas.Children.Clear();

        int size = isZooming ? ZoomedWidthSpace : _sideLength;
        mainImageCanvas.Background = ShowCheckerBackground
            ? CreateCheckerBrush(size, ActualTheme)
            : new SolidColorBrush(Colors.Transparent);

        // from StackOverflow
        // user:
        // https://stackoverflow.com/users/403671/simon-mourier
        // Post: read on 2023/12/28
        // https://stackoverflow.com/a/76760568/7438031

        // get visual layer's compositor
        Compositor compositor = ElementCompositionPreview.GetElementVisual(mainImageCanvas).Compositor;

        // create a surface brush, this is where we can use NearestNeighbor interpolation
        CompositionSurfaceBrush brush = compositor.CreateSurfaceBrush();
        brush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;

        // create a visual
        SpriteVisual imageVisual = compositor.CreateSpriteVisual();
        imageVisual.Brush = brush;
        imageVisual.BackfaceVisibility = CompositionBackfaceVisibility.Hidden;

        // load the image
        LoadedImageSurface image = LoadedImageSurface.StartLoadFromUri(new Uri(_imageFile.Path));
        brush.Surface = image;

        // set the visual size when the image has loaded
        image.LoadCompleted += (s, e) =>
        {
            // choose any size here
            try
            {
                imageVisual.Size = new System.Numerics.Vector2(size, size);
                image.Dispose();
            }
            catch { }
        };

        // add the visual as a child to canvas
        Grid tempGrid = new()
        {
            Background = new SolidColorBrush(Colors.Transparent)
        };
        ElementCompositionPreview.SetElementChildVisual(tempGrid, imageVisual);
        mainImageCanvas.Children.Add(tempGrid);
    }

    private static ImageBrush CreateCheckerBrush(int size, ElementTheme theme)
    {
        int tileSize = 8;
        WriteableBitmap bitmap = new(size, size);

        // Light mode: #F0F0F0 / #C4C4C4  —  Dark mode: #404040 / #2A2A2A
        bool isDark = theme == ElementTheme.Dark;
        byte tileLight = isDark ? (byte)0x40 : (byte)0xF0;
        byte tileDark  = isDark ? (byte)0x2A : (byte)0xC4;

        byte[] pixels = new byte[size * size * 4]; // BGRA format
        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                bool isLightTile = ((row / tileSize) + (col / tileSize)) % 2 == 0;
                byte val = isLightTile ? tileLight : tileDark;
                int idx = (row * size + col) * 4;
                pixels[idx]     = val; // B
                pixels[idx + 1] = val; // G
                pixels[idx + 2] = val; // R
                pixels[idx + 3] = 255; // A
            }
        }

        using Stream stream = bitmap.PixelBuffer.AsStream();
        stream.Write(pixels, 0, pixels.Length);

        return new ImageBrush { ImageSource = bitmap, Stretch = Stretch.Fill };
    }

    private async void ImagePreview_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        DragOperationDeferral deferral = args.GetDeferral();
        StorageFolder folder = ApplicationData.Current.LocalCacheFolder;
        string imageNameFileName = $"{OriginalName}-{_sideLength}x{_sideLength}.png";
        StorageFile file = await folder.CreateFileAsync(imageNameFileName, CreationCollisionOption.ReplaceExisting);
        await _imageFile.CopyAndReplaceAsync(file);

        args.Data.SetStorageItems(new[] { file });
        args.Data.RequestedOperation = DataPackageOperation.Copy;
        deferral.Complete();
    }

    public void Clear()
    {
        mainImageCanvas.Children.Clear();
        mainImageCanvas.DragStarting -= ImagePreview_DragStarting;
    }
}
