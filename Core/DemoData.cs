using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TieLiteDesktop.Core.Api;
using TieLiteDesktop.Core.Models;
using TieLiteDesktop.Core.Proto;

namespace TieLiteDesktop.Core
{
    /// <summary>Offline fixtures so the UI can be developed and demoed without network access.</summary>
    public static class DemoData
    {
        private static ProtoMessage Load(string name, string type)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
            if (!File.Exists(path)) throw new FileNotFoundException("缺少示例数据：" + name);
            return ProtoCodec.Decode(Svc.Schema, type, File.ReadAllBytes(path));
        }

        public static HotPage Hot() => TiebaApi.ParseHot(Load("hot.bin", "tieba.hotThreadList.HotThreadListResponse"));

        public static ForumPage Forum() => TiebaApi.ParseForum(Load("frs.bin", "tieba.frsPage.FrsPageResponse"));

        public static ThreadPage Thread() => TiebaApi.ParseThread(Load("pb.bin", "tieba.pbPage.PbPageResponse"));

        public static (ThreadPage page, PollInfoModel poll) PollDemo()
        {
            var page = TiebaApi.ParseThread(Load("poll.bin", "tieba.pbPage.PbPageResponse"));
            page.Thread.Title = "大伙，7.x版本是否会重现4.x顶上战争";
            var poll = new PollInfoModel
            {
                Title = "大伙，7.x版本是否会重现4.x顶上战争",
                TotalNum = 1286,
                IsPolled = false,
            };
            poll.Options.Add(new PollOption { Id = 1, Text = "会，4.x的盛况还会再来", Num = 612 });
            poll.Options.Add(new PollOption { Id = 2, Text = "不会，时代已经变了", Num = 431 });
            poll.Options.Add(new PollOption { Id = 3, Text = "看后续版本再说", Num = 243 });
            return (page, poll);
        }

        public static List<RecommendedForum> Forums() => new()
        {
            new RecommendedForum { Id = 1, Name = "孙笑川" },
            new RecommendedForum { Id = 2, Name = "原神" },
            new RecommendedForum { Id = 3, Name = "steam" },
            new RecommendedForum { Id = 4, Name = "武汉大学" },
            new RecommendedForum { Id = 5, Name = "图拉丁" },
            new RecommendedForum { Id = 6, Name = "显卡" },
            new RecommendedForum { Id = 7, Name = "抗压背锅" },
            new RecommendedForum { Id = 8, Name = "李毅" },
            new RecommendedForum { Id = 9, Name = "手机" },
            new RecommendedForum { Id = 10, Name = "动漫" },
        };

        public static List<HotTopic> Topics() => new()
        {
            new HotTopic { Id = 28369804, Name = "飞雪吧替厉娜把超女物料存了20年", DiscussNum = 2185770 },
            new HotTopic { Id = 28369805, Name = "吧友连续14年盖楼支持诺基亚", DiscussNum = 1666398 },
            new HotTopic { Id = 28369806, Name = "LCK选手AI性转视频走红韩网", DiscussNum = 1329664 },
            new HotTopic { Id = 28369807, Name = "飞雪吧图册都是吧主自己整理的", DiscussNum = 1148634 },
            new HotTopic { Id = 28369808, Name = "盘点Bin古早发言吧友来打分", DiscussNum = 1081340 },
            new HotTopic { Id = 28369809, Name = "大学宿舍关系能有多微妙", DiscussNum = 946120 },
        };
    }

    /// <summary>Switches between the live API and the bundled fixtures.</summary>
    public static class Data
    {
        public static Task<HotPage> HotAsync(string tabCode = null) =>
            Svc.DemoMode
                ? Task.FromResult(DemoData.Hot())
                : string.IsNullOrEmpty(tabCode)
                    ? Svc.Api.GetHotFeedAsync(40)
                    : Svc.Api.GetHotAsync(tabCode);

        public static Task<ForumPage> ForumAsync(string forum, int page = 1, int sortType = 0) =>
            Svc.DemoMode ? Task.FromResult(DemoData.Forum()) : Svc.Api.GetForumAsync(forum, page, sortType);

        public static Task<ThreadPage> ThreadAsync(long id, int page = 1, bool seeLz = false, bool reverse = false) =>
            Svc.DemoMode ? Task.FromResult(DemoData.Thread()) : Svc.Api.GetThreadAsync(id, page, seeLz, reverse);

        public static Task<List<UserInfo>> SearchUsersAsync(string query, int page = 1) =>
            Svc.DemoMode ? Task.FromResult(new List<UserInfo>()) : Svc.Api.SearchUsersAsync(query, page);

        public static Task<List<SearchPostResult>> SearchForumAsync(string forumName, string query, int page = 1) =>
            Svc.DemoMode ? SearchThreadsAsync(query, page, forumName) : Svc.Api.SearchForumAsync(forumName, query, page);

        public static Task<List<SearchPostResult>> SearchThreadsAsync(string query, int page = 1, string forumName = null)
        {
            if (!Svc.DemoMode) return Svc.Api.SearchThreadAsync(query, page, forumName);
            var forum = DemoData.Forum();
            var matched = forum.Threads
                .Where(t => (t.Title ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            (t.PreviewText ?? "").Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(t => new SearchPostResult
                {
                    ThreadId = t.Id,
                    Title = t.Title,
                    ForumName = forum.Forum.Name,
                    Content = t.PreviewText,
                    AuthorName = t.Author?.DisplayName,
                    Time = t.LastTimeInt,
                })
                .ToList();
            if (matched.Count == 0)
            {
                matched = forum.Threads.Take(5).Select(t => new SearchPostResult
                {
                    ThreadId = t.Id,
                    Title = t.Title,
                    ForumName = forum.Forum.Name,
                    Content = t.PreviewText,
                    AuthorName = t.Author?.DisplayName,
                    Time = t.LastTimeInt,
                }).ToList();
            }
            return Task.FromResult(matched);
        }

        public static Task<List<SearchForumResult>> SearchForumsAsync(string query)
        {
            if (!Svc.DemoMode) return Svc.Api.SearchForumAsync(query);
            var forum = DemoData.Forum().Forum;
            return Task.FromResult(new List<SearchForumResult>
            {
                new()
                {
                    Id = forum.Id,
                    Name = forum.Name,
                    Desc = forum.Slogan,
                    MemberNum = forum.MemberNum,
                    ThreadNum = forum.ThreadNum,
                    Avatar = forum.AvatarUrl,
                },
            });
        }

        public static Task<List<RecommendedForum>> ForumsAsync() =>
            Svc.DemoMode ? Task.FromResult(DemoData.Forums()) : Svc.Api.GetRecommendedForumsAsync();

        public static Task<List<HotTopic>> TopicsAsync() =>
            Svc.DemoMode ? Task.FromResult(DemoData.Topics()) : Svc.Api.GetHotTopicsAsync();

        public static IEnumerable<ThreadSummary> Visible(IEnumerable<ThreadSummary> threads)
        {
            if (threads == null) return Enumerable.Empty<ThreadSummary>();
            var result = threads;
            if (Svc.Settings != null && Svc.Settings.HideVideoThreads)
            {
                result = result.Where(t => !t.HasVideo);
            }
            return result.Where(t => !BlockWords.Match(t.Title) && !BlockWords.Match(t.PreviewText));
        }

        public static Task<TopicPage> TopicAsync(long id, string name, int page = 1)
        {            if (!Svc.DemoMode) return Svc.Api.GetTopicDetailAsync(id, name, page);
            var topic = DemoData.Topics().FirstOrDefault(t => t.Id == id) ?? new HotTopic
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(name) ? "热门话题" : name,
            };
            var result = new TopicPage { Info = topic, HasMore = false };
            var forumName = topic.Name.Contains("吧", StringComparison.Ordinal)
                ? topic.Name[..topic.Name.IndexOf("吧", StringComparison.Ordinal)]
                : "热门话题";
            var demo = DemoData.Thread();
            for (var i = 0; i < 6; i++)
            {
                result.Threads.Add(new ThreadSummary
                {
                    Id = 9200000000000 + topic.Id * 10 + i,
                    Title = topic.Name + " · 讨论 " + (i + 1),
                    ForumName = forumName,
                    ReplyNum = 120 + i * 37,
                    AgreeNum = 300 + i * 52,
                    LastTimeInt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - i * 3600,
                    PreviewText = "关于「" + topic.Name + "」的讨论与观点，欢迎一起交流。",
                    Author = new UserInfo { Id = 1000 + i, ShowName = "吧友" + (i + 1) },
                });
            }
            return Task.FromResult(result);
        }
    }
}
