using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TieLiteDesktop.Core
{
    public sealed class RecyclePost
    {
        public long ThreadId { get; set; }
        public long ForumId { get; set; }
        public string ForumName { get; set; } = "";
        public string Title { get; set; } = "";
        public long DeletedAt { get; set; }
    }

    /// <summary>Local list of threads deleted through this client, so they can be restored.</summary>
    public static class RecycleStore
    {
        private static readonly string FilePath =
            Path.Combine(AppSettings.ConfigDirectory, "recycle-bin.json");

        public static List<RecyclePost> All()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<RecyclePost>();
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<RecyclePost>>(json) ?? new List<RecyclePost>();
            }
            catch
            {
                return new List<RecyclePost>();
            }
        }

        public static void Add(RecyclePost post)
        {
            if (post == null || post.ThreadId == 0) return;
            var list = All();
            list.RemoveAll(item => item.ThreadId == post.ThreadId);
            list.Insert(0, post);
            Save(list.Take(100).ToList());
        }

        public static void Remove(long threadId)
        {
            var list = All();
            list.RemoveAll(item => item.ThreadId == threadId);
            Save(list);
        }

        private static void Save(List<RecyclePost> list)
        {
            try
            {
                Directory.CreateDirectory(AppSettings.ConfigDirectory);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(list,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
            }
        }
    }
}
