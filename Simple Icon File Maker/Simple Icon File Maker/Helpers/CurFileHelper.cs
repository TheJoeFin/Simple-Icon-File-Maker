namespace Simple_Icon_File_Maker.Helpers;

public static class CurFileHelper
{
    // Converts an ICO file to a CUR file by patching the binary header.
    // CUR format is identical to ICO except:
    //   - bytes [2:4]  → type = 2 (instead of 1)
    //   - per-entry bytes [+4:+6] → X hotspot (instead of color planes)
    //   - per-entry bytes [+6:+8] → Y hotspot (instead of bits-per-pixel)
    // hotspotX/Y are pixel coordinates relative to referenceSize (the image the user picked on).
    // For each entry the hotspot is scaled proportionally to that entry's actual pixel size.
    public static async Task ConvertIcoToCurAsync(string icoPath, string curPath,
        int hotspotX = 0, int hotspotY = 0, int referenceSize = -1)
    {
        byte[] data = await File.ReadAllBytesAsync(icoPath);

        if (data.Length < 6)
            return;

        // Change type from ICO (1) to CUR (2)
        data[2] = 2;
        data[3] = 0;

        int count = BitConverter.ToUInt16(data, 4);

        for (int i = 0; i < count; i++)
        {
            int entryOffset = 6 + (i * 16);
            if (entryOffset + 16 > data.Length)
                break;

            // ICONDIRENTRY byte 0 = width; 0 encodes 256 (max byte can't hold 256).
            int entrySize = data[entryOffset] == 0 ? 256 : data[entryOffset];

            int scaledX, scaledY;
            if (referenceSize > 1 && referenceSize != entrySize)
            {
                scaledX = (int)Math.Round((double)hotspotX * entrySize / referenceSize);
                scaledY = (int)Math.Round((double)hotspotY * entrySize / referenceSize);
            }
            else
            {
                scaledX = hotspotX;
                scaledY = hotspotY;
            }

            scaledX = Math.Clamp(scaledX, 0, entrySize - 1);
            scaledY = Math.Clamp(scaledY, 0, entrySize - 1);

            // Write little-endian WORD hotspot values
            data[entryOffset + 4] = (byte)(scaledX & 0xFF);
            data[entryOffset + 5] = (byte)(scaledX >> 8);
            data[entryOffset + 6] = (byte)(scaledY & 0xFF);
            data[entryOffset + 7] = (byte)(scaledY >> 8);
        }

        await File.WriteAllBytesAsync(curPath, data);
    }
}
