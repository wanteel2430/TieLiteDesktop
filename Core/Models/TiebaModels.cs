using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TieLiteDesktop.Core.Proto;

namespace TieLiteDesktop.Core.Models
{
    public enum ContentKind
    {
        Text,
        Link,
        Emoticon,
        At,
        Image,
        Video,
        Voice,
        Unknown,
    }

    public sealed class ContentSegment
    {
        public ContentKind Kind { get; set; } = ContentKind.Text;
        public string Text { get; set; } = "";
        public string Url { get; set; } = "";
        public string ThumbUrl { get; set; } = "";
        public string EmoticonId { get; set; } = "";
        public long Uid { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public sealed class UserInfo
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string ShowName { get; set; } = "";
        public string Portrait { get; set; } = "";
        public int Level { get; set; }
        public int Sex { get; set; }
        public bool IsBawu { get; set; }
        public string BawuType { get; set; } = "";
        public string Intro { get; set; } = "";
        public int FansNum { get; set; }
        public int FollowNum { get; set; }
        public double TbAge { get; set; }
        public int PostNum { get; set; }
        public int ThreadNum { get; set; }
        public int AgreeNum { get; set; }
        public string IpAddress { get; set; } = "";
        public string LevelName { get; set; } = "";
        public int Age { get; set; }
        public string TiebaUid { get; set; } = "";
        public bool HasConcerned { get; set; }

        public string DisplayName =>
            !string.IsNullOrWhiteSpace(Name) &&
            (string.IsNullOrWhiteSpace(ShowName) || ShowName.StartsWith("贴吧用户", StringComparison.Ordinal))
                ? Name
                : (!string.IsNullOrWhiteSpace(ShowName) ? ShowName : Name);

        public string AvatarUrl
        {
            get
            {
                if (string.IsNullOrEmpty(Portrait)) return "";
                if (Portrait.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    Portrait.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return Portrait;
                return "https://himg.bdimg.com/sys/portrait/item/" + Portrait;
            }
        }

        public static UserInfo From(ProtoMessage m)
        {
            if (m == null) return new UserInfo();
            return new UserInfo
            {
                Id = m.GetLong("id"),
                Name = m.GetString("name") ?? "",
                ShowName = m.GetString("nameShow") ?? m.GetString("show_name") ?? "",
                Portrait = m.GetString("portrait") ?? "",
                Level = m.GetInt("level_id"),
                Sex = m.GetInt("sex"),
                IsBawu = m.GetBool("is_bawu"),
                BawuType = m.GetString("bawu_type") ?? "",
                Intro = m.GetString("intro") ?? "",
                FansNum = m.GetInt("fans_num"),
                FollowNum = m.GetInt("concern_num"),
                PostNum = m.GetInt("post_num"),
                ThreadNum = m.GetInt("thread_num"),
                AgreeNum = Math.Max(m.GetInt("total_agree_num"), m.GetInt("agree_num")),
                TbAge = double.TryParse(m.GetString("tb_age"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var age) ? age : 0,
                IpAddress = m.GetString("ip_address") ?? m.GetString("ip") ?? m.GetString("location") ?? "",
                LevelName = m.GetString("level_name") ?? "",
                TiebaUid = m.GetString("tieba_uid") ?? "",
                HasConcerned = m.GetInt("has_concerned") == 1,
            };
        }

        public static UserInfo FromSimple(ProtoMessage m)
        {
            if (m == null) return new UserInfo();
            return new UserInfo
            {
                Id = m.GetLong("id"),
                Name = m.GetString("name") ?? "",
                ShowName = m.GetString("name") ?? "",
                Portrait = m.GetString("portrait") ?? "",
                Level = m.GetInt("level_id"),
            };
        }
    }

    public sealed class ManagerInfo
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string ShowName { get; set; } = "";
        public string Role { get; set; } = "";
    }

    public sealed class ForumRuleItem
    {
        public string Title { get; set; } = "";
        public string Text { get; set; } = "";
    }

    public sealed class ForumRuleDetail
    {
        public string Title { get; set; } = "";
        public string Preface { get; set; } = "";
        public string PublishTime { get; set; } = "";
        public List<ForumRuleItem> Rules { get; } = new();
    }

    public sealed class ForumInfo
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Avatar { get; set; } = "";
        public string Slogan { get; set; } = "";
        public string Description { get; set; } = "";
        public string HotText { get; set; } = "";
        public string RuleTitle { get; set; } = "";
        public long RuleThreadId { get; set; }
        public string FirstClass { get; set; } = "";
        public int MemberNum { get; set; }
        public int ThreadNum { get; set; }
        public int PostNum { get; set; }
        public bool IsLiked { get; set; }
        public int UserLevel { get; set; }
        public string LevelName { get; set; } = "";
        public int CurScore { get; set; }
        public int LevelupScore { get; set; }
        public bool IsSigned { get; set; }
        public int SignDayCount { get; set; }
        public int SignRank { get; set; }
        public int TotalSignCount { get; set; }
        public List<ManagerInfo> Managers { get; } = new();

        public string AvatarUrl
        {
            get
            {
                if (string.IsNullOrEmpty(Avatar)) return "";
                if (Avatar.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return Avatar;
                return "https://tiebapic.baidu.com/forum/" + Avatar;
            }
        }

        public static ForumInfo From(ProtoMessage m)
        {
            if (m == null) return new ForumInfo();
            var info = new ForumInfo
            {
                Id = m.GetLong("id"),
                Name = m.GetString("name") ?? "",
                Avatar = m.GetString("avatar") ?? "",
                Slogan = m.GetString("slogan") ?? "",
                FirstClass = m.GetString("first_class") ?? "",
                MemberNum = m.GetInt("member_num"),
                ThreadNum = m.GetInt("thread_num"),
                PostNum = m.GetInt("post_num"),
                IsLiked = m.GetBool("is_like"),
                UserLevel = m.GetInt("user_level"),
                LevelName = m.GetString("level_name") ?? "",
                CurScore = m.GetInt("cur_score"),
                LevelupScore = m.GetInt("levelup_score"),
            };
            var sign = m.GetMessage("sign_in_info");
            if (sign != null)
            {
                var signUser = sign.GetMessage("user_info");
                if (signUser != null)
                {
                    info.IsSigned = signUser.GetBool("is_sign_in");
                    info.SignDayCount = signUser.GetInt("cont_sign_num");
                    info.SignRank = signUser.GetInt("user_sign_rank");
                    info.TotalSignCount = signUser.GetInt("cout_total_sign_num");
                }
            }
            foreach (var manager in m.GetMessages("managers"))
            {
                info.Managers.Add(new ManagerInfo
                {
                    Id = manager.GetLong("id"),
                    Name = manager.GetString("name") ?? "",
                    ShowName = manager.GetString("show_name") ?? "",
                    Role = manager.GetString("role_name") ?? manager.GetString("role") ?? "",
                });
            }
            return info;
        }

        public static ForumInfo FromSimple(ProtoMessage m)
        {
            if (m == null) return new ForumInfo();
            return new ForumInfo
            {
                Id = m.GetLong("id"),
                Name = m.GetString("name") ?? "",
                Avatar = m.GetString("avatar") ?? "",
                MemberNum = m.GetInt("memberNum"),
                PostNum = m.GetInt("postNum"),
            };
        }
    }

    public sealed class PollOption
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
        public long Num { get; set; }
        public string Image { get; set; } = "";
    }

    public sealed class PollInfoModel
    {
        public string Title { get; set; } = "";
        public bool IsMulti { get; set; }
        public bool IsPolled { get; set; }
        public long TotalNum { get; set; }
        public int EndTime { get; set; }
        public string PolledValue { get; set; } = "";
        public List<PollOption> Options { get; } = new();

        public static PollInfoModel From(ProtoMessage m)
        {
            if (m == null) return null;
            var poll = new PollInfoModel
            {
                Title = m.GetString("title") ?? "",
                IsMulti = m.GetBool("is_multi"),
                IsPolled = m.GetBool("is_polled"),
                TotalNum = m.GetLong("total_num", m.GetLong("total_poll")),
                EndTime = m.GetInt("end_time"),
                PolledValue = m.GetString("polled_value") ?? "",
            };
            foreach (var option in m.GetMessages("options"))
            {
                poll.Options.Add(new PollOption
                {
                    Id = option.GetInt("id"),
                    Text = option.GetString("text") ?? "",
                    Num = option.GetLong("num"),
                    Image = option.GetString("image") ?? "",
                });
            }
            return poll.Options.Count > 0 ? poll : null;
        }
    }

    public sealed class VideoInfoModel
    {
        public string Url { get; set; } = "";
        public string PageUrl { get; set; } = "";
        public string Thumbnail { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public int Duration { get; set; }
        public string Title { get; set; } = "";

        public static VideoInfoModel From(ProtoMessage m)
        {
            if (m == null) return null;
            var video = new VideoInfoModel
            {
                Url = m.GetString("videoUrl") ?? "",
                Thumbnail = m.GetString("thumbnailUrl") ?? "",
                Width = m.GetInt("videoWidth"),
                Height = m.GetInt("videoHeight"),
                Duration = m.GetInt("videoDuration"),
            };
            return string.IsNullOrWhiteSpace(video.Url) && string.IsNullOrWhiteSpace(video.Thumbnail) ? null : video;
        }
    }

    public sealed class BawuMember
    {
        public long Uid { get; set; }
        public string Name { get; set; } = "";
        public string RoleName { get; set; } = "";
        public string Portrait { get; set; } = "";
        public int Level { get; set; }

        public string AvatarUrl => string.IsNullOrEmpty(Portrait)
            ? ""
            : Portrait.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? Portrait
                : "https://himg.bdimg.com/sys/portrait/item/" + Portrait;
    }

    public sealed class ThreadSummary
    {
        public long Id { get; set; }
        public string Title { get; set; } = "";
        public int ReplyNum { get; set; }
        public int ViewNum { get; set; }
        public int AgreeNum { get; set; }
        public int ShareNum { get; set; }
        public int HotNum { get; set; }
        public string LastTime { get; set; } = "";
        public long LastTimeInt { get; set; }
        public long CreateTime { get; set; }
        public bool IsTop { get; set; }
        public bool IsGood { get; set; }
        public bool IsHot { get; set; }
        public bool IsHidden { get; set; }
        public bool ShowVisibility { get; set; }
        public long ForumId { get; set; }
        public string ForumName { get; set; } = "";
        public long FirstPostId { get; set; }
        public long AuthorId { get; set; }
        public UserInfo Author { get; set; } = new();
        public UserInfo LastReplyer { get; set; }
        public string PreviewText { get; set; } = "";
        public string PreviewImage { get; set; } = "";
        public int PreviewWidth { get; set; }
        public int PreviewHeight { get; set; }
        public bool HasVideo { get; set; }
        public bool IsPoll { get; set; }
        public PollInfoModel Poll { get; set; }

        private static string FirstNonEmpty(params string[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

        private static (int width, int height) ParseSize(string bsize)
        {
            if (string.IsNullOrEmpty(bsize)) return (0, 0);
            var parts = bsize.Split(',');
            if (parts.Length != 2) return (0, 0);
            int.TryParse(parts[0], out var w);
            int.TryParse(parts[1], out var h);
            return (w, h);
        }

        public static ThreadSummary From(ProtoMessage m)
        {
            if (m == null) return new ThreadSummary();
            var thread = new ThreadSummary
            {
                Id = m.GetLong("id"),
                Title = (m.GetString("title") ?? "").Trim(),
                ReplyNum = m.GetInt("replyNum"),
                ViewNum = m.GetInt("viewNum"),
                AgreeNum = m.GetInt("agreeNum"),
                ShareNum = (int)m.GetLong("shareNum"),
                HotNum = m.GetInt("hotNum"),
                LastTime = m.GetString("lastTime") ?? "",
                LastTimeInt = m.GetLong("lastTimeInt"),
                CreateTime = m.GetLong("createTime"),
                IsTop = m.GetBool("isTop"),
                IsGood = m.GetBool("isGood"),
                ForumId = m.GetLong("forumId", m.GetLong("fid")),
                ForumName = m.GetString("forumName") ?? m.GetString("fname") ?? "",
                FirstPostId = m.GetLong("firstPostId"),
                AuthorId = m.GetLong("authorId", m.GetMessage("author")?.GetLong("id") ?? 0),
                Author = UserInfo.From(m.GetMessage("author")),
                LastReplyer = m.GetMessage("lastReplyer") != null ? UserInfo.From(m.GetMessage("lastReplyer")) : null,
                IsPoll = m.GetMessage("poll_info") != null,
                Poll = PollInfoModel.From(m.GetMessage("poll_info")),
            };

            var video = m.GetMessage("videoInfo") ?? m.GetMessage("video_info");
            if (video != null)
            {
                thread.HasVideo = true;
                if (string.IsNullOrEmpty(thread.PreviewImage))
                {
                    thread.PreviewImage = video.GetString("thumbnailUrl") ?? "";
                }
            }

            var text = new StringBuilder();
            foreach (var item in m.GetMessages("_abstract"))
            {
                var type = item.GetInt("type");
                if (type == 0 || type == 9 || type == 27 || type == 35 || type == 40)
                {
                    text.Append(item.GetString("text"));
                }
                else if (type == 3 && string.IsNullOrEmpty(thread.PreviewImage))
                {
                    thread.PreviewImage = item.GetString("src") ?? item.GetString("bigSrc") ?? "";
                }
                else if (type == 5 || type == 10)
                {
                    thread.HasVideo = true;
                }
            }
            foreach (var media in m.GetMessages("media"))
            {
                var mediaType = media.GetInt("type");
                if (mediaType == 3)
                {
                    if (thread.PreviewImage.Length == 0)
                    {
                        thread.PreviewImage = FirstNonEmpty(
                            media.GetString("dynamicPic"), media.GetString("bigPic"),
                            media.GetString("srcPic"), media.GetString("originPic"));
                        thread.PreviewWidth = media.GetInt("width");
                        thread.PreviewHeight = media.GetInt("height");
                    }
                    continue;
                }
                // video media: use its cover and mark the thread as a video thread
                if (mediaType == 5 || mediaType == 4 || mediaType == 9)
                {
                    thread.HasVideo = true;
                    if (thread.PreviewImage.Length == 0)
                    {
                        thread.PreviewImage = FirstNonEmpty(
                            media.GetString("dynamicPic"), media.GetString("bigPic"),
                            media.GetString("srcPic"), media.GetString("originPic"));
                        thread.PreviewWidth = media.GetInt("width");
                        thread.PreviewHeight = media.GetInt("height");
                    }
                }
            }
            if (thread.PreviewImage.Length == 0)
            {
                foreach (var item in m.GetMessages("firstPostContent"))
                {
                    if (item.GetInt("type") == 5)
                    {
                        thread.HasVideo = true;
                        if (thread.PreviewImage.Length == 0)
                        {
                            thread.PreviewImage = FirstNonEmpty(item.GetString("src"), item.GetString("cdnSrc"),
                                item.GetString("bigCdnSrc"), item.GetString("originSrc"));
                        }
                        continue;
                    }
                    if (item.GetInt("type") != 3 || thread.PreviewImage.Length > 0) continue;
                    thread.PreviewImage = FirstNonEmpty(item.GetString("cdnSrc"), item.GetString("bigCdnSrc"),
                        item.GetString("originSrc"), item.GetString("src"));
                    var (w, h) = ParseSize(item.GetString("bsize"));
                    thread.PreviewWidth = w;
                    thread.PreviewHeight = h;
                }
            }
            thread.PreviewText = text.ToString().Trim();
            if (thread.PreviewText.Length > 160) thread.PreviewText = thread.PreviewText[..160];
            return thread;
        }
    }

    public sealed class SubPost
    {
        public long Id { get; set; }
        public long AuthorId { get; set; }
        public UserInfo Author { get; set; } = new();
        public int Floor { get; set; }
        public long Time { get; set; }
        public List<ContentSegment> Content { get; } = new();
        public int AgreeNum { get; set; }
        public bool HasAgree { get; set; }
        public bool IsOp { get; set; }
    }

    public sealed class PostItem
    {
        public long Id { get; set; }
        public int Floor { get; set; }
        public long Time { get; set; }
        public long AuthorId { get; set; }
        public UserInfo Author { get; set; } = new();
        public bool IsOp { get; set; }
        public bool IsTop { get; set; }
        public int SubPostCount { get; set; }
        public int AgreeNum { get; set; }
        public bool HasAgree { get; set; }
        public int DisagreeNum { get; set; }
        public string Title { get; set; } = "";
        public List<ContentSegment> Content { get; } = new();
        public List<SubPost> SubPosts { get; } = new();
        public string IpLocation { get; set; } = "";
        public VideoInfoModel Video { get; set; }

        public bool HasImages => Content.Any(c => c.Kind == ContentKind.Image);

        public string PlainText => string.Join("", Content.Where(c => c.Kind == ContentKind.Text || c.Kind == ContentKind.At || c.Kind == ContentKind.Link).Select(c => c.Text));
    }

    public sealed class PageInfo
    {
        public int CurrentPage { get; set; }
        public int TotalPage { get; set; }
        public bool HasMore { get; set; }
        public int TotalNum { get; set; }
        public int PageSize { get; set; }

        public static PageInfo From(ProtoMessage m) => m == null
            ? new PageInfo()
            : new PageInfo
            {
                CurrentPage = m.GetInt("current_page"),
                TotalPage = m.GetInt("total_page"),
                HasMore = m.GetBool("has_more"),
                TotalNum = m.GetInt("total_num"),
                PageSize = m.GetInt("page_size"),
            };
    }

    public sealed class ForumTab
    {
        public int Id { get; set; }
        public int Type { get; set; }
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public bool IsGeneral { get; set; }
        public bool IsDefault { get; set; }
    }

    public sealed class ForumPage
    {
        public ForumInfo Forum { get; set; } = new();
        public List<ThreadSummary> Threads { get; } = new();
        public List<ForumTab> Tabs { get; } = new();
        public PageInfo Page { get; set; } = new();
        public UserInfo User { get; set; } = new();
        public bool HasMore => Page == null || Page.HasMore || Page.TotalPage > Page.CurrentPage;
    }

    public sealed class ThreadPage
    {
        public ThreadSummary Thread { get; set; } = new();
        public ForumInfo Forum { get; set; } = new();
        public PageInfo Page { get; set; } = new();
        public List<PostItem> Posts { get; } = new();
        public UserInfo CurrentUser { get; set; } = new();
        public bool IsOnlyOp { get; set; }
    }

    public sealed class HotPage
    {
        public List<ThreadSummary> Threads { get; } = new();
        public List<HotTopic> Topics { get; } = new();
        public List<ForumTab> Tabs { get; } = new();
    }

    public sealed class HotTopic
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public long DiscussNum { get; set; }
        public string Desc { get; set; } = "";
        public string Image { get; set; } = "";
        public long ThreadId { get; set; }
        public string UserName { get; set; } = "";

        public string WebUrl => Id > 0
            ? "https://tieba.baidu.com/hottopic/browse/hottopic?topic_id=" + Id
            : "https://tieba.baidu.com/";
    }

    public sealed class RecommendedForum
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Avatar { get; set; } = "";
        public long HotNum { get; set; }
        public int Level { get; set; }
        public bool IsSigned { get; set; }
        public int MemberNum { get; set; }
        public int ThreadNum { get; set; }
        public string HotText { get; set; } = "";
        public string AvatarUrl => string.IsNullOrWhiteSpace(Avatar)
            ? ""
            : Avatar.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? Avatar
                : "https://tiebapic.baidu.com/forum/" + Avatar;
    }

    public sealed class TopicPage
    {
        public HotTopic Info { get; set; } = new();
        public List<ThreadSummary> Threads { get; } = new();
        public bool HasMore { get; set; }
    }

    public sealed class SearchForumResult
    {
        public string Name { get; set; } = "";
        public string Desc { get; set; } = "";
        public int MemberNum { get; set; }
        public int ThreadNum { get; set; }
        public string Avatar { get; set; } = "";
        public long Id { get; set; }
    }

    public sealed class SearchPostResult
    {
        public long ThreadId { get; set; }
        public long PostId { get; set; }
        public string Title { get; set; } = "";
        public string ForumName { get; set; } = "";
        public string Content { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public long Time { get; set; }
    }

    public sealed class MessageItem
    {
        public long PostId { get; set; }
        public long ThreadId { get; set; }
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string ForumName { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Portrait { get; set; } = "";
        public long Time { get; set; }
        public bool IsReply { get; set; }
        public bool IsAt { get; set; }
        public bool IsAgree { get; set; }
        public string Quote { get; set; } = "";
    }
}
