using ImageMagick;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Simple_Icon_File_Maker.Controls;

public sealed partial class PreviewImage : UserControl
{
    readonly string OriginalName = string.Empty;
    readonly StorageFile _imageFile;
    readonly int _sideLength = 0;

    public PreviewImage(StorageFile imageFile, int sideLength, string originalName)
    {
        InitializeComponent();
        _imageFile = imageFile;
        _sideLength = sideLength;
        ToolTipService.SetToolTip(this, $"{sideLength} x {sideLength}");
        OriginalName = originalName;
    }

    private bool isZooming = false;
    public int ZoomedWidthSpace = 100;

    public bool ZoomPreview
    {
        get
        {
            return isZooming;
        }
        set
        {
            if (value != isZooming)
            {
                isZooming = value;
                mainImageCanvas.Children.Clear();
                InvalidateMeasure();
                InvalidateArrange();
            }
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var dim = Math.Min(finalSize.Width, finalSize.Height);
        mainImageCanvas.Arrange(new Rect(new Point((finalSize.Width - dim) / 2, (finalSize.Height - dim) / 2), new Size(dim, dim)));
        LoadImageOnToCanvas();
        return finalSize;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dim = Math.Min(availableSize.Width, availableSize.Height);
        // smallerAvailableSize = (int)dim;
        if (double.IsPositiveInfinity(dim))
            dim = 3000;
        return new Size(dim, dim);
    }

    private async void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        FileSavePicker savePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        string extension = Path.GetExtension(_imageFile.Path);
        savePicker.FileTypeChoices.Add("Image", new List<string>() { extension });
        savePicker.SuggestedFileName = $"{OriginalName}-{_sideLength}x{_sideLength}";
        savePicker.DefaultFileExtension = extension;

        Window saveWindow = new();
        IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(savePicker, windowHandleSave);

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

        // load the image
        LoadedImageSurface image = LoadedImageSurface.StartLoadFromUri(new Uri(_imageFile.Path));
        brush.Surface = image;

        int size = isZooming ? ZoomedWidthSpace : _sideLength;
        Width = size;
        Height = size;

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
        Grid tempGrid = new();
        ElementCompositionPreview.SetElementChildVisual(tempGrid, imageVisual);
        mainImageCanvas.Children.Add(tempGrid);
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
