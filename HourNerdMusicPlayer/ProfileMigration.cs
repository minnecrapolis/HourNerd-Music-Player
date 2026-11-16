using System.IO;
using System.Text;

namespace HourNerdMusicPlayer
{
    internal static class ProfileMigration
    {
        internal static bool CopyLegacyProfile(string oldDirectory, string newDirectory,
            string oldName, string newName)
        {
            string destination = Path.Combine(newDirectory, newName);
            string source = Path.Combine(oldDirectory, oldName);
            if (File.Exists(destination) || !File.Exists(source)) return false;
            string settings = File.ReadAllText(source, Encoding.Unicode);
            // Keep legacy product names here to read the prior working profile.
            settings = settings.Replace("Elpis_", "HourNerdMusicPlayer_");
            settings = settings.Replace(oldDirectory, newDirectory);
            Directory.CreateDirectory(newDirectory);
            using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream, Encoding.Unicode))
                writer.Write(settings);
            return true;
        }
    }
}
