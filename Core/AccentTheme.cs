using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace TieLiteDesktop.Core
{
    /// <summary>Accent palettes inspired by FluxTie's theme color options.</summary>
    public static class AccentTheme
    {
        private static readonly Dictionary<string, Color> Colors = new(StringComparer.OrdinalIgnoreCase)
        {
            ["blue"] = Color.FromRgb(0x44, 0x77, 0xE0),
            ["green"] = Color.FromRgb(0x2F, 0xA3, 0x6B),
            ["purple"] = Color.FromRgb(0x7C, 0x5C, 0xE0),
            ["orange"] = Color.FromRgb(0xE0, 0x8A, 0x2F),
            ["pink"] = Color.FromRgb(0xE0, 0x5C, 0x8A),
            ["red"] = Color.FromRgb(0xD9, 0x53, 0x4F),
            ["cyan"] = Color.FromRgb(0x1F, 0xA8, 0xB8),
        };

        public static Color GetColor(string accent, string custom = null) =>
            accent == "custom" && TryParse(custom, out var parsed) ? parsed : Colors[Normalize(accent)];

        private static bool TryParse(string hex, out Color color)
        {
            color = Colors["blue"];
            if (string.IsNullOrWhiteSpace(hex)) return false;
            try
            {
                var value = hex.Trim().TrimStart('#');
                if (value.Length != 6) return false;
                color = Color.FromRgb(
                    Convert.ToByte(value.Substring(0, 2), 16),
                    Convert.ToByte(value.Substring(2, 2), 16),
                    Convert.ToByte(value.Substring(4, 2), 16));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string Normalize(string accent) =>
            !string.IsNullOrWhiteSpace(accent) && Colors.ContainsKey(accent) ? accent : "blue";

        public static void Apply(string accent, string custom = null)
        {
            var app = Application.Current;
            if (app == null) return;
            var color = GetColor(accent, custom);
            var soft = Color.FromArgb(0x26, color.R, color.G, color.B);
            var deep = Color.FromRgb(
                (byte)(color.R * 0.78),
                (byte)(color.G * 0.78),
                (byte)(color.B * 0.78));
            var deeper = Color.FromRgb(
                (byte)(color.R * 0.55),
                (byte)(color.G * 0.55),
                (byte)(color.B * 0.55));
            app.Resources["Accent"] = new SolidColorBrush(color);
            app.Resources["AccentSoft"] = new SolidColorBrush(soft);
            app.Resources["AccentGradient"] = new LinearGradientBrush(color, deep, 35);
            app.Resources["HeroGradient"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(color, 0),
                    new GradientStop(deep, 0.55),
                    new GradientStop(deeper, 1),
                },
            };
        }
    }
}
