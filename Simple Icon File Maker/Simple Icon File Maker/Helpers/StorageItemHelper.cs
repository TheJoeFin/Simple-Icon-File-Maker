using Simple_Icon_File_Maker.Constants;
using Windows.Storage;

namespace Simple_Icon_File_Maker.Helpers;

public static class StorageItemHelper
{
    public static async Task<string?> TryGetImagePathFromStorageItems(IReadOnlyList<IStorageItem> storageItems)
    {
      // Iterate through all the items to find an image, stop at first success
        foreach (IStorageItem item in storageItems)
        {
            if (item is StorageFile file && file.IsSupportedImageFormat())
            {
                return file.Path;
            }
        }
        return null;
    }

    public static List<string> GetFailedItemNames(IReadOnlyList<IStorageItem> storageItems)
    {
        List<string> failedItemNames = [];
      foreach (IStorageItem item in storageItems)
        {
            if (item is not StorageFile file || !file.IsSupportedImageFormat())
     {
                failedItemNames.Add($"File type not supported: {item.Name}");
            }
        }
        return failedItemNames;
    }
}
