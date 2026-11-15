using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TieLiteDesktop.Core
{
    /// <summary>Downloads, caches (memory + disk) and decodes Tieba images for WPF.</summary>
    public static class ImageLoader
    {
        private static readonly ConcurrentDictionary<string, BitmapImage> Memory = new();
        private static readonly ConcurrentDictionary<string, Task<BitmapImage>> Inflight = new();
        private static readonly HttpClient Http = CreateClient();
        private static readonly SemaphoreSlim Gate = new(6);

        public static void ClearCache()
        {
            try
            {
                if (!Directory.Exists(CacheDirectory)) return;
                foreach (var file in Directory.GetFiles(CacheDirectory))
                {
                    try { File.Delete(file); } catch { }
                }
            }
            catch
            {
            }
        }

        public static string CacheDirectory
    {
        get
        {
            var custom = Svc.Settings?.ImageCacheDir;
            if (!string.IsNullOrWhiteSpace(custom) && Directory.Exists(custom)) return custom;
            return Path.Combine(AppSettings.ConfigDirectory, "images");
        }
    }

        private static HttpClient CreateClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                UseCookies = false,
            };
            if (Svc.Settings != null && Svc.Settings.ProxyMode == "none") handler.UseProxy = false;
            if (Svc.Settings != null && Svc.Settings.ProxyMode == "custom" &&
                Uri.TryCreate(Svc.Settings.ProxyUrl, UriKind.Absolute, out var proxy))
            {
                handler.Proxy = new System.Net.WebProxy(proxy);
            }
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://tieba.baidu.com/");
            return client;
        }

        public static string Normalize(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            if (url.StartsWith("//", StringComparison.Ordinal)) return "https:" + url;
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return "https://" + url[7..];
            return url;
        }

        public static async Task<BitmapImage> GetAsync(string url, int decodeWidth = 0, CancellationToken ct = default)
        {
            url = Normalize(url);
            if (string.IsNullOrEmpty(url)) return null;
            try
            {
                if (File.Exists(url)) return Decode(File.ReadAllBytes(url), decodeWidth);
            }
            catch
            {
            }
            var key = decodeWidth > 0 ? url + "#" + decodeWidth : url;
            if (Memory.TryGetValue(key, out var cached)) return cached;

            var task = Inflight.GetOrAdd(key, _ => DownloadAsync(url, decodeWidth, ct));
            try
            {
                var image = await task.ConfigureAwait(false);
                if (image != null) Memory[key] = image;
                return image;
            }
            finally
            {
                Inflight.TryRemove(key, out _);
            }
        }

        private static async Task<BitmapImage> DownloadAsync(string url, int decodeWidth, CancellationToken ct)
        {
            byte[] bytes = null;
            var cachePath = Path.Combine(CacheDirectory, DeviceProfile.Md5Hex(url) + ".img");
            try
            {
                if (File.Exists(cachePath))
                {
                    bytes = await File.ReadAllBytesAsync(cachePath, ct).ConfigureAwait(false);
                }
            }
            catch
            {
                bytes = null;
            }

            if (bytes == null || bytes.Length == 0)
            {
                await Gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    try
                    {
                        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct)
                            .ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();
                        bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                    }
                    catch when (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        // many legacy Tieba image hosts answer on plain http only
                        using var response = await Http.GetAsync("http://" + url.Substring(8),
                            HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();
                        bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                    }
                    try
                    {
                        Directory.CreateDirectory(CacheDirectory);
                        await File.WriteAllBytesAsync(cachePath, bytes, ct).ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }
                finally
                {
                    Gate.Release();
                }
            }

            return Decode(bytes, decodeWidth);
        }

        public static BitmapImage Decode(byte[] bytes, int decodeWidth)
        {
            try
            {
                using var ms = new MemoryStream(bytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                if (decodeWidth > 0) image.DecodePixelWidth = decodeWidth;
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        public static BitmapImage FromFile(string path, int decodeWidth = 0)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                if (decodeWidth > 0) image.DecodePixelWidth = decodeWidth;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }
    }
}
