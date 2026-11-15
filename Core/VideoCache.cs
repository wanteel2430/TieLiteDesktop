using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TieLiteDesktop.Core
{
    /// <summary>Downloads Tieba videos to a local file (with the required Referer/UA) for MediaElement.</summary>
    public static class VideoCache
    {
        public static string CacheDirectory
        {
            get
            {
                var custom = Svc.Settings?.VideoCacheDir;
                if (!string.IsNullOrWhiteSpace(custom) && System.IO.Directory.Exists(custom)) return custom;
                return Path.Combine(AppSettings.ConfigDirectory, "videos");
            }
        }

        public static async Task<string> DownloadAsync(string url, IProgress<double> progress, CancellationToken ct)
        {
            var primary = CacheDirectory;
            try
            {
                return await DownloadToAsync(url, primary, progress, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // profile/custom folder can be locked by another process: fall back to %TEMP%
                var fallback = Path.Combine(Path.GetTempPath(), "FluxTieVideos");
                System.IO.Directory.CreateDirectory(fallback);
                return await DownloadToAsync(url, fallback, progress, ct).ConfigureAwait(false);
            }
        }

        private static async Task<string> DownloadToAsync(string url, string directory,
            IProgress<double> progress, CancellationToken ct)
        {
            var path = Path.Combine(directory, DeviceProfile.Md5Hex(url) + ".mp4");
            if (File.Exists(path) && new FileInfo(path).Length > 10240) return path;

            System.IO.Directory.CreateDirectory(directory);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Referer", "https://tieba.baidu.com/");
            using var response = await Svc.Client.Http.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1;
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".part";
            var received = 0L;
            await using (var output = File.Create(temp))
            await using (var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await input.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer, 0, read, ct).ConfigureAwait(false);
                    received += read;
                    if (total > 0) progress?.Report(Math.Clamp((double)received / total, 0, 1));
                }
            }

            try
            {
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                return path;
            }
            catch
            {
                return temp;
            }
        }
    }
}
