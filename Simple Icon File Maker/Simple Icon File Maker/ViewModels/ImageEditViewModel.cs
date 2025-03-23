using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagick;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_Icon_File_Maker.Helpers;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Simple_Icon_File_Maker.ViewModels;

public partial class ImageEditViewModel : ObservableRecipient
{
    [ObservableProperty]
    private string fileName = "no file selected";

    [ObservableProperty]
    private string imagePath = string.Empty;

    [ObservableProperty]
    private ImageSource? mainSource;

    private SoftwareBitmap? _input;
    public SoftwareBitmap? Input => _input;

    private SoftwareBitmapSource? _inputSource;
    public SoftwareBitmapSource? InputSource => _inputSource;

    private readonly List<PointInt32> _selectionPoints = [];

    public List<PointInt32> SelectionPoints
    {
        get
        {
            return _selectionPoints;
        }
    }

    public ImageEditViewModel()
    {
        
    }

    partial void OnImagePathChanged(string? oldValue, string newValue)
    {
        MagickImage image = new(newValue);
        MainSource = image.ToImageSource();
        _ = Task.Run(async () =>
        {
            var softwareBitmap = await FilePathToSoftwareBitmapAsync(newValue);
                _input = softwareBitmap;
                _inputSource = await ToSourceAsync(softwareBitmap);
                OnPropertyChanged(nameof(InputSource));
        });
    }

    public static async Task<SoftwareBitmap> FilePathToSoftwareBitmapAsync(string filePath)
    {
        using IRandomAccessStream stream = await StorageFileExtensions.CreateStreamAsync(filePath);
        // Create the decoder from the stream
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
        // Get the SoftwareBitmap representation of the file
        return await decoder.GetSoftwareBitmapAsync();
    }

    public async Task<SoftwareBitmapSource> ToSourceAsync(SoftwareBitmap softwareBitmap)
    {
        var source = new SoftwareBitmapSource();

        if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
        {
            SoftwareBitmap convertedBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            await source.SetBitmapAsync(convertedBitmap);
        }
        else
        {
            await source.SetBitmapAsync(softwareBitmap);
        }

        return source;
    }
}
