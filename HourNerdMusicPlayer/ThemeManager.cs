using System;
using System.Collections;
using System.Linq;
using System.Windows;

namespace HourNerdMusicPlayer
{
    public static class ThemeManager
    {
        public static readonly string[] Names = { "Dark", "Light", "Midnight", "Forest" };
        public static string Current { get; private set; } = "Dark";

        public static string NormalizeName(string name)
        {
            return Names.FirstOrDefault(value => string.Equals(value, name,
                StringComparison.OrdinalIgnoreCase)) ?? "Dark";
        }

        public static void Apply(string name)
        {
            name = NormalizeName(name);
            var application = Application.Current;
            if (application == null) return;
            if (!application.Dispatcher.CheckAccess())
            {
                application.Dispatcher.Invoke(new Action(() => Apply(name)));
                return;
            }
            new PaletteHelper().SetLightDark(name != "Light");
            var palette = new ResourceDictionary
            {
                Source = new Uri("/HourNerdMusicPlayer;component/Themes/" + name + ".xaml", UriKind.Relative)
            };
            // Local application entries override Material Design's merged base theme.
            // DynamicResource bindings update existing controls without a restart.
            foreach (DictionaryEntry item in palette)
                application.Resources[item.Key] = item.Value;
            Current = name;
        }
    }
}
