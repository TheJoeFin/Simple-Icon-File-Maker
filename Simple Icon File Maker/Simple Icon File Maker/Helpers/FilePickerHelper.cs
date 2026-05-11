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

    public static async Task<FileSavePicker> CreateSavePicker(string OutputPath, string ImagePath, string? forceExtension = null)
    {
        bool isCurSource = Path.GetExtension(ImagePath).Equals(".cur", StringComparison.OrdinalIgnoreCase);
        string defaultExtension = forceExtension ?? (isCurSource ? ".cur" : ".ico");

        FileSavePicker savePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,

            DefaultFileExtension = defaultExtension,
            FileTypeChoices =
            {
                { "ICO File", [".ico"] },
                { "CUR File", [".cur"] }
            }
        };

        if (!string.IsNullOrWhiteSpace(OutputPath) && File.Exists(OutputPath))
        {
            // Only restore the previous file when its extension matches what we want to save as.
            // If they differ (e.g. previous save was .ico but we're now saving as .cur) skip
            // SuggestedSaveFile so that DefaultFileExtension controls the active filter.
            bool extensionMatches = Path.GetExtension(OutputPath)
                .Equals(defaultExtension, StringComparison.OrdinalIgnoreCase);

            if (extensionMatches)
            {
                try
                {
                    StorageFile previousFile = await StorageFile.GetFileFromPathAsync(OutputPath);
                    savePicker.SuggestedSaveFile = previousFile;
                }
                catch { }
            }
        }

        savePicker.SuggestedFileName = Path.GetFileNameWithoutExtension(ImagePath) + defaultExtension;

        InitializeWithWindow.Initialize(savePicker, App.MainWindow.WindowHandle);

        return savePicker;
    }
}
