using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TieLiteDesktop.Core
{
    /// <summary>Recent search keywords (max 20).</summary>
    public static class SearchHistoryStore
    {
        private static readonly object Gate = new();
        private static List<string> _cache;

        private static string FilePath => Path.Combine(AppSettings.ConfigDirectory, "search-history.json");

        public static void Add(string keyword)
        {
            keyword = (keyword ?? "").Trim();
            if (keyword.Length == 0) return;
            lock (Gate)
            {
                var list = Load();
                list.RemoveAll(k => string.Equals(k, keyword, StringComparison.OrdinalIgnoreCase));
                list.Insert(0, keyword);
                if (list.Count > 20) list.RemoveRange(20, list.Count - 20);
                Save(list);
            }
        }

        public static void Remove(string keyword)
        {
            lock (Gate)
            {
                var list = Load();
                list.RemoveAll(k => string.Equals(k, keyword, StringComparison.OrdinalIgnoreCase));
                Save(list);
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                _cache = new List<string>();
                try
                {
                    if (File.Exists(FilePath)) File.Delete(FilePath);
                }
                catch
                {
                }
            }
        }

        public static List<string> All()
        {
            lock (Gate) return new List<string>(Load());
        }

        private static void Save(List<string> list)
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

        private static List<string> Load()
        {
            if (_cache != null) return _cache;
            try
            {
                if (File.Exists(FilePath))
                {
                    _cache = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FilePath)) ?? new List<string>();
                }
            }
            catch
            {
                _cache = new List<string>();
            }
            return _cache ??= new List<string>();
        }
    }
}
