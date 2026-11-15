using System;
using System.Windows;
using System.Windows.Media;

namespace TieLiteDesktop.Core
{
    /// <summary>Switches cards to a translucent frosted surface while a custom background is active.</summary>
    public static class BackgroundMaterial
    {
        public static void Apply()
        {
            var app = Application.Current;
            if (app == null) return;
            app.Resources.Remove("CardSurface");
            app.Resources.Remove("TextPrimary");
            app.Resources.Remove("TextSecondary");
            app.Resources.Remove("TextTertiary");
            if (string.IsNullOrWhiteSpace(Svc.Settings?.BackgroundImage)) return;
            var surface = app.TryFindResource("Surface") as SolidColorBrush;
            if (surface == null) return;
            var color = surface.Color;
            var average = BackgroundSampler.Average(Svc.Settings.BackgroundImage);
            var cardColor = Color.FromRgb(
                (byte)(average.R * 0.25 + color.R * 0.75),
                (byte)(average.G * 0.25 + color.G * 0.75),
                (byte)(average.B * 0.25 + color.B * 0.75));
            var alpha = (byte)Math.Clamp((int)Math.Round(Svc.Settings.CardOpacity * 255), 80, 255);
            app.Resources["CardSurface"] = new SolidColorBrush(
                Color.FromArgb(alpha, cardColor.R, cardColor.G, cardColor.B));

            var luminance = Svc.EffectiveTheme == "dark" ? 0.2 : 0.8;
            if (luminance < 0.55)
            {
                app.Resources["TextPrimary"] = new SolidColorBrush(Color.FromRgb(0xF5, 0xF7, 0xFB));
                app.Resources["TextSecondary"] = new SolidColorBrush(Color.FromRgb(0xC2, 0xC9, 0xD6));
                app.Resources["TextTertiary"] = new SolidColorBrush(Color.FromRgb(0x98, 0xA2, 0xB3));
            }
            else
            {
                app.Resources["TextPrimary"] = new SolidColorBrush(Color.FromRgb(0x15, 0x1A, 0x22));
                app.Resources["TextSecondary"] = new SolidColorBrush(Color.FromRgb(0x46, 0x50, 0x5F));
                app.Resources["TextTertiary"] = new SolidColorBrush(Color.FromRgb(0x6B, 0x74, 0x84));
            }
        }
    }
}
