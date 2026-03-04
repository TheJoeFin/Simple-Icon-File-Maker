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
                string name = Path.GetFileNameWithoutExtension(imagePath);
                await sourceFile.RenameAsync(name);
                savePicker.SuggestedSaveFile = sourceFile;
            }
        }
        catch
        {
            // If file access fails, fall back to default picker behavior
        }
    }
}