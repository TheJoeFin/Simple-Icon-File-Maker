using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace IconConverter.IconEx
{
    public class GroupIconResources : ReadOnlyCollectionBase
    {
        #region Unmanaged Code
        private delegate int EnumResNamesProc(
            IntPtr hMod,
            IntPtr lpszType,
            IntPtr lpszName,
            IntPtr lParam
            );
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private extern static int EnumResourceNames(
            IntPtr hModule,
            IntPtr lpType,
            [MarshalAs(UnmanagedType.FunctionPtr)]
			EnumResNamesProc lpEnumFunc,
            IntPtr lParam);
        [DllImport("kernel32", CharSet = CharSet.Auto)]
        private extern static IntPtr LoadLibraryEx(
            [MarshalAs(UnmanagedType.LPTStr)]
			string lpLibFileName,
            IntPtr hFile,
            int dwFlags);
        [DllImport("kernel32")]
        private extern static int FreeLibrary(
            IntPtr hLibModule);
        private const int LOAD_LIBRARY_AS_DATAFILE = 0x2;
        private const int RT_ICON = 3;
        private const int RT_GROUP_ICON = RT_ICON + 11;
        #endregion

        #region Member Variables
        private string libraryFile;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the GroupIconResource with the specified index
        /// </summary>
        public GroupIconResource this[int index]
        {
            get
            {
                return (GroupIconResource)this.InnerList[index];
            }
        }

        /// <summary>
        /// Gets/sets the library file to load resources from
        /// </summary>
        public string LibraryFile
        {
            get
            {
                return this.libraryFile;
            }
            set
            {
                loadResources(value);
            }
        }
        #endregion

        #region Private Implementation
        private void loadResources(
            string libraryFile
            )
        {
            // Load the library:
            IntPtr hLibrary = LoadLibraryEx(libraryFile, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
            if (hLibrary != IntPtr.Zero)
            {
                EnumResourceNames(
                    hLibrary,
                    (IntPtr)RT_GROUP_ICON,
                    new EnumResNamesProc(MyResourceEnum),
                    IntPtr.Zero);
                FreeLibrary(hLibrary);
                this.libraryFile = libraryFile;
            }
        }

        private int MyResourceEnum(
            IntPtr hMod,
            IntPtr lpszType,
            IntPtr lpszName,
            IntPtr lParam
            )
        {
            uint res = (uint)lpszName;
            if ((res & 0xFFFF0000) == 0)
            {
                // numeric resource type:
                int resourceId = (int)(res & 0xFFFF);
                GroupIconResource g = new GroupIconResource(resourceId);
                this.InnerList.Add(g);
            }
            else
            {
                // string resource type:
                string resourceName = Marshal.PtrToStringAuto(lpszName);
                GroupIconResource g = new GroupIconResource(resourceName);
                this.InnerList.Add(g);
            }
            return 1;
        }
        #endregion

        #region Constructor, Dispose
        /// <summary>
        /// Creates an instance of the class but doesn't
        /// enumerate any resources
        /// </summary>
        public GroupIconResources()
        {
        }

        /// <summary>
        /// Creates an instance of the class and enumerates
        /// icon resources for the specified file.
        /// </summary>
        /// <param name="libraryFile">File to get resources
        /// for</param>
        public GroupIconResources(
            string libraryFile
            )
        {
            loadResources(libraryFile);
        }
        #endregion

    }

    /// <summary>
    /// Stores the identifier for a single group
    /// icon resource.
    /// </summary>
    public class GroupIconResource
    {

        private int resourceId = 0;
        private string resourceName = "";
        private bool idIsNumeric = false;

        public bool IdIsNumeric
        {
            get
            {
                return this.idIsNumeric;
            }
        }

        public int Id
        {
            get
            {
                return this.resourceId;
            }
        }

        public string Name
        {
            get
            {
                return this.resourceName;
            }
        }

        internal GroupIconResource(
            int id
            )
        {
            this.resourceId = id;
            this.idIsNumeric = true;
        }
        internal GroupIconResource(
            string resourceName
            )
        {
            this.resourceName = resourceName;
            this.idIsNumeric = false;
        }
    }
}
