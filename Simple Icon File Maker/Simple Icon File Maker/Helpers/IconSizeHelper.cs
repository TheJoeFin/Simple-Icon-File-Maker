using Simple_Icon_File_Maker.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace Simple_Icon_File_Maker.Helpers;

public static class IconSizeHelper
{
    private const string iconFileName = "IconSizes.json";
    private const string settingsName = "IconSizes";

    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
    internal static async Task Save(IEnumerable<IconSize> iconSizes)
    {
        StorageFolder local = ApplicationData.Current.LocalFolder;
        StorageFile file = await local.CreateFileAsync(iconFileName, CreationCollisionOption.ReplaceExisting);

        string json = JsonSerializer.Serialize(iconSizes);

        try
        {
            await FileIO.WriteTextAsync(file, json);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public static async Task<List<IconSize>> GetIconSizes()
    {
        try
        {
            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
            StorageFolder local = ApplicationData.Current.LocalFolder;
            StorageFile file = await local.GetFileAsync(iconFileName);
            string json = await FileIO.ReadTextAsync(file);
            List<IconSize>? list = JsonSerializer.Deserialize<List<IconSize>>(json);

            return list ?? IconSize.GetAllSizes().ToList();

        }
        catch (FileNotFoundException)
        {
            return IconSize.GetAllSizes().ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return IconSize.GetAllSizes().ToList();
        }
    }
}
