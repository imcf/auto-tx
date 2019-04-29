using System;
using System.IO;
using NLog;

namespace ATxCommon
{
    public class DirectoryDetails
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private long _size = -1;
        private int _age = -1;

        /// <summary>
        /// Initialize the DirectoryDetails object from the given DirectoryInfo.
        /// </summary>
        /// <param name="dirInfo">The DirectoryInfo object of the directory to investigate.</param>
        public DirectoryDetails(DirectoryInfo dirInfo) {
            Dir = dirInfo;
        }

        /// <summary>
        /// The underlying DirectoryInfo object.
        /// </summary>
        public DirectoryInfo Dir { get; }

        /// <summary>
        /// The age defined by the directory's NAME in days.
        /// </summary>
        public int AgeFromName {
            get {
                if (_age < 0)
                    _age = FsUtils.DirNameToAge(Dir, DateTime.Now);

                return _age;
            }
        }

        /// <summary>
        /// The full size of the directory tree in bytes.
        /// </summary>
        public long Size {
            get {
                if (_size >= 0)
                    return _size;

                try {
                    _size = FsUtils.GetDirectorySize(Dir.FullName);
                }
                catch (Exception ex) {
                    Log.Error("ERROR getting directory size of [{0}]: {1}",
                        Dir.FullName, ex.Message);
                }

                return _size;
            }
        }

        /// <summary>
        /// Human friendly description of the directory's age, derived from its NAME.
        /// </summary>
        public string HumanAgeFromName => TimeUtils.DaysToHuman(AgeFromName, false);

        /// <summary>
        /// Human friendly description of the directory tree size (recursively).
        /// </summary>
        public string HumanSize => Conv.BytesToString(Size);
    }
}
