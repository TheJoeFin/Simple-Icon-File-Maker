namespace Simple_Icon_File_Maker.Helpers;

public static class StringHelper
{
    public static bool IsSupportedImageFormat(this string extension)
    {
        return Constants.FileTypes.SupportedImageFormats.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
