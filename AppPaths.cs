using System;
using System.IO;

namespace ClockEnforcer
{
    internal static class AppPaths
    {
        public static string BaseFolder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ClockEnforcer");

        static AppPaths()
        {
            Directory.CreateDirectory(BaseFolder);
        }

        public static string GetPath(string fileName)
        {
            return Path.Combine(BaseFolder, fileName);
        }
    }
}
