using FolderMove;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Foldermove
{
    class Win32Directory
    {
        public const int MAX_PATH = 260;
        public const int MAX_ALTERNATE = 14;
        public const int FILE_ATTRIBUTE_DIRECTORY = 0x10;
        public const int FIND_FIRST_EX_LARGE_FETCH = 2;

        
        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public uint nFileSizeHigh; //changed all to uint from int, otherwise you run into unexpected overflow
            public uint nFileSizeLow;  //| http://www.pinvoke.net/default.aspx/Structures/WIN32_FIND_DATA.html
            public uint dwReserved0;   //|
            public uint dwReserved1;   //v
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_ALTERNATE)]
            public string cAlternate;
        }

        
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindFirstFileEX(string lpFileName, out WIN32_FIND_DATA lpFindFileData, int dwAdditionalFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll")]
        public static extern bool FindClose(IntPtr hFindFile);

        public long RecurseDirectory(string directory, int level, out int files)
        {
            IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
            long size = 0;
            files = 0;
            //folders = 0;

            IntPtr findHandle;

            // please note that the following line won't work if you try this on a network folder, like \\Machine\C$
            // simply remove the \\?\ part in this case or use \\?\UNC\ prefix
            findHandle = FindFirstFileEX(@"\\?\" + directory + @"\*", out WIN32_FIND_DATA findData, 2);
            if (findHandle != INVALID_HANDLE_VALUE)
            {

                do
                {
                    if ((findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
                    {

                        if (findData.cFileName != "." && findData.cFileName != "..")
                        {
                            //folders++;

                            string subdirectory = directory + (directory.EndsWith(@"\") ? "" : @"\") +
                                findData.cFileName;
                            if (level != 0)  // allows -1 to do complete search.
                            {
                                size += RecurseDirectory(subdirectory, level - 1, out int subfiles);

                                //folders += subfolders;
                                files += subfiles;
                            }
                        }
                    }
                    else
                    {
                        // File
                        files++;

                        size += (long)findData.nFileSizeLow + (long)findData.nFileSizeHigh * 4294967296;
                    }
                }
                while (FindNextFile(findHandle, out findData));
                FindClose(findHandle);

            }

            return size;
        }

        // copied with minor changes from pinvoke.net by Brocks
        // [Sample by K&#229;re Smith] // [Minor edits by Mike Liddell] 
        //[More minor edits Rob T]
    }
}
