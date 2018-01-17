using System.IO;
using System.Linq;

namespace ATXCommon
{
    public static class FsUtils
    {
        /// <summary>
        /// Recursively sum up size of all files under a given path.
        /// </summary>
        /// <param name="path">Full path of the directory.</param>
        /// <returns>The total size in bytes.</returns>
        public static long GetDirectorySize(string path) {
            return new DirectoryInfo(path)
                .GetFiles("*", SearchOption.AllDirectories)
                .Sum(file => file.Length);
        }
    }
}
