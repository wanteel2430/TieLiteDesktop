using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace TieLiteDesktop.Core
{
    /// <summary>Crops and scales user images to the standard 1920 x 1080 canvas.</summary>
    public static class ImageNormalizer
    {
        public const int Width = 1920;
        public const int Height = 1080;

        public static void Normalize(string source, string destination)
        {
            using var image = Image.FromFile(source);
            var scale = Math.Max(Width / (float)image.Width, Height / (float)image.Height);
            var drawWidth = (int)Math.Ceiling(image.Width * scale);
            var drawHeight = (int)Math.Ceiling(image.Height * scale);
            var offsetX = (Width - drawWidth) / 2;
            var offsetY = (Height - drawHeight) / 2;
            using var bitmap = new Bitmap(Width, Height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(image, new Rectangle(offsetX, offsetY, drawWidth, drawHeight));
            }
            var directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            bitmap.Save(destination, ImageFormat.Png);
        }
    }
}
