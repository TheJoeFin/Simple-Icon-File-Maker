using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace IconConverter.IconEx
{
    internal struct IconHeader
    {
        public IconHeader(BinaryReader reader)
            : this()
        {
            Reserved = reader.ReadInt16();
            Type = reader.ReadInt16();
            Count = reader.ReadInt16();
        }

        public short Reserved { get; set; }

        public short Type { get; set; }

        public short Count { get; set; }

        public void Save(BinaryWriter writer)
        {
            writer.Write(Reserved);
            writer.Write(Type);
            writer.Write(Count);

            Console.WriteLine(string.Format("{0} {1} {2} IconFileWriter", Reserved, Type, Count));

        }
    }

    public struct IconEntry
    {
        /// <summary>This constructor should be called by an IconFileReader.
        /// The IconFileWriter uses a constructor that passes in all values
        /// explicitly.</summary>
        public IconEntry(BinaryReader reader)
            : this()
        {
            Width = reader.ReadByte();
            Height = reader.ReadByte();
            ColorCount = reader.ReadByte();
            Reserved = reader.ReadByte();
            Planes = reader.ReadInt16();
            BitCount = reader.ReadInt16();
            BytesInRes = reader.ReadInt32();
            ImageOffset = reader.ReadInt32();
        }

        public byte Width { get; set; }

        public byte Height { get; set; }

        public byte ColorCount { get; set; }

        public byte Reserved { get; set; }

        public short Planes { get; set; }

        public short BitCount { get; set; }

        public int BytesInRes { get; set; }

        public int ImageOffset { get; set; }

        public void Save(BinaryWriter writer)
        {
            writer.Write(Width);
            writer.Write(Height);
            writer.Write(ColorCount);
            writer.Write(Reserved);
            writer.Write(Planes);
            writer.Write(BitCount);
            writer.Write(BytesInRes);
            writer.Write(ImageOffset);
        }
        public override string ToString()
        {
            return string.Format(
                "Size: ({0},{1}), ColorCount: {2}, Reserverd: {7}, Planes: {3}, BitCount {4}, BytesInRes: {5}, ImageOffset {6}",
                this.Width, this.Height, this.ColorCount,
                this.Planes, this.BitCount,
                this.BytesInRes, this.ImageOffset, this.Reserved);
        }
    }
    public class IconFileReader
    {
        #region Fields
 
        private IEnumerable<IIconData> images;
 
        private string filename;
 
        #endregion Fields
 
        #region Constructors
 
        public IconFileReader(string filename)
        {
            Filename = filename;
        }
 
        public IconFileReader() { }
 
        #endregion Constructors
 
        #region Properties
 
        public IEnumerable<IIconData> Images
        {
            get { return images; }
        }
 
        public string Filename
        {
            get { return filename; }
            set
            {
                filename = value;
                ReadFile(filename);
            }
        }
 
        #endregion Properties
 
        #region Methods
 
        #region Public Methods
 
        public Icon FirstIcon
        {
            get { return images != null ? images.FirstOrDefault().Icon : null; }
        }
 
        #endregion Public Methods
 
        #region Private Methods
 
        private static IconData BuildIconData(Stream source, IconHeader header, IconEntry entry)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                {
                    /* I’m not recreating an icon file here, just one icon at a time in the icon file format.
                     * Therefore write the count as 1, and the offset as sizeof(header) + sizeof(entry). */
                    const short number = 1;
                    var offset = Marshal.SizeOf(typeof(IconHeader)) + Marshal.SizeOf(typeof(IconEntry));
 
                    writer.Write(header.Reserved);
                    writer.Write(header.Type);
                    writer.Write(number);
                    writer.Write((byte)entry.Width);
                    writer.Write((byte)entry.Height);
                    writer.Write(entry.ColorCount);
                    writer.Write(entry.Reserved);
                    writer.Write(entry.Planes);
                    writer.Write(entry.BitCount);
                    writer.Write(entry.BytesInRes);
                    writer.Write(offset);
 
                    var buffer = new byte[entry.BytesInRes];
                    source.Position = entry.ImageOffset;
                    source.Read(buffer, 0, entry.BytesInRes);
                    writer.Write(buffer);
 
                    /* While this shouldn’t always be necessary, the managed Icon type will throw an exception when, for example,
                     * trying to create an icon from any .png image that was just loaded from the file, whether it is 256×256
                     * (0x0 in the entry), or smaller, whereas this way always seems to work. */
                    using (var image = Image.FromStream(stream) as Bitmap)
                    {
                        Icon temp = null;
                        try
                        {
                            temp = Icon.FromHandle(image.GetHicon());
 
                            /* Use the dimensions we got from the GDI icon, so we
                             * don’t have to worry about double-height bitmaps etc.*/
                            return new IconData { Icon = new Icon(temp, temp.Width, temp.Height), BitDepth = entry.BitCount };
                        }
                        finally
                        {
                            if (temp != null)
                                NativeMethods.DestroyIcon(temp.Handle);
                        }
                    }
                }
            }
        }
 
        private static IEnumerable<IIconData> ReadStream(Stream stream)
        {
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
            {
                var header = new IconHeader(reader);
 
                var entries = new IconEntry[header.Count];
 
                for (var i = 0; i < header.Count; i++)
                {
                    entries[i] = new IconEntry(reader);
                }
 
                var imageData = new IconData[header.Count];
 
                for (var i = 0; i < header.Count; i++)
                {
                    try
                    {
                        imageData[i] = BuildIconData(stream, header, entries[i]);
                    }
                    catch { }
                }
 
                /* Sort the results; by bit-depth, then size; descending, so that the
                 * first icon found will be the largest resolution and highest bit depth. */
                var bitDepths = imageData.Select(b => b.BitDepth).Distinct().ToList();
                bitDepths.Sort();
 
                var result = new List<IIconData>();
 
                foreach (var i in bitDepths)
                {
                    result.AddRange(imageData.Where(b => b.BitDepth == i).OrderBy(b => b.Icon.Width));
                }
 
                result.Reverse();
 
                return result;
            }
        }
 
        private void ReadFile(string filename)
        {
            try
            {
                using (var stream = new MemoryStream(File.ReadAllBytes(filename)))
                {
                    stream.Position = 0;
                    images = ReadStream(stream);
                }
            }
            catch (FileNotFoundException) { }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            catch (Exception) { }
        }
 
        #endregion Private Methods
 
        #endregion Methods
 
        #region Nested Classes
 
        private static class NativeMethods
        {
            [DllImportAttribute("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern bool DestroyIcon(IntPtr hIcon);
        }
 
        #endregion Nested Classes
    }
 
    /// <summary>The <see cref=”Romy.Core.IconFileReader.Images”/> property contains a collection of this type.</summary>
    /// <remarks>When the icons are converted to managed Icon instances, the original bit depth is lost. This type is used
    /// to store the original bit depth, for display.</remarks>
    public class IconData : IIconData
    {
        #region Properties
 
        public Icon Icon { get; set; }
 
        public int BitDepth { get; set; }
 
        #endregion Properties
    }
    public interface IIconData
    {
        int BitDepth { get; set; }

        System.Drawing.Icon Icon { get; set; }
    }

    public class IconFileWriter
    {
        #region Fields

        List<Image> images = new List<Image>();

        #endregion Fields

        #region Properties

        public IList<Image> Images
        {
            get { return images; }
        }

        #endregion Properties

        #region Methods

        #region Public Methods

        public void Save(string path)
        {
            using (var stream = new MemoryStream())
            {
                SaveToStream(stream);
                stream.Position = 0;

                using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, (int)stream.Length, false))
                {
                    stream.WriteTo(fileStream);
                }
            }
        }

        public async Task SaveAsync(string path)
        {
            using (var stream = new MemoryStream())
            {
                SaveToStream(stream);
                stream.Position = 0;

                using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }
        }

        #endregion Public Methods

        #region Private Methods

        private void SaveToStream(Stream stream)
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                // Remove any duplicated resolutions. Done first because this may change the image count.
              //  ValidateImages();

                var imageData = new Dictionary<IconEntry, byte[]>();

                // Write header
                new IconHeader
                {
                    Reserved = 0,
                    Type = 1,
                    Count = Convert.ToInt16(images.Count)
                }.Save(writer);

                // The offset of the first icon.
                var offset = Marshal.SizeOf(typeof(IconHeader)) + images.Count * Marshal.SizeOf(typeof(IconEntry));
               

                // Write all the icon entries
                for (var i = 0; i < images.Count; i++)
                {
                    ImageConverter a = new ImageConverter();
                    Bitmap image = images[i] as Bitmap;
                    byte[] data = (byte[])a.ConvertTo(image, typeof(byte[]));

                //    var lol = new ImageMagick.MagickReadSettings() {Format=ImageMagick.MagickFormat.Png };
               //     ImageMagick.MagickImage mi = new ImageMagick.MagickImage(data1, lol);
                //    byte[] data = mi.ToByteArray();

                    var entry = new IconEntry
                    {
                        Width = image.Width < 256 ? Convert.ToByte(image.Width) : (byte)0,
                        Height = image.Height < 256 ? Convert.ToByte(image.Height) : (byte)0,
                        ColorCount = 0,
                        Reserved = 0,
                        Planes = 1,
                        BitCount = 32,
                        BytesInRes = data.Length,
                        ImageOffset = offset
                    };

                    imageData[entry] = data;
                    Console.WriteLine(entry);
                    entry.Save(writer);

                    offset += data.Length;
                }

                // Write the Icons.
                foreach (var kvp in imageData)
                {
                    writer.Seek(kvp.Key.ImageOffset, SeekOrigin.Begin);
                    writer.Write(kvp.Value);
                }
            }
        }
        public static byte[] IconToBytes(Icon icon)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                icon.Save(ms);
                return ms.ToArray();
            }
        }

        public static Bitmap ResizeImage(Bitmap imgToResize, System.Drawing.Size size)
        {
            Bitmap b = new Bitmap(size.Width, size.Height);

            using (Graphics g = Graphics.FromImage((System.Drawing.Image)b))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Default;
                g.DrawImage(imgToResize, 0, 0, size.Width, size.Height);
            }
            return b;
        }

        private void ValidateImages()
        {
            var contained = new List<int>();

            var validatedImages = new List<Image>();

            // Make sure there are not multiple images of the same resolution
            for (var i = 0; i < images.Count; i++)
            {
                var image = images[i] as Bitmap;

                /* Images larger than 256×256 will create invalid
                 * icons, so resize any image that’s too large. */
                if (image.Width > 256 || image.Height > 256)
                {
                    image = ResizeImage(image, new Size(256, 256));
                }

                if (!contained.Contains(image.Width))
                {
                    contained.Add(image.Width);
                    validatedImages.Add(image);
                }
            }

            images.Clear();
            images.AddRange(validatedImages);
        }

        #endregion Private Methods

        #endregion Methods
    }
}
