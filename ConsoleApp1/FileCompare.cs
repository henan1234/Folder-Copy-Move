using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class FileCompare : System.Collections.Generic.IEqualityComparer<Alphaleonis.Win32.Filesystem.FileInfo>
    {
        public FileCompare() { }

        public bool Equals(Alphaleonis.Win32.Filesystem.FileInfo f1, Alphaleonis.Win32.Filesystem.FileInfo f2)
        {
            return (f1.Name == f2.Name &&
                    f1.Length == f2.Length);
        }

        // Return a hash that reflects the comparison criteria. According to the   
        // rules for IEqualityComparer<T>, if Equals is true, then the hash codes must  
        // also be equal. Because equality as defined here is a simple value equality, not  
        // reference identity, it is possible that two or more objects will produce the same  
        // hash code.  
        public int GetHashCode(Alphaleonis.Win32.Filesystem.FileInfo fi)
        {
            string s = String.Format("{0}{1}", fi.Name, fi.Length);
            return s.GetHashCode();
        }
    }
}
