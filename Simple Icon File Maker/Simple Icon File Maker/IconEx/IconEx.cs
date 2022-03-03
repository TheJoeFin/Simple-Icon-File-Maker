using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IconConverter.IconEx
{
    #region IconExException
    /// <summary>
    /// Exception thrown for unreadable icons
    /// </summary>
    public class IconExException : Exception
    {
        public IconExException()
            : base()
        {
        }
        public IconExException(string message)
            : base(message)
        {
        }
        public IconExException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
    #endregion

    #region IconEx
    /// <summary>
    /// Manages a true Windows Icon with multiple images
    /// (colour depths and sizes)
    /// </summary>
    public class IconEx : IDisposable
    {
        #region Unmanaged Code
        [DllImport("kernel32", CharSet = CharSet.Auto)]
        private extern static IntPtr LoadLibraryEx(
            [MarshalAs(UnmanagedType.LPTStr)]
			string lpLibFileName,
            IntPtr hFile,
            int dwFlags);
        [DllImport("kernel32")]
        private extern static int FreeLibrary(
            IntPtr hLibModule);
        [DllImport("kernel32")]
        private extern static IntPtr LoadResource(
            IntPtr hInstance,
            IntPtr hResInfo);
        [DllImport("kernel32")]
        private extern static IntPtr LockResource(
            IntPtr hResData);
        [DllImport("kernel32", CharSet = CharSet.Auto)]
        private extern static IntPtr FindResource(
            IntPtr hInstance,
            [MarshalAs(UnmanagedType.LPTStr)]
			string lpName,
            IntPtr lpType);
        [DllImport("kernel32")]
        private extern static int SizeofResource(
            IntPtr hInstance,
            IntPtr hResInfo);
        [DllImport("kernel32")]
        private extern static int FreeResource(
            IntPtr hResData);
        #endregion

        #region Constants
        private const Int16 IMAGE_ICON = 1;
        private const int LOAD_LIBRARY_AS_DATAFILE = 0x2;
        private const int RT_CURSOR = 1;
        private const int RT_BITMAP = 2;
        private const int RT_ICON = 3;
        private const int DIFFERENCE = 11;
        private const int RT_GROUP_CURSOR = RT_CURSOR + DIFFERENCE;
        private const int RT_GROUP_ICON = RT_ICON + DIFFERENCE;
        #endregion

        #region Structures
        public struct ICONDIRENTRY
        {
            public byte width; // Width of the image
            public byte height; // Height of the image (times 2)
            public byte colorCount; // Number of colors in image (0 if more than 8bpp)
            public byte reserved; // Reserved
            public Int16 wPlanes; // Color Planes
            public Int16 wBitCount; // Bits per pixel
            public int dwBytesInRes; // how many bytes in this resource?
            public int dwImageOffset;// where in the file is this image			

            public ICONDIRENTRY(
                BinaryReader br)
            {
                this.width = br.ReadByte();
                this.height = br.ReadByte();
                this.colorCount = br.ReadByte();
                this.reserved = br.ReadByte();
                this.wPlanes = br.ReadInt16();
                this.wBitCount = br.ReadInt16();
                this.dwBytesInRes = br.ReadInt32();
                this.dwImageOffset = br.ReadInt32();
            }

            public void Write(
                BinaryWriter br)
            {
                br.Write(this.width);
                br.Write(this.height);
                br.Write(this.colorCount);
                br.Write(this.reserved);
                br.Write(this.wPlanes);
                br.Write(this.wBitCount);
                br.Write(this.dwBytesInRes);
                br.Write(this.dwImageOffset);
            }

            public override string ToString()
            {
                return string.Format(
                    "Size: ({0},{1}), ColorCount: {2}, Reserverd: {7}, Planes: {3}, BitCount {4}, BytesInRes: {5}, ImageOffset {6}",
                    this.width, this.height, this.colorCount,
                    this.wPlanes, this.wBitCount,
                    this.dwBytesInRes, this.dwImageOffset, this.reserved);
            }
        }

        private struct MEMICONDIRENTRY
        {
            public byte width; // Width of the image
            public byte height; // Height of the image (times 2)
            public byte colorCount; // Number of colors in image (0 if more than 8bpp)
            public byte reserved; // Reserved
            public Int16 wPlanes; // Color Planes
            public Int16 wBitCount; // Bits per pixel
            public int dwBytesInRes; // how many bytes in this resource?
            public Int16 nID;// resource id of the image

            public MEMICONDIRENTRY(
                IntPtr lPtr,
                int ofs)
            {
                this.width = Marshal.ReadByte(lPtr, ofs);
                this.height = Marshal.ReadByte(lPtr, ofs + 1);
                this.colorCount = Marshal.ReadByte(lPtr, ofs + 2);
                this.reserved = Marshal.ReadByte(lPtr, ofs + 3);
                this.wPlanes = Marshal.ReadInt16(lPtr, ofs + 4);
                this.wBitCount = Marshal.ReadInt16(lPtr, ofs + 6);
                this.dwBytesInRes = Marshal.ReadInt32(lPtr, ofs + 8);
                this.nID = Marshal.ReadInt16(lPtr, ofs + 12);
            }

            public override string ToString()
            {
                return string.Format(
                    "Size: ({0},{1}), ColorCount: {2}, Planes: {3}, BitCount {4}, BytesInRes: {5}, IconResourceID {6}",
                    this.width, this.height, this.colorCount,
                    this.wPlanes, this.wBitCount,
                    this.dwBytesInRes, this.nID);
            }
        }

        #endregion

        #region Member Variables
        public IconDeviceImageCollection iconCollection = new IconDeviceImageCollection();
        private string iconFile = null;
        private string libraryFile = null;
        private int resourceId = -1;
        private string resourceName = null;
        #endregion

        #region Properties
        /// <summary>
        /// Returns the collection of device images
        /// within this icon 
        /// </summary>
        public IconDeviceImageCollection Items
        {
            get
            {
                return this.iconCollection;
            }
            set
            {

            }
        }

        /// <summary>
        /// Gets the file the icon was loaded from
        /// </summary>
        public string IconFile
        {
            get
            {
                return this.iconFile;
            }
        }

        /// <summary>
        /// Gets the library this icon was loaded from
        /// or a blank string if the icon was not sourced
        /// from a library.
        /// </summary>
        public string LibraryFile
        {
            get
            {
                return this.libraryFile;
            }
        }

        /// <summary>
        /// Gets the integer resource id of this icon if it
        /// was loaded from a library
        /// </summary>
        public int ResourceId
        {
            get
            {
                return this.resourceId;
            }
        }

        /// <summary>
        /// Gets the string resource id of this icon if it
        /// was loaded from a library
        /// </summary>
        public string ResourceName
        {
            get
            {
                return this.resourceName;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Loads an icon from the specified file
        /// </summary>
        /// <param name="iconFile">File containing icon</param>
        public void FromFile(
            string iconFile)
        {
            loadFromFile(iconFile);
        }

        /// <summary>
        /// Loads an icon from an executable or library
        /// with the specified integer resource id
        /// </summary>
        /// <param name="libraryFile">Executable or DLL 
        /// containing icon</param>
        /// <param name="resourceId">Integer resource identifier</param>
        public void FromLibrary(
            string libraryFile,
            int resourceId)
        {
            loadInitialise();
            string resourceName = String.Format("#{0:N0}", resourceId);
            loadFromLibrary(libraryFile, resourceName);
        }

        /// <summary>
        /// Loads an icon from an executable or library
        /// with the specified string resource id
        /// </summary>
        /// <param name="libraryFile">Executable or DLL 
        /// containing icon</param>
        /// <param name="resourceName">String resource identifier</param>
        public void FromLibrary(
            string libraryFile,
            string resourceName
            )
        {
            loadInitialise();
            loadFromLibrary(libraryFile, resourceName);
        }

        /// <summary>
        /// Saves the icon to the specified file
        /// </summary>
        /// <param name="iconFile">File to save to</param>
        public void Save(
            string iconFile
            )
        {
            // open the file for writing, truncate if exists
            FileStream fs = new FileStream(
                iconFile,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read);
            BinaryWriter bw = null;
            try
            {
                bw = new BinaryWriter(fs);

                // write out the icon header:
                writeIconFileHeader(bw);

                // write out the icon directory entries:
                int iconOffset = 6 + 16 * this.iconCollection.Count;
                foreach (IconDeviceImage idi in this.iconCollection)
                {
                    int bytesInRes = idi.IconImageDataBytes();

                    ICONDIRENTRY ide = new ICONDIRENTRY();
                    ide.width = (byte)idi.IconSize.Width;
                    ide.height = (byte)idi.IconSize.Height;
                    switch (idi.ColorDepth)
                    {
                        case ColorDepth.Depth4Bit:
                            ide.colorCount = 16;
                            ide.wBitCount = 4;
                            break;
                        case ColorDepth.Depth8Bit:
                            ide.colorCount = 255;
                            ide.wBitCount = 8;
                            break;
                        case ColorDepth.Depth16Bit:
                            ide.colorCount = 0;
                            ide.wBitCount = 16;
                            break;
                        case ColorDepth.Depth24Bit:
                            ide.colorCount = 0;
                            ide.wBitCount = 24;
                            break;
                        case ColorDepth.Depth32Bit:
                            ide.colorCount = 0;
                            ide.wBitCount = 32;
                            break;
                    }
                    ide.wPlanes = 1;
                    ide.dwBytesInRes = bytesInRes;
                    ide.dwImageOffset = iconOffset;
                    Console.WriteLine(ide);
                    ide.Write(bw);

                    iconOffset += bytesInRes;
                }
               
           //   Console.WriteLine(bw.BaseStream.Length);

                // write out the icon data:
                foreach (IconDeviceImage idi in this.iconCollection)
                {
                    
                    idi.SaveIconBitmapData(bw);
                }
            }
            catch (Exception ex)
            {
                if (ex is SystemException)
                {
                    throw ex;
                }
                else
                {
                    throw new IconExException(ex.Message, ex);
                }
            }
            finally
            {
                if (bw != null)
                {
                    bw.Close();
                }
            }

        }
        #endregion

        #region Private Implementation
        private void loadInitialise()
        {
            this.iconFile = "";
            this.resourceId = -1;
            this.libraryFile = "";
            this.iconCollection = new IconDeviceImageCollection();
        }

        // this method is too long, I'm sorry...
        private void loadFromLibrary(
            string libraryFile,
            string resourceName
            )
        {
            string msg = "";
            bool failed = false;
            IntPtr hGlobal = IntPtr.Zero;
            IntPtr hRsrc = IntPtr.Zero;
            IntPtr hLibrary = IntPtr.Zero;
            IntPtr lPtr = IntPtr.Zero;

            try
            {
                hLibrary = LoadLibraryEx(
                    libraryFile,
                    IntPtr.Zero,
                    LOAD_LIBRARY_AS_DATAFILE);
                if (hLibrary != IntPtr.Zero)
                {
                    hRsrc = FindResource(
                        hLibrary,
                        resourceName,
                        (IntPtr)RT_GROUP_ICON);
                    if (hRsrc != IntPtr.Zero)
                    {
                        hGlobal = LoadResource(hLibrary, hRsrc);
                        if (hGlobal != IntPtr.Zero)
                        {
                            lPtr = LockResource(hGlobal);
                            if (lPtr != IntPtr.Zero)
                            {
                                // now we can read the header:
                                int iconCount = readResourceIconFileHeader(lPtr);
                                // read the directory:
                                MEMICONDIRENTRY[] ide = new MEMICONDIRENTRY[iconCount];
                                int ofs = 6;
                                for (int iconEntry = 0; iconEntry < iconCount; iconEntry++)
                                {
                                    ide[iconEntry] = new MEMICONDIRENTRY(lPtr, ofs);
                                    //Console.WriteLine(ide[iconEntry].ToString());
                                    ofs += 14;
                                }
                                FreeResource(hGlobal);
                                hGlobal = IntPtr.Zero;

                                // we have the directory, so now can load the icons:
                                IconDeviceImage[] icons = new IconDeviceImage[iconCount];
                                // read the icons:
                                for (int iconEntry = 0; iconEntry < iconCount; iconEntry++)
                                {
                                    // find the specified icon:
                                    string resName = String.Format("#{0:N0}", ide[iconEntry].nID);
                                    hRsrc = FindResource(
                                        hLibrary,
                                        resName,
                                        (IntPtr)RT_ICON);
                                    if (hRsrc == IntPtr.Zero)
                                    {
                                        msg = String.Format(
                                            "Could not find the component icon resource with id {0}",
                                            ide[iconEntry].nID);
                                        failed = true;
                                        break;
                                    }
                                    else
                                    {
                                        // load the resource:
                                        hGlobal = LoadResource(
                                            hLibrary,
                                            hRsrc);
                                        if (hGlobal == IntPtr.Zero)
                                        {
                                            msg = String.Format(
                                                "Could not load the component icon resource with id {0}",
                                                ide[iconEntry].nID);
                                            failed = true;
                                            break;
                                        }
                                        else
                                        {
                                            // check the size:
                                            int resSize = SizeofResource(hLibrary, hRsrc);
                                            if ((resSize > 0) && (resSize == ide[iconEntry].dwBytesInRes))
                                            {
                                                // ok
                                                lPtr = LockResource(hGlobal);
                                                byte[] b = new byte[resSize];
                                                Marshal.Copy(lPtr, b, 0, resSize);
                                                icons[iconEntry] = new IconDeviceImage(b);
                                            }
                                            else
                                            {
                                                msg = String.Format(
                                                    "Component icon resource with id {0} is corrupt",
                                                    ide[iconEntry].nID);
                                                failed = true;
                                            }
                                        }
                                    }
                                }
                                if (!failed)
                                {
                                    // Add the icons to the collection:
                                    this.iconCollection = new IconDeviceImageCollection(icons);
                                }
                            }
                            else
                            {
                                msg = "Can't lock resource for reading.";
                                failed = true;
                            }
                        }
                        else
                        {
                            msg = "Can't load resource for reading.";
                            failed = true;
                        }
                    }
                    else
                    {
                        msg = "Can't find resource.";
                        failed = true;
                    }
                }
                else
                {
                    msg = "Can't load library.";
                    failed = true;
                }
            }
            catch (Exception ex)
            {
                failed = true;
                msg = ex.Message;
            }
            finally
            {
                // clear up handles:
                if (hGlobal != IntPtr.Zero)
                {
                    FreeResource(hGlobal);
                }
                if (hLibrary != IntPtr.Zero)
                {
                    FreeLibrary(hLibrary);
                }
                if (failed)
                {
                    throw new IconExException(msg);
                }
            }

        }

        private void loadFromFile(
            string iconFile)
        {
            loadInitialise();

            // Open the file
            FileStream fs = new FileStream(
                iconFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            BinaryReader br = new BinaryReader(fs);

            try
            {
                // read the header:
                int iconCount = readIconFileHeader(br);
                // read the directory:
                ICONDIRENTRY[] ide = new ICONDIRENTRY[iconCount];
                for (int iconEntry = 0; iconEntry < iconCount; iconEntry++)
                {
                    ide[iconEntry] = new ICONDIRENTRY(br);
                }
                IconDeviceImage[] icons = new IconDeviceImage[iconCount];
                // read the actual icons:
                for (int iconEntry = 0; iconEntry < iconCount; iconEntry++)
                {
                    fs.Seek(ide[iconEntry].dwImageOffset, SeekOrigin.Begin);
                    byte[] b = new byte[ide[iconEntry].dwBytesInRes];
                    br.Read(b, 0, ide[iconEntry].dwBytesInRes);
                    icons[iconEntry] = new IconDeviceImage(b);
                }
                // Add the icons to the collection:
                this.iconCollection = new IconDeviceImageCollection(icons);
            }
            catch (Exception ex)
            {
                if (ex is SystemException)
                {
                    throw ex;
                }
                else
                {
                    throw new IconExException("Failed to read icon file.", ex);
                }
            }
            finally
            {
                br.Close();
            }

            this.iconFile = iconFile;
        }

        private int readResourceIconFileHeader(
            IntPtr lPtr)
        {
            int idReserved = Marshal.ReadInt16(lPtr);
            int idType = Marshal.ReadInt16(lPtr, 2);
            int idCount = Marshal.ReadInt16(lPtr, 4);
            if ((idReserved == 0) &&
                (idType == IMAGE_ICON) &&
                (idCount > 0) &&
                (idCount < 1024))
            {
                return idCount;
            }
            else
            {
                throw new IconExException("Invalid Icon File Header");
            }
        }

        private int readIconFileHeader(
            BinaryReader br)
        {
            int idReserved = br.ReadInt16();
            int idType = br.ReadInt16();
            int idCount = br.ReadInt16();
            if ((idReserved == 0) &&
                (idType == IMAGE_ICON) &&
                (idCount > 0) &&
                (idCount < 1024))
            {
                return idCount;
            }
            else
            {
                throw new IconExException("Invalid Icon File Header");
            }
        }

        private void writeIconFileHeader(
            BinaryWriter bw)
        {
            Int16 idReserved = 0;
            bw.Write(idReserved);
            Int16 idType = IMAGE_ICON;
            bw.Write(idType);
            Int16 idCount = (Int16)this.Items.Count;
            bw.Write(idCount);

            Console.WriteLine(string.Format("{0} {1} {2} Iconex", idReserved, idType, idCount));
        }

        #endregion

        #region Constructor, Dispose
        /// <summary>
        /// Constructs a new, empty instance of the IconEx
        /// object
        /// </summary>
        public IconEx()
        {
        }

        /// <summary>
        /// Constructs an IconEx instance and opens the icon
        /// file specified.
        /// </summary>
        /// <param name="iconFile">Icon file to read</param>
        public IconEx(string iconFile)
        {
            loadFromFile(iconFile);
        }

        /// <summary>
        /// Constructs an IconEx instance and opens the icon
        /// from the specified library (Executable or DLL)
        /// with the specified integer resource identifier
        /// </summary>
        /// <param name="libraryFile">Executable or DLL to extract
        /// icon from</param>
        /// <param name="resourceId">Integer resource Id</param>
        public IconEx(
            string libraryFile,
            int resourceId
            )
        {
            FromLibrary(libraryFile, resourceId);
        }

        /// <summary>
        /// Constructs an IconEx instance and opens the icon
        /// from the specified library (Executable or DLL)
        /// with the specified string resource identifier
        /// </summary>
        /// <param name="libraryFile">Executable or DLL to extract
        /// icon from</param>
        /// <param name="resourceName">String resource Id</param>
        public IconEx(
            string libraryFile,
            string resourceName
            )
        {
            FromLibrary(libraryFile, resourceName);
        }
        public void Dispose()
        {
            if (this.iconCollection != null)
            {
                iconCollection.Dispose();
                iconCollection = null;
            }
        }
        #endregion
    }
    #endregion

    #region IconDeviceImageCollection
    /// <summary>
    /// Manages a read/write collection of icon resources
    /// within an Icon file
    /// </summary>
    public class IconDeviceImageCollection : CollectionBase, IDisposable
    {
        #region Member Variables
        #endregion

        #region Methods
        /// <summary>
        /// Add a new icon device image
        /// </summary>
        /// <param name="icon">Icon to add</param>
        public void Add(IconDeviceImage icon)
        {
            foreach (IconDeviceImage iconExisting in this.InnerList)
            {
                if (icon.IconSize.Equals(iconExisting.IconSize) &&
                    icon.ColorDepth.Equals(iconExisting.ColorDepth))
                {
                    throw new IconExException("An Icon Device Image with the same size and colour depth already exists in this icon");
                }
            }
            this.InnerList.Add(icon);
        }
        /// <summary>
        /// Gets the IconDevice Image at the specified
        /// index
        /// </summary>
        public IconDeviceImage this[int index]
        {
            get
            {
                return (IconDeviceImage)this.InnerList[index];
            }
        }
        #endregion

        #region Constructor, Dispose
        /// <summary>
        ///  Constructs a new, empty collection of device
        ///  images.
        /// </summary>
        public IconDeviceImageCollection()
        {
        }
        /// <summary>
        /// Constructs a new collection of device images
        /// </summary>
        /// <param name="icons">Icons to add</param>
        public IconDeviceImageCollection(
            IconDeviceImage[] icons
            )
        {
            foreach (IconDeviceImage icon in icons)
            {
                this.InnerList.Add(icon);
            }
        }
        public void Dispose()
        {
            if (this.InnerList != null)
            {
                foreach (IconDeviceImage icon in this.InnerList)
                {
                    icon.Dispose();
                }
                this.InnerList.Clear();
            }
        }
        #endregion
    }
    #endregion

    #region IconDeviceImage
    /// <summary>
    /// Manages a single icon device image within an
    /// Icon file
    /// </summary>
    public class IconDeviceImage : IDisposable
    {
        #region Member Variables
        private Size size;
        private System.Windows.Forms.ColorDepth colorDepth = ColorDepth.Depth4Bit;
        private byte[] data;
        private IntPtr hIcon = IntPtr.Zero;
        #endregion

        #region Unmanaged Code
        [DllImport("gdi32")]
        private static extern int SetDIBitsToDevice(
            IntPtr hdc,
            int X, int Y, int dx, int dy,
            int SrcX, int SrcY, int Scan, int NumScans,
            IntPtr Bits,
            IntPtr BitsInfo,
            int wUsage);
        [DllImport("gdi32")]
        public static extern int GetDIBits(
            IntPtr hdc,
            IntPtr hBitmap,
            int nStartScan,
            int nNumScans,
            IntPtr Bits,
            IntPtr BitsInfo,
            int wUsage);

        private const int DIB_RGB_COLORS = 0; //  color table in RGBs
        private const int DIB_PAL_COLORS = 1; //  color table in palette indices
        private const int DIB_PAL_INDICES = 2; //  No color table indices into surf palette
        private const int DIB_PAL_PHYSINDICES = 2; //  No color table indices into surf palette
        private const int DIB_PAL_LOGINDICES = 4; //  No color table indices into DC palette

        // Bitmap compression types:
        private const int BI_RGB = 0x0;
        private const int BI_RLE4 = 0x2;
        private const int BI_RLE8 = 0x1;

        [DllImport("gdi32")]
        private static extern IntPtr CreateCompatibleDC(
            IntPtr hdc);

        [DllImport("gdi32", CharSet = CharSet.Auto)]
        private static extern IntPtr CreateDC(
            [MarshalAs(UnmanagedType.LPTStr)]
			string lpDriverName,
            IntPtr lpDeviceName,
            IntPtr lpOutput,
            IntPtr lpInitData);

        [DllImport("gdi32")]
        private static extern IntPtr CreateCompatibleBitmap(
            IntPtr hdc,
            int width,
            int height);

        [DllImport("gdi32")]
        private static extern IntPtr SelectObject(
            IntPtr hdc, IntPtr hObject);

        [DllImport("gdi32")]
        private static extern int DeleteObject(
            IntPtr hObject);

        [DllImport("gdi32")]
        private static extern int DeleteDC(
            IntPtr hdc);

        [DllImport("user32")]
        private static extern int DestroyIcon(
            IntPtr hIcon);

        [DllImport("user32")]
        private static extern IntPtr CreateIconIndirect(
            ref ICONINFO piconInfo);

        private const Int16 IMAGE_ICON = 1;

        #endregion

        #region Structures

        #region ICONINFO.  Used by the CreateIconIndirect API function
        private struct ICONINFO
        {
            public int fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hBmMask;
            public IntPtr hBmColor;
        }
        #endregion

        #region BITMAPINFOHEADER.  This is stored at the start of an icon's data
        private struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public Int16 biPlanes;
            public Int16 biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;

            public BITMAPINFOHEADER(
                Size size,
                ColorDepth colorDepth
                )
            {
                this.biSize = 0;
                this.biWidth = size.Width;
                this.biHeight = size.Height * 2;
                this.biPlanes = 1;
                this.biCompression = BI_RGB;
                this.biSizeImage = 0;
                this.biXPelsPerMeter = 0;
                this.biYPelsPerMeter = 0;
                this.biClrUsed = 0;
                this.biClrImportant = 0;
                switch (colorDepth)
                {
                    case ColorDepth.Depth4Bit:
                        this.biBitCount = 4;
                        break;
                    case ColorDepth.Depth8Bit:
                        this.biBitCount = 8;
                        break;
                    case ColorDepth.Depth16Bit:
                        this.biBitCount = 16;
                        break;
                    case ColorDepth.Depth24Bit:
                        this.biBitCount = 24;
                        break;
                    case ColorDepth.Depth32Bit:
                        this.biBitCount = 32;
                        break;
                    default:
                        this.biBitCount = 4;
                        break;
                }
                this.biSize = Marshal.SizeOf(this.GetType());
            }

            public void Write(
                BinaryWriter bw)
            {
                bw.Write(this.biSize);
                bw.Write(this.biWidth);
                bw.Write(this.biHeight);
                bw.Write(this.biPlanes);
                bw.Write(this.biBitCount);
                bw.Write(this.biCompression);
                bw.Write(this.biSizeImage);
                bw.Write(this.biXPelsPerMeter);
                bw.Write(this.biYPelsPerMeter);
                bw.Write(this.biClrUsed);
                bw.Write(this.biClrImportant);
            }

            public BITMAPINFOHEADER(byte[] data)
            {
                MemoryStream ms = new MemoryStream(data, false);
                BinaryReader br = new BinaryReader(ms);
                biSize = br.ReadInt32();
                biWidth = br.ReadInt32();
                biHeight = br.ReadInt32();
                biPlanes = br.ReadInt16();
                biBitCount = br.ReadInt16();
                biCompression = br.ReadInt32();
                biSizeImage = br.ReadInt32();
                biXPelsPerMeter = br.ReadInt32();
                biYPelsPerMeter = br.ReadInt32();
                biClrUsed = br.ReadInt32();
                biClrImportant = br.ReadInt32();
                br.Close();
            }

            public override string ToString()
            {
                return string.Format(
                    "biSize: {0}, biWidth: {1}, biHeight: {2}, biPlanes: {3}, biBitCount: {4}, biCompression: {5}, biSizeImage: {6}, biXPelsPerMeter: {7}, biYPelsPerMeter {8}, biClrUsed {9}, biClrImportant {10}",
                    biSize, biWidth, biHeight, biPlanes, biBitCount,
                    biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter,
                    biClrUsed, biClrImportant);
            }
        }
        #endregion

        #region RQBQUAD. Used to store colours in a paletised icon (2, 4 or 8 bit)
        private struct RGBQUAD
        {
            public byte rgbBlue;
            public byte rgbGreen;
            public byte rgbRed;
            public byte rgbReserved;

            public RGBQUAD(
                byte r, byte g, byte b, byte alpha
                )
            {
                rgbBlue = b;
                rgbGreen = g;
                rgbRed = r;
                rgbReserved = 0; //alpha;
            }
            public RGBQUAD(
                Color c
                )
            {
                rgbBlue = c.B;
                rgbGreen = c.G;
                rgbRed = c.R;
                rgbReserved = 0; //c.A;
            }
            public void Write(
                BinaryWriter bw)
            {
                bw.Write(this.rgbBlue);
                bw.Write(this.rgbGreen);
                bw.Write(this.rgbRed);
                bw.Write(this.rgbReserved);
            }
            public override string ToString()
            {
                return string.Format(
                    "rgbBlue: {0}, rgbGreen: {1}, rgbRed: {2}",
                    rgbBlue, rgbGreen, rgbRed);
            }
        }
        #endregion

        #endregion

        #region Properties
        /// <summary>
        /// Gets the Icon handle for this device image
        /// </summary>
        public IntPtr Handle
        {
            get
            {
                return this.hIcon;
            }
        }
        /// <summary>
        /// Gets the size of this device image
        /// </summary>
        public Size IconSize
        {
            get
            {
                return this.size;
            }
        }
        /// <summary>
        /// Gets the colour depth of this device image
        /// </summary>
        public System.Windows.Forms.ColorDepth ColorDepth
        {
            get
            {
                return this.colorDepth;
            }
        }

        /// <summary>
        /// Gets/sets the Mask Image of the icon as a bitmap
        /// </summary>
        public System.Drawing.Bitmap MaskImage
        {
            get
            {
                IntPtr junk = IntPtr.Zero;
                Bitmap bm = getIconBitmap(true, false, ref junk);
                return bm;
            }
            set
            {
                setMaskBitsFromBitmap(value);
            }
        }
        /// <summary>
        /// Gets/sets the image portion of the icon as a bitmap
        /// </summary>
        public System.Drawing.Bitmap IconImage
        {
            get
            {
                IntPtr junk = IntPtr.Zero;
                Bitmap bm = getIconBitmap(false, false, ref junk);
                return bm;
            }
            set
            {
                setImageBitsFromBitmap(value);
            }
        }
        /// <summary>
        /// Gets the device image as a managed icon
        /// Note that you should clone the icon if you want to keep it
        /// after this class has been disposed.		
        /// </summary>
        public System.Drawing.Icon Icon
        {
            get
            {
                System.Drawing.Icon icon = System.Drawing.Icon.FromHandle(this.hIcon);
                return icon;
            }
        }
        #endregion

        #region Private Implementation
        private void setMaskBitsFromBitmap(
            Bitmap bm
            )
        {
            IntPtr hdcc = CreateDC("DISPLAY", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            IntPtr hdc = CreateCompatibleDC(hdcc);
            DeleteDC(hdcc);
            IntPtr hBmp = bm.GetHbitmap();

            BITMAPINFOHEADER bmInfoHdr = new BITMAPINFOHEADER(
                this.size, this.colorDepth);

            // Now prepare the for GetDIBits call:
            RGBQUAD rgbQuad = new RGBQUAD();
            int monoBmHdrSize = bmInfoHdr.biSize + Marshal.SizeOf(rgbQuad) * 2;

            IntPtr bitsInfo = Marshal.AllocCoTaskMem(
                monoBmHdrSize);
            Marshal.WriteInt32(bitsInfo, Marshal.SizeOf(bmInfoHdr));
            Marshal.WriteInt32(bitsInfo, 4, this.size.Width);
            Marshal.WriteInt32(bitsInfo, 8, this.size.Height);
            Marshal.WriteInt16(bitsInfo, 12, 1);
            Marshal.WriteInt16(bitsInfo, 14, 1);
            Marshal.WriteInt32(bitsInfo, 16, BI_RGB);
            Marshal.WriteInt32(bitsInfo, 20, 0);
            Marshal.WriteInt32(bitsInfo, 24, 0);
            Marshal.WriteInt32(bitsInfo, 28, 0);
            Marshal.WriteInt32(bitsInfo, 32, 0);
            Marshal.WriteInt32(bitsInfo, 36, 0);
            // Write the black and white colour indices:
            Marshal.WriteInt32(bitsInfo, 40, 0);
            Marshal.WriteByte(bitsInfo, 44, 255);
            Marshal.WriteByte(bitsInfo, 45, 255);
            Marshal.WriteByte(bitsInfo, 46, 255);
            Marshal.WriteByte(bitsInfo, 47, 0);

            int maskImageBytes = MaskImageSize(bmInfoHdr);
            IntPtr bits = Marshal.AllocCoTaskMem(maskImageBytes);

            int success = GetDIBits(hdc, hBmp, 0, this.size.Height, bits, bitsInfo, DIB_RGB_COLORS);

            Marshal.Copy(bits, data, MaskImageIndex(bmInfoHdr), maskImageBytes);

            // Free memory:
            Marshal.FreeCoTaskMem(bits);
            Marshal.FreeCoTaskMem(bitsInfo);

            DeleteObject(hBmp);
            DeleteDC(hdc);

            createIcon();

        }
        private void setImageBitsFromBitmap(
            Bitmap bm
            )
        {
            IntPtr hdcc = CreateDC("DISPLAY", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            IntPtr hdc = CreateCompatibleDC(hdcc);
            DeleteDC(hdcc);
            IntPtr hBmp = bm.GetHbitmap();

            BITMAPINFOHEADER bmInfoHdr = new BITMAPINFOHEADER(
                this.size, this.colorDepth);

            // Now prepare for GetDIBits call:
            int xorIndex = XorImageIndex(bmInfoHdr);
            int xorImageBytes = XorImageSize(bmInfoHdr);

            // Get the BITMAPINFO header into the pointer:
            IntPtr bitsInfo = Marshal.AllocCoTaskMem(
                xorIndex);
            Marshal.Copy(data, 0, bitsInfo, xorIndex);
            // fix the height:
            Marshal.WriteInt32(bitsInfo, 8, bmInfoHdr.biHeight / 2);

            IntPtr bits = Marshal.AllocCoTaskMem(xorImageBytes);

            int success = GetDIBits(hdc, hBmp, 0, this.size.Height, bits, bitsInfo, DIB_RGB_COLORS);

            Marshal.Copy(bits, data, xorIndex, xorImageBytes);

            // Free memory:
            Marshal.FreeCoTaskMem(bits);
            Marshal.FreeCoTaskMem(bitsInfo);

            DeleteObject(hBmp);
            DeleteDC(hdc);

            createIcon();
        }

        private void setDeviceImage(
            Size size,
            System.Windows.Forms.ColorDepth colorDepth
            )
        {
            this.size = size;
            this.colorDepth = colorDepth;
            // Initialise the data:
            BITMAPINFOHEADER bmInfoHdr = new BITMAPINFOHEADER(
                size, colorDepth);
            this.data = new byte[
                this.MaskImageIndex(bmInfoHdr) + this.MaskImageSize(bmInfoHdr)];

            MemoryStream mw = new MemoryStream(this.data, 0, this.data.Length, true);
            BinaryWriter bw = new BinaryWriter(mw);
            bmInfoHdr.Write(bw);
            // Write the colour indexes if required:
            switch (this.colorDepth)
            {
                case ColorDepth.Depth4Bit:
                    write16ColorPalette(bw);
                    break;
                case ColorDepth.Depth8Bit:
                    write256ColorPalette(bw);
                    break;
            }
            bw.Close();

        }

        private void write16ColorPalette(
            BinaryWriter bw)
        {
            // Write out 16 entries containing the
            // standard colour palette:
            writeColor(bw, Color.Black);
            writeColor(bw, Color.White);
            writeColor(bw, Color.Red);
            writeColor(bw, Color.Green);
            writeColor(bw, Color.Blue);
            writeColor(bw, Color.Yellow);
            writeColor(bw, Color.Magenta);
            writeColor(bw, Color.Cyan);
            writeColor(bw, Color.Gray);
            writeColor(bw, Color.DarkRed);
            writeColor(bw, Color.DarkGreen);
            writeColor(bw, Color.DarkBlue);
            writeColor(bw, Color.Olive);
            writeColor(bw, Color.Purple);
            writeColor(bw, Color.Teal);
            writeColor(bw, Color.DarkGray);
        }
        private void write256ColorPalette(
            BinaryWriter bw)
        {
            KnownColor kc = KnownColor.ActiveBorder;
            Array colors = Enum.GetValues(kc.GetType());
            int i = 0;
            foreach (KnownColor color in colors)
            {
                writeColor(bw, Color.FromKnownColor(color));
                i++;
                if (i > 255)
                {
                    break;
                }
            }
        }

        private void writeColor(
            BinaryWriter bw,
            Color color
            )
        {
            RGBQUAD r = new RGBQUAD(color);
            r.Write(bw);
        }

        private Bitmap getIconBitmap(
            bool mask,
            bool returnHandle,
            ref IntPtr hBmp
            )
        {
            // Bitmap to return
            Bitmap bm = null;

            // Get bitmap info:			
            BITMAPINFOHEADER bmInfoHdr = new BITMAPINFOHEADER(data);

            if (mask)
            {
                // extract monochrome mask
                IntPtr hdc = CreateCompatibleDC(IntPtr.Zero);
                hBmp = CreateCompatibleBitmap(hdc, bmInfoHdr.biWidth, bmInfoHdr.biHeight / 2);
                IntPtr hBmpOld = SelectObject(hdc, hBmp);

                // Prepare BitmapInfoHeader for mono bitmap:
                RGBQUAD rgbQuad = new RGBQUAD();
                int monoBmHdrSize = bmInfoHdr.biSize + Marshal.SizeOf(rgbQuad) * 2;

                IntPtr bitsInfo = Marshal.AllocCoTaskMem(
                    monoBmHdrSize);
                Marshal.WriteInt32(bitsInfo, Marshal.SizeOf(bmInfoHdr));
                Marshal.WriteInt32(bitsInfo, 4, bmInfoHdr.biWidth);
                Marshal.WriteInt32(bitsInfo, 8, bmInfoHdr.biHeight / 2);
                Marshal.WriteInt16(bitsInfo, 12, 1);
                Marshal.WriteInt16(bitsInfo, 14, 1);
                Marshal.WriteInt32(bitsInfo, 16, BI_RGB);
                Marshal.WriteInt32(bitsInfo, 20, 0);
                Marshal.WriteInt32(bitsInfo, 24, 0);
                Marshal.WriteInt32(bitsInfo, 28, 0);
                Marshal.WriteInt32(bitsInfo, 32, 0);
                Marshal.WriteInt32(bitsInfo, 36, 0);
                // Write the black and white colour indices:
                Marshal.WriteInt32(bitsInfo, 40, 0);
                Marshal.WriteByte(bitsInfo, 44, 255);
                Marshal.WriteByte(bitsInfo, 45, 255);
                Marshal.WriteByte(bitsInfo, 46, 255);
                Marshal.WriteByte(bitsInfo, 47, 0);

                // Prepare Mask bits:
                int maskImageBytes = MaskImageSize(bmInfoHdr);
                IntPtr bits = Marshal.AllocCoTaskMem(maskImageBytes);
                Marshal.Copy(data, MaskImageIndex(bmInfoHdr), bits, maskImageBytes);

                int success = SetDIBitsToDevice(
                    hdc,
                    0, 0, bmInfoHdr.biWidth, bmInfoHdr.biHeight / 2,
                    0, 0, 0, bmInfoHdr.biHeight / 2,
                    bits,
                    bitsInfo,
                    DIB_RGB_COLORS);

                Marshal.FreeCoTaskMem(bits);
                Marshal.FreeCoTaskMem(bitsInfo);

                SelectObject(hdc, hBmpOld);
                DeleteObject(hdc);

            }
            else
            {
                // extract colour (XOR) part of image:

                // Create bitmap:
                IntPtr hdcDesktop = CreateDC("DISPLAY", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                IntPtr hdc = CreateCompatibleDC(hdcDesktop);
                hBmp = CreateCompatibleBitmap(hdcDesktop, bmInfoHdr.biWidth, bmInfoHdr.biHeight / 2);
                DeleteDC(hdcDesktop);
                IntPtr hBmpOld = SelectObject(hdc, hBmp);

                // Find the index of the XOR bytes:
                int xorIndex = XorImageIndex(bmInfoHdr);
                int xorImageSize = XorImageSize(bmInfoHdr);

                // Get Bitmap info header to a pointer:								
                IntPtr bitsInfo = Marshal.AllocCoTaskMem(xorIndex);
                Marshal.Copy(data, 0, bitsInfo, xorIndex);
                // fix the height:
                Marshal.WriteInt32(bitsInfo, 8, bmInfoHdr.biHeight / 2);

                // Get the XOR bits:				
                IntPtr bits = Marshal.AllocCoTaskMem(xorImageSize);
                Marshal.Copy(data, xorIndex, bits, xorImageSize);

                int success = SetDIBitsToDevice(
                    hdc,
                    0, 0, bmInfoHdr.biWidth, bmInfoHdr.biHeight / 2,
                    0, 0, 0, bmInfoHdr.biHeight / 2,
                    bits,
                    bitsInfo,
                    DIB_RGB_COLORS);

                Marshal.FreeCoTaskMem(bits);
                Marshal.FreeCoTaskMem(bitsInfo);

                SelectObject(hdc, hBmpOld);
                DeleteObject(hdc);
            }

            if (!returnHandle)
            {
                // the bitmap will own the handle and clear
                // it up when it is disposed.  Otherwise
                // need to call DeleteObject on hBmp
                // returned.
                bm = Bitmap.FromHbitmap(hBmp);
            }
            return bm;
        }

        private int MaskImageIndex(
            BITMAPINFOHEADER bmInfoHeader
            )
        {
            int maskImageIndex = XorImageIndex(bmInfoHeader);
            maskImageIndex += XorImageSize(bmInfoHeader);
            return maskImageIndex;
        }

        private int XorImageSize(
            BITMAPINFOHEADER bmInfoHeader
            )
        {
            int imageBytes = (bmInfoHeader.biHeight / 2 *
                WidthBytes(bmInfoHeader.biWidth * bmInfoHeader.biBitCount * bmInfoHeader.biPlanes));
            return imageBytes;
        }

        private int MaskImageSize(
            BITMAPINFOHEADER bmInfoHeader
            )
        {
            int imageBytes = bmInfoHeader.biHeight / 2 *
                WidthBytes(bmInfoHeader.biWidth);
            return imageBytes;
        }

        private int WidthBytes(int width)
        {
            // Returns the width of a row in a DIB Bitmap given the
            // number of bits.  DIB Bitmap rows always align on a 
            // DWORD boundary.
            int widthBytes = ((width + 31) / 32) * 4;
            return widthBytes;
        }

        private int XorImageIndex(
            BITMAPINFOHEADER bmInfoHeader)
        {
            // Returns the position of the DIB bitmap bits within a
            // DIB bitmap array:
            RGBQUAD rgbq = new RGBQUAD();
            return Marshal.SizeOf(bmInfoHeader) +
                dibNumColors(bmInfoHeader) * Marshal.SizeOf(rgbq);
        }
        private int dibNumColors(
            BITMAPINFOHEADER bmInfoHeader)
        {
            int colorCount = 0;
            if (bmInfoHeader.biClrUsed != 0)
            {
                colorCount = bmInfoHeader.biClrUsed;
            }
            else
            {
                switch (bmInfoHeader.biBitCount)
                {
                    case 1:
                        colorCount = 2;
                        break;
                    case 4:
                        colorCount = 16;
                        break;
                    case 8:
                        colorCount = 256;
                        break;
                }
            }
            return colorCount;
        }
        /// <summary>
        /// Internal method.  Returns the number of bytes in the
        /// icon data.  Not intended for public use.
        /// </summary>
        /// <returns>Number of bytes of icon data</returns>
        internal int IconImageDataBytes()
        {
            return this.data.Length;
        }
        /// <summary>
        /// Internal method.  Writes the icon bitmap data to
        /// the specified BinaryWriter.  Not intended for 
        /// public use.
        /// </summary>
        /// <param name="bw">BinaryWriter to write to</param>
        internal void SaveIconBitmapData(
            BinaryWriter bw)
        {
            bw.Write(this.data, 0, this.data.Length);
        }
        private void createIcon()
        {
            if (this.hIcon != IntPtr.Zero)
            {
                DestroyIcon(this.hIcon);
                this.hIcon = IntPtr.Zero;
            }

            ICONINFO ii = new ICONINFO();
            ii.fIcon = IMAGE_ICON;
            getIconBitmap(false, true, ref ii.hBmColor);
            getIconBitmap(true, true, ref ii.hBmMask);

            this.hIcon = CreateIconIndirect(ref ii);

            DeleteObject(ii.hBmColor);
            DeleteObject(ii.hBmMask);
        }
        #endregion

        #region Constructor, Dispose
        /// <summary>
        /// Constructs a new IconDeviceImage with the specified
        /// size and colour depth.
        /// </summary>
        /// <param name="size">Size of device image</param>
        /// <param name="colorDepth">Colour depth of device image</param>
        public IconDeviceImage(
            Size size,
            System.Windows.Forms.ColorDepth colorDepth
            )
        {
            setDeviceImage(size, colorDepth);
            createIcon();
        }

        /// <summary>
        /// Constructs a new IconDeviceImage from a Managed Icon
        /// </summary>
        /// <param name="icon">Icon to construct from</param>		
        public IconDeviceImage(
            System.Drawing.Icon icon
            )
        {
            // use DrawIconEx to create bitmaps for the 
            // colour and mask images, then use GetDIBits
            // to populate data
        }

        /// <summary>
        /// Constructs a new icon device image from an array of
        /// bytes in the Icon file format
        /// </summary>
        /// <param name="b">Array of bytes</param>
        internal IconDeviceImage(
            byte[] b
            )
        {
            // store the bytes:
            data = new Byte[b.Length];
            for (int i = 0; i < b.Length; i++)
            {
                data[i] = b[i];
            }

            // Read the BitmapInfoHeader structure to get the 
            // size and number of bytes:
            BITMAPINFOHEADER bmInfoHeader = new BITMAPINFOHEADER(data);
            //Console.WriteLine(bmInfoHeader.ToString());

            this.size.Width = bmInfoHeader.biWidth;
            this.size.Height = bmInfoHeader.biHeight / 2;
            switch (bmInfoHeader.biBitCount)
            {
                case 1:
                case 4:
                    this.colorDepth = ColorDepth.Depth4Bit;
                    break;
                case 8:
                    this.colorDepth = ColorDepth.Depth8Bit;
                    break;
                case 16:
                    this.colorDepth = ColorDepth.Depth16Bit;
                    break;
                case 24:
                    this.colorDepth = ColorDepth.Depth24Bit;
                    break;
                case 32:
                    this.colorDepth = ColorDepth.Depth32Bit;
                    break;
            }
            createIcon();
        }
        /// <summary>
        /// Clears up any resources associated with this
        /// Icon Device Image.
        /// </summary>
        public void Dispose()
        {
            if (this.hIcon != IntPtr.Zero)
            {
                DestroyIcon(this.hIcon);
                this.hIcon = IntPtr.Zero;
            }
        }
        #endregion
    }
    #endregion
}