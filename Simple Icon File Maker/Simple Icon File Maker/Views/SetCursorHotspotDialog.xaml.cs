using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Simple_Icon_File_Maker;

public sealed partial class SetCursorHotspotDialog : ContentDialog
{
    public int HotspotX { get; private set; }
    public int HotspotY { get; private set; }
    public int ReferenceSize { get; private set; } = 256;

    private readonly string _imagePath;

    public bool Confirmed { get; private set; }

    public SetCursorHotspotDialog(string imagePath)
    {
        InitializeComponent();
        _imagePath = imagePath;
        PrimaryButtonClick += (_, _) => Confirmed = true;
    }

    private async void ContentDialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(_imagePath);
            using IRandomAccessStream stream = await file.OpenReadAsync();

            BitmapImage bitmapImage = new();
            await bitmapImage.SetSourceAsync(stream);

            ReferenceSize = Math.Max(bitmapImage.PixelWidth, 1);
            PreviewImage.Source = bitmapImage;

            UpdateCrosshair(0, 0);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load hotspot preview: {ex.Message}");
        }
    }

    private void ImageGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Capture so PointerMoved keeps firing even when the cursor leaves the grid,
        // making it possible to drag to a corner without losing the drag.
        ImageGrid.CapturePointer(e.Pointer);
        ApplyPointerPosition(e.GetCurrentPoint(ImageGrid).Position);
        e.Handled = true;
    }

    private void ImageGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(ImageGrid).Properties.IsLeftButtonPressed)
            return;
        ApplyPointerPosition(e.GetCurrentPoint(ImageGrid).Position);
        e.Handled = true;
    }

    private void ImageGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        ImageGrid.ReleasePointerCapture(e.Pointer);
    }

    private void ApplyPointerPosition(Point pos)
    {
        double w = ImageGrid.ActualWidth;
        double h = ImageGrid.ActualHeight;

        // Clamp display coords so the crosshair stays inside the grid while dragging outside.
        double clampedX = Math.Clamp(pos.X, 0, w);
        double clampedY = Math.Clamp(pos.Y, 0, h);

        HotspotX = Math.Clamp((int)(clampedX / w * ReferenceSize), 0, ReferenceSize - 1);
        HotspotY = Math.Clamp((int)(clampedY / h * ReferenceSize), 0, ReferenceSize - 1);
        UpdateCrosshair(clampedX, clampedY);
    }

    private void UpdateCrosshair(double displayX, double displayY)
    {
        CrosshairH.Y1 = displayY;
        CrosshairH.Y2 = displayY;
        CrosshairV.X1 = displayX;
        CrosshairV.X2 = displayX;
        Canvas.SetLeft(HotspotDot, displayX - 4);
        Canvas.SetTop(HotspotDot, displayY - 4);
        HotspotText.Text = $"X: {HotspotX},  Y: {HotspotY}";
    }
}
