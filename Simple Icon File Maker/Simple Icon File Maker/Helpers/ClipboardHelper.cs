using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Simple_Icon_File_Maker.Helpers;

public static class ClipboardHelper
{
    public static async Task<string?> TryGetImageFromClipboardAsync()
    {
      try
        {
       DataPackageView dataPackageView = Clipboard.GetContent();

     if (dataPackageView == null)
      return null;

      // Try to get bitmap data from clipboard
     if (dataPackageView.Contains(StandardDataFormats.Bitmap))
            {
      Debug.WriteLine("Clipboard contains Bitmap");
             
     IRandomAccessStreamReference bitmapStreamRef = await dataPackageView.GetBitmapAsync();
  
          if (bitmapStreamRef != null)
              {
            using IRandomAccessStreamWithContentType bitmapStream = await bitmapStreamRef.OpenReadAsync();
          
                    // Save the bitmap to a temporary file
           StorageFolder tempFolder = ApplicationData.Current.LocalCacheFolder;
         string tempFileName = $"clipboard_paste_{DateTime.Now:yyyyMMddHHmmss}.png";
                  StorageFile tempFile = await tempFolder.CreateFileAsync(tempFileName, CreationCollisionOption.ReplaceExisting);
      
          using (IRandomAccessStream fileStream = await tempFile.OpenAsync(FileAccessMode.ReadWrite))
        {
              await RandomAccessStream.CopyAsync(bitmapStream, fileStream);
       await fileStream.FlushAsync();
         }

         return tempFile.Path;
         }
            }
            // Try to get storage items (files) from clipboard
            else if (dataPackageView.Contains(StandardDataFormats.StorageItems))
      {
   Debug.WriteLine("Clipboard contains StorageItems");
    IReadOnlyList<IStorageItem> storageItems = await dataPackageView.GetStorageItemsAsync();
              
     if (storageItems != null && storageItems.Count > 0)
       {
  return await StorageItemHelper.TryGetImagePathFromStorageItems(storageItems);
        }
         }

return null;
    }
        catch (Exception ex)
        {
   Debug.WriteLine($"Error pasting from clipboard: {ex.Message}");
      return null;
        }
    }
}
