using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TieLiteDesktop.Core
{
    public sealed class ReadingPosition
    {
        public int Page { get; set; } = 1;
        public double Offset { get; set; }
        public long SavedAt { get; set; }
    }

    /// <summary>Remembers the last page/scroll offset of each thread.</summary>
    public static class ReadingPositionStore
    {
        private static readonly object Gate = new();
        private static Dictionary<long, ReadingPosition> _cache;

        private static string FilePath => Path.Combine(AppSettings.ConfigDirectory, "reading-positions.json");

        public static ReadingPosition Get(long threadId)
        {
            lock (Gate)
            {
                return Load().TryGetValue(threadId, out var value) ? value : null;
            }
        }

        public static void Save(long threadId, int page, double offset)
        {
            if (threadId <= 0) return;
            lock (Gate)
            {
                var map = Load();
                map[threadId] = new ReadingPosition
                {
                    Page = Math.Max(1, page),
                    Offset = Math.Max(0, offset),
                    SavedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                };
                if (map.Count > 200)
                {
                    var oldest = new List<KeyValuePair<long, ReadingPosition>>(map);
                    oldest.Sort((a, b) => a.Value.SavedAt.CompareTo(b.Value.SavedAt));
                    for (var i = 0; i < oldest.Count - 200; i++) map.Remove(oldest[i].Key);
                }
                try
                {
                    Directory.CreateDirectory(AppSettings.ConfigDirectory);
                    File.WriteAllText(FilePath, JsonSerializer.Serialize(map));
                }
                catch
                {
                }
            }
        }

        private static Dictionary<long, ReadingPosition> Load()
        {
            if (_cache != null) return _cache;
            try
            {
                if (File.Exists(FilePath))
                {
                    _cache = JsonSerializer.Deserialize<Dictionary<long, ReadingPosition>>(File.ReadAllText(FilePath)) ??
                             new Dictionary<long, ReadingPosition>();
                }
            }
            catch
            {
                _cache = new Dictionary<long, ReadingPosition>();
            }
            return _cache ??= new Dictionary<long, ReadingPosition>();
        }
    }
}
