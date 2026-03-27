using Simple_Icon_File_Maker.Constants;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Simple_Icon_File_Maker.Helpers;

public static class FilePickerHelper
{
    public static FileOpenPicker CreateDllPicker()
    {
        FileOpenPicker picker = new()
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
        };

        foreach (string ext in FileTypes.SupportedDllFormats)
            picker.FileTypeFilter.Add(ext);

        InitializeWithWindow.Initialize(picker, App.MainWindow.WindowHandle);

        return picker;
    }

    public static async Task<FileSavePicker> CreateSavePicker(string OutputPath, string ImagePath)
    {
        FileSavePicker savePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            
            DefaultFileExtension = ".ico",
            FileTypeChoices =
            {
                { "ICO File", [".ico"] }
            }
        };

        if (!string.IsNullOrWhiteSpace(OutputPath) && File.Exists(OutputPath))
        {
            try
            {
                StorageFile previousFile = await StorageFile.GetFileFromPathAsync(OutputPath);
                savePicker.SuggestedSaveFile = previousFile;
            }
            catch { }
        }

        savePicker.SuggestedFileName = Path.GetFileNameWithoutExtension(ImagePath) + ".ico";

        InitializeWithWindow.Initialize(savePicker, App.MainWindow.WindowHandle);

        return savePicker;
    }
}
