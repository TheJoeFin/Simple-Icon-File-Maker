using Windows.Storage;
using Windows.Storage.Pickers;

namespace Simple_Icon_File_Maker.Helpers;

public static class FilePickerHelper
{
    public static async Task TrySetSuggestedFolderFromSourceImage(FileSavePicker savePicker, string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        try
        {
            // Use the source image file itself to suggest the folder
            // This makes the picker open in the source image's folder
            if (File.Exists(imagePath))
            {
                StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(imagePath);
                savePicker.SuggestedSaveFile = sourceFile;

                // SuggestedSaveFile overrides SuggestedFileName, so re-set
                // the name without the source extension to avoid names like "file.png.ico"
                savePicker.SuggestedFileName = Path.GetFileNameWithoutExtension(imagePath);
            }
        }
        catch
        {
            // If file access fails, fall back to default picker behavior
        }
    }
}
