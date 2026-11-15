using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TieLiteDesktop.Core
{
    public sealed class HistoryEntry
    {
        public long Id { get; set; }
        public string Title { get; set; } = "";
        public string ForumName { get; set; } = "";
        public long Time { get; set; }
    }

    /// <summary>Local browsing history (most recent first, max 100).</summary>
    public static class HistoryStore
    {
        private static readonly object Gate = new();
        private static List<HistoryEntry> _cache;

        private static string FilePath => Path.Combine(AppSettings.ConfigDirectory, "history.json");

        public static void Add(long id, string title, string forumName)
        {
            if (id == 0) return;
            lock (Gate)
            {
                var list = Load();
                list.RemoveAll(e => e.Id == id);
                list.Insert(0, new HistoryEntry
                {
                    Id = id,
                    Title = title ?? "",
                    ForumName = forumName ?? "",
                    Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                });
                if (list.Count > 100) list.RemoveRange(100, list.Count - 100);
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
        }

        public static List<HistoryEntry> All()
        {
            lock (Gate) return Load();
        }

        public static void Clear()
        {
            lock (Gate)
            {
                _cache = new List<HistoryEntry>();
                try
                {
                    if (File.Exists(FilePath)) File.Delete(FilePath);
                }
                catch
                {
                }
            }
        }

        private static List<HistoryEntry> Load()
        {
            if (_cache != null) return _cache;
            try
            {
                if (File.Exists(FilePath))
                {
                    _cache = JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(FilePath)) ??
                             new List<HistoryEntry>();
                }
            }
            catch
            {
                _cache = new List<HistoryEntry>();
            }
            return _cache ??= new List<HistoryEntry>();
        }
    }
}
