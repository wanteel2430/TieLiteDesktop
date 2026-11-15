using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TieLiteDesktop.Core
{
    public static class BackgroundSampler
    {
        public static Color Average(string path)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.DecodePixelWidth = 64;
                image.EndInit();
                image.Freeze();
                var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
                var width = converted.PixelWidth;
                var height = converted.PixelHeight;
                var stride = width * 4;
                var pixels = new byte[stride * height];
                converted.CopyPixels(pixels, stride, 0);
                long r = 0, g = 0, b = 0;
                var count = 0;
                for (var y = 0; y < height; y += 2)
                {
                    for (var x = 0; x < width; x += 2)
                    {
                        var index = y * stride + x * 4;
                        b += pixels[index];
                        g += pixels[index + 1];
                        r += pixels[index + 2];
                        count++;
                    }
                }
                if (count == 0) return Color.FromRgb(0x12, 0x14, 0x18);
                return Color.FromRgb((byte)(r / count), (byte)(g / count), (byte)(b / count));
            }
            catch
            {
                return Color.FromRgb(0x12, 0x14, 0x18);
            }
        }
    }
}
