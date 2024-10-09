using Windows.Storage;

namespace Simple_Icon_File_Maker.Constants;

public static class FileTypes
{
    public static readonly HashSet<string> SupportedImageFormats = [".png", ".bmp", ".jpeg", ".jpg", ".ico"];

    public static bool IsSupportedImageFormat(this StorageFile file)
    {
        return SupportedImageFormats.Contains(file.FileType, StringComparer.OrdinalIgnoreCase);
    }

}
