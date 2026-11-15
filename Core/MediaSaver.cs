using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace TieLiteDesktop.Core
{
    /// <summary>Saves a remote (or cached local) image/video to a user-chosen file.</summary>
    public static class MediaSaver
    {
        public static async Task<string> SaveAsync(string url, string defaultName, string filter)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var dialog = new SaveFileDialog
            {
                FileName = Sanitize(defaultName),
                Filter = filter,
                AddExtension = true,
            };
            if (dialog.ShowDialog() != true) return null;

            if (File.Exists(url))
            {
                File.Copy(url, dialog.FileName, true);
                return dialog.FileName;
            }

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://tieba.baidu.com/");
            var bytes = await client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            return dialog.FileName;
        }

        private static string Sanitize(string name)
        {
            var value = string.IsNullOrWhiteSpace(name) ? "fluxie-media" : name;
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }
            return value;
        }
    }
}
