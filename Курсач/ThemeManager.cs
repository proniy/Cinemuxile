using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Курсач
{
    public static class ThemeManager
    {
        private static readonly string ThemesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes");
        private static readonly string ThemesFile = Path.Combine(ThemesPath, "themes.json");
        private static readonly string LastThemeFile = Path.Combine(ThemesPath, "last_theme.json");

        static ThemeManager()
        {
            if (!Directory.Exists(ThemesPath)) Directory.CreateDirectory(ThemesPath);
        }

        public static List<Theme> LoadThemes()
        {
            if (!File.Exists(ThemesFile)) return new List<Theme>();
            var json = File.ReadAllText(ThemesFile);
            return JsonConvert.DeserializeObject<List<Theme>>(json) ?? new List<Theme>();
        }

        public static void SaveThemes(List<Theme> themes)
        {
            var json = JsonConvert.SerializeObject(themes, Formatting.Indented);
            File.WriteAllText(ThemesFile, json);
        }
        public static void SaveCurrentTheme(Theme theme)
        {
            var json = JsonConvert.SerializeObject(theme, Formatting.Indented);
            File.WriteAllText(LastThemeFile, json);
        }

        public static Theme LoadCurrentTheme()
        {
            if (!File.Exists(LastThemeFile)) return null;
            var json = File.ReadAllText(LastThemeFile);
            return JsonConvert.DeserializeObject<Theme>(json);
        }
    }
}