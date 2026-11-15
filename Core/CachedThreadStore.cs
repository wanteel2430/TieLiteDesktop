using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Core
{
    public sealed class CachedSegment
    {
        public string Kind { get; set; } = "Text";
        public string Text { get; set; } = "";
        public string Url { get; set; } = "";
        public string ThumbUrl { get; set; } = "";
        public string LocalImage { get; set; } = "";
    }

    public sealed class CachedPost
    {
        public long Id { get; set; }
        public int Floor { get; set; }
        public string Author { get; set; } = "";
        public string Portrait { get; set; } = "";
        public long Time { get; set; }
        public string Ip { get; set; } = "";
        public List<CachedSegment> Segments { get; } = new();
    }

    public sealed class CachedThread
    {
        public long Id { get; set; }
        public string Title { get; set; } = "";
        public string ForumName { get; set; } = "";
        public long SavedAt { get; set; }
        public string Key { get; set; } = "";
        public bool OnlyOp { get; set; }
        public int MaxPages { get; set; }
        public List<CachedPost> Posts { get; } = new();
    }

    /// <summary>Locally cached thread snapshots (max 30).</summary>
    public static class CachedThreadStore
    {
        private static readonly object Gate = new();
        private static List<CachedThread> _cache;
        private static readonly HashSet<long> Saving = new();

        private static string FilePath
        {
            get
            {
                var custom = Svc.Settings?.PostCacheDir;
                if (!string.IsNullOrWhiteSpace(custom) && Directory.Exists(custom))
                {
                    return Path.Combine(custom, "cached-threads.json");
                }
                return Path.Combine(AppSettings.ConfigDirectory, "cached-threads.json");
            }
        }

        public static void Reload() => _cache = null;

        public static string CacheDirectory => Path.GetDirectoryName(FilePath) ?? AppSettings.ConfigDirectory;

        public static IReadOnlyList<CachedThread> All()
        {
            lock (Gate)
            {
                var list = Load();
                foreach (var item in list)
                {
                    if (string.IsNullOrWhiteSpace(item.Key))
                    {
                        item.Key = BuildKey(item.Id, item.OnlyOp, item.MaxPages);
                    }
                }
                var deduped = list.GroupBy(t => t.Key)
                    .Select(group => group.OrderByDescending(t => t.SavedAt).First())
                    .OrderByDescending(t => t.SavedAt)
                    .ToList();
                if (deduped.Count != list.Count)
                {
                    Persist(deduped);
                }
                return deduped;
            }
        }

        public static CachedThread Get(long id)
        {
            lock (Gate) return Load().FirstOrDefault(t => t.Id == id);
        }

        public static string BuildKey(long id, bool onlyOp, int maxPages) =>
            id.ToString() + "|" + (onlyOp ? "1" : "0") + "|" + Math.Max(0, maxPages);

        public static async Task SaveAsync(ThreadPage page, bool onlyOp, int maxPages,
            IProgress<double> progress = null)
        {
            if (page?.Thread == null || page.Thread.Id == 0) return;
            lock (Saving)
            {
                if (!Saving.Add(page.Thread.Id)) return;
            }
            try
            {
            var key = BuildKey(page.Thread.Id, onlyOp, maxPages);
            var list = Load();
            list.RemoveAll(t => t.Key == key);
                var cached = new CachedThread
                {
                    Id = page.Thread.Id,
                    Title = page.Thread.Title,
                    ForumName = page.Thread.ForumName,
                    SavedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Key = key,
                    OnlyOp = onlyOp,
                    MaxPages = maxPages,
                };
                foreach (var post in page.Posts)
                {
                    var item = new CachedPost
                    {
                        Id = post.Id,
                        Floor = post.Floor,
                        Author = post.Author?.DisplayName ?? "",
                        Portrait = post.Author?.AvatarUrl ?? "",
                        Time = post.Time,
                        Ip = post.IpLocation,
                    };
                    foreach (var segment in post.Content)
                    {
                        if (segment.Kind == ContentKind.Image && string.IsNullOrWhiteSpace(segment.Url) &&
                            string.IsNullOrWhiteSpace(segment.ThumbUrl))
                        {
                            continue;
                        }
                        var cachedSegment = new CachedSegment
                        {
                            Kind = segment.Kind.ToString(),
                            Text = segment.Text ?? "",
                            Url = segment.Url ?? "",
                            ThumbUrl = segment.ThumbUrl ?? "",
                        };
                        if (segment.Kind == ContentKind.Image)
                        {
                            cachedSegment.LocalImage = await DownloadImageAsync(segment.Url, segment.ThumbUrl);
                        }
                        item.Segments.Add(cachedSegment);
                    }
                    cached.Posts.Add(item);
                }
                lock (Gate)
                {
                    list.Insert(0, cached);
                    Persist(list);
                }
                progress?.Report(1);
            }
            finally
            {
                lock (Saving)
                {
                    Saving.Remove(page.Thread.Id);
                }
            }
        }

        private static async Task<string> DownloadImageAsync(string url, string thumb)
        {
            var source = string.IsNullOrWhiteSpace(url) ? thumb : url;
            if (string.IsNullOrWhiteSpace(source)) return "";
            try
            {
                var directory = Path.Combine(CacheDirectory, "images");
                Directory.CreateDirectory(directory);
                var extension = ".img";
                try
                {
                    var uriPath = new Uri(source).AbsolutePath;
                    var dot = uriPath.LastIndexOf('.');
                    if (dot >= 0 && uriPath.Length - dot <= 6) extension = uriPath[dot..];
                }
                catch
                {
                }
                var path = Path.Combine(directory, DeviceProfile.Md5Hex(source) + extension);
                if (File.Exists(path) && new FileInfo(path).Length > 512) return path;
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/135 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://tieba.baidu.com/");
                var bytes = await client.GetByteArrayAsync(source).ConfigureAwait(false);
                await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
                return path;
            }
            catch
            {
                return "";
            }
        }

        public static void Remove(string key)
        {
            lock (Gate)
            {
                var list = Load();
                list.RemoveAll(t => t.Key == key);
                Persist(list);
            }
        }

        private static void Persist(List<CachedThread> list)
        {
            _cache = list;
            try
            {
                Directory.CreateDirectory(AppSettings.ConfigDirectory);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(list));
            }
            catch
            {
            }
        }

        private static List<CachedThread> Load()
        {
            if (_cache != null) return _cache;
            try
            {
                if (File.Exists(FilePath))
                {
                    _cache = JsonSerializer.Deserialize<List<CachedThread>>(File.ReadAllText(FilePath)) ??
                             new List<CachedThread>();
                }
            }
            catch
            {
                _cache = new List<CachedThread>();
            }
            return _cache ??= new List<CachedThread>();
        }
    }
}
