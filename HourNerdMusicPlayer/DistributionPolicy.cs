using System;
using System.IO;

namespace HourNerdMusicPlayer
{
    internal static class DistributionPolicy
    {
        internal const string PublicMarker = "public-distribution.flag";
        internal static string WorkingDirectory
        {
            get
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HourNerd Music Player", "Working");
                return IsPublicDistribution ? Path.Combine(root, "Public") : root;
            }
        }

        internal static void ConfigureWorkingDirectory()
        {
            Directory.CreateDirectory(WorkingDirectory);
            Environment.CurrentDirectory = WorkingDirectory;
        }
        internal static bool IsPublicDistribution
        {
            get { return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PublicMarker)); }
        }

        internal static string ProfileDirectory
        {
            get
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "HourNerd Music Player");
                return IsPublicDistribution ? Path.Combine(root, "Public") : root;
            }
        }
    }
}
