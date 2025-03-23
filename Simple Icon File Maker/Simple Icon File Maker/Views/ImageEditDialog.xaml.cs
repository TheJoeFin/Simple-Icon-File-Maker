using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Simple_Icon_File_Maker.ViewModels;
using Windows.Graphics;

namespace Simple_Icon_File_Maker.Views;

public sealed partial class ImageEditDialog : ContentDialog
{
    public ImageEditViewModel ViewModel;

    public ImageEditDialog(string imagePath)
    {
        InitializeComponent();
        ViewModel = App.GetService<ImageEditViewModel>();
        ViewModel.ImagePath = imagePath;
    }

    private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.Input is null)
            return;

        var currentPoint = e.GetCurrentPoint(InputImageCanvas);
        var ratioX = InputImage.ActualWidth / ViewModel.Input.PixelWidth;
        var ratioY = InputImage.ActualHeight / ViewModel.Input.PixelHeight;
        // Get the offset between the canvas and the image
        var offSetX = InputImageCanvas.ActualWidth > InputImage.ActualWidth ? (InputImageCanvas.ActualWidth - InputImage.ActualWidth) / 2 : 0;
        var offSetY = InputImageCanvas.ActualHeight > InputImage.ActualHeight ? (InputImageCanvas.ActualHeight - InputImage.ActualHeight) / 2 : 0;
        var x = (uint)((currentPoint.Position.X - offSetX) / ratioX);
        var y = (uint)((currentPoint.Position.Y - offSetY) / ratioY);

        ViewModel.SelectionPoints.Add(new PointInt32((int)x, (int)y));
        var ellipse = new Ellipse() { Width = 8, Height = 8, Stroke = new SolidColorBrush(Colors.Red), Fill = new SolidColorBrush(Colors.Red) };
        Canvas.SetLeft(ellipse, currentPoint.Position.X - 4);
        Canvas.SetTop(ellipse, currentPoint.Position.Y - 4);
        InputImageCanvas.Children.Add(ellipse);
    }
}
