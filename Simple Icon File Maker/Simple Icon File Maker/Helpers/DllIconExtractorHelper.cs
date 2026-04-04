using ImageMagick;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Simple_Icon_File_Maker.Helpers;

[SupportedOSPlatform("windows")]
public static class DllIconExtractorHelper
{
    private const int RT_ICON = 3;
    private const int RT_GROUP_ICON = 14;
    private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint LoadLibraryEx(string lpFileName, nint hFile, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(nint hModule);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindResource(nint hModule, nint lpName, nint lpType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint LoadResource(nint hModule, nint hResInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint LockResource(nint hResData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SizeofResource(nint hModule, nint hResInfo);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool EnumResourceNames(nint hModule, nint lpszType, EnumResNameProc lpEnumFunc, nint lParam);

    private delegate bool EnumResNameProc(nint hModule, nint lpszType, nint lpszName, nint lParam);

    public static Task<IReadOnlyList<string>> ExtractIconsToFolderAsync(string dllPath, string outputFolder)
    {
        return Task.Run<IReadOnlyList<string>>(() =>
        {
            List<string> savedPaths = [];
            Directory.CreateDirectory(outputFolder);

            nint hModule = LoadLibraryEx(dllPath, 0, LOAD_LIBRARY_AS_DATAFILE);
            if (hModule == 0)
                return savedPaths;

            try
            {
                ExtractGroupIcons(hModule, outputFolder, savedPaths);
            }
            finally
            {
                FreeLibrary(hModule);
            }

            return savedPaths;
        });
    }

    private static void ExtractGroupIcons(nint hModule, string outputFolder, List<string> savedPaths)
    {
        EnumResourceNames(hModule, RT_GROUP_ICON, (hMod, _, lpszName, _) =>
        {
            nint hResInfo = FindResource(hMod, lpszName, RT_GROUP_ICON);
            if (hResInfo == 0)
                return true;

            byte[]? groupData = ReadResourceBytes(hMod, hResInfo);
            if (groupData is null || groupData.Length < 6)
                return true;

            // GRPICONDIR: reserved(2) + type(2) + count(2)
            int count = BitConverter.ToInt16(groupData, 4);
            string groupName = GetResourceName(lpszName);

            // Pick the entry with the largest pixel dimensions — that is the best source
            // image for the user to scale down from. All entries in the same group are
            // different-resolution versions of the same icon; we only need one source.
            int bestDim = -1;
            ushort bestNId = 0;
            int bestDisplayWidth = 0;
            int bestDisplayHeight = 0;

            for (int j = 0; j < count; j++)
            {
                // GRPICONDIRENTRY starts at offset 6, each entry is 14 bytes
                int entryOffset = 6 + j * 14;
                if (entryOffset + 14 > groupData.Length)
                    break;

                byte width = groupData[entryOffset];
                byte height = groupData[entryOffset + 1];
                int displayWidth = width == 0 ? 256 : width;
                int displayHeight = height == 0 ? 256 : height;
                int dim = displayWidth * displayHeight;

                if (dim > bestDim)
                {
                    bestDim = dim;
                    // nId is at offset +12 within the entry (2 bytes)
                    bestNId = BitConverter.ToUInt16(groupData, entryOffset + 12);
                    bestDisplayWidth = displayWidth;
                    bestDisplayHeight = displayHeight;
                }
            }

            if (bestDim < 0)
                return true;

            nint hIconRes = FindResource(hMod, bestNId, RT_ICON);
            if (hIconRes == 0)
                return true;

            byte[]? iconData = ReadResourceBytes(hMod, hIconRes);
            if (iconData is null)
                return true;

            string outputPath = Path.Combine(outputFolder, $"{groupName}.png");

            try
            {
                SaveIconAsPng(iconData, bestDisplayWidth, bestDisplayHeight, outputPath);
                savedPaths.Add(outputPath);
            }
            catch
            {
                // Skip icons that fail to convert
            }

            return true;
        }, 0);
    }

    private static string GetResourceName(nint lpszName)
    {
        // IS_INTRESOURCE: pointer value <= 0xFFFF
        if ((ulong)lpszName <= 0xFFFF)
            return lpszName.ToString();

        return Marshal.PtrToStringUni(lpszName) ?? lpszName.ToString();
    }

    private static byte[]? ReadResourceBytes(nint hModule, nint hResInfo)
    {
        uint size = SizeofResource(hModule, hResInfo);
        if (size == 0)
            return null;

        nint hResData = LoadResource(hModule, hResInfo);
        if (hResData == 0)
            return null;

        nint pData = LockResource(hResData);
        if (pData == 0)
            return null;

        byte[] data = new byte[size];
        Marshal.Copy(pData, data, 0, (int)size);
        return data;
    }

    private static void SaveIconAsPng(byte[] iconData, int width, int height, string outputPath)
    {
        // Windows Vista+ stores 256x256 RT_ICON entries as raw PNG data
        if (iconData.Length >= 8 && iconData.AsSpan(0, 8).SequenceEqual(PngSignature))
        {
            File.WriteAllBytes(outputPath, iconData);
            return;
        }

        // DIB data — wrap in a minimal ICO container so ImageMagick can decode it
        byte[] icoBytes = BuildIcoFile(iconData, width, height);
        MagickReadSettings settings = new() { Format = MagickFormat.Ico };
        using MagickImage image = new(icoBytes, settings);
        image.Write(outputPath, MagickFormat.Png);
    }

    private static byte[] BuildIcoFile(byte[] iconData, int width, int height)
    {
        // ICO header: 6 bytes
        // ICONDIRENTRY: 16 bytes
        // Total header: 22 bytes, image data follows
        const int headerSize = 22;
        byte[] ico = new byte[headerSize + iconData.Length];
        using MemoryStream ms = new(ico);
        using BinaryWriter w = new(ms);

        // ICONDIR
        w.Write((ushort)0);  // reserved
        w.Write((ushort)1);  // type = 1 (icon)
        w.Write((ushort)1);  // count = 1

        // ICONDIRENTRY
        w.Write((byte)(width >= 256 ? 0 : width));   // width (0 = 256)
        w.Write((byte)(height >= 256 ? 0 : height)); // height (0 = 256)
        w.Write((byte)0);    // color count
        w.Write((byte)0);    // reserved
        w.Write((ushort)1);  // planes
        w.Write((ushort)32); // bit count
        w.Write((uint)iconData.Length);  // size of image data
        w.Write((uint)headerSize);       // offset of image data

        // Image data
        w.Write(iconData);

        return ico;
    }
}
