using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TieLiteDesktop.Core.Models;
using TieLiteDesktop.Core.Proto;

namespace TieLiteDesktop.Core.Api
{
    /// <summary>High-level Tieba endpoints used by the desktop UI.</summary>
    public sealed class TiebaApi
    {
        private readonly TiebaClient _client;

        public TiebaApi(TiebaClient client) => _client = client;

        private Dictionary<string, object> Envelope(Dictionary<string, object> body)
        {
            body["common"] = _client.CommonParams();
            return body;
        }

        private static void SetCommonVersion(Dictionary<string, object> body, string version)
        {
            if (body.TryGetValue("common", out var value) && value is Dictionary<string, object> common)
            {
                common["_client_version"] = version;
            }
        }

        // ---------------------------------------------------------------- home
        public async Task<HotPage> GetHotAsync(string tabCode = null, CancellationToken ct = default)
        {
            var body = Envelope(new Dictionary<string, object>
            {
                ["tabId"] = "",
                ["tabCode"] = tabCode ?? "",
            });
            var msg = await _client.CallProtoAsync("/c/f/forum/hotThreadList?cmd=309661",
                "tieba.hotThreadList.HotThreadListRequest",
                "tieba.hotThreadList.HotThreadListResponse", body, ct).ConfigureAwait(false);
            return ParseHot(msg);
        }

        public static HotPage ParseHot(ProtoMessage msg)
        {
            var data = msg.GetMessage("data");
            var page = new HotPage();
            if (data == null) return page;
            foreach (var t in data.GetMessages("threadInfo"))
            {
                page.Threads.Add(ThreadSummary.From(t));
            }
            foreach (var topic in data.GetMessages("topicList"))
            {
                page.Topics.Add(new HotTopic
                {
                    Id = topic.GetLong("topic_id"),
                    Name = topic.GetString("topic_name") ?? "",
                    DiscussNum = topic.GetLong("discuss_num"),
                    Desc = topic.GetString("topic_desc") ?? "",
                    Image = topic.GetString("topic_pic") ?? "",
                    ThreadId = topic.GetLong("topic_tid"),
                    UserName = topic.GetString("topic_user_name") ?? "",
                });
            }
            foreach (var tab in data.GetMessages("hotThreadTabInfo"))
            {
                page.Tabs.Add(new ForumTab
                {
                    Id = tab.GetInt("tabId"),
                    Type = tab.GetInt("tabType"),
                    Name = tab.GetString("tabName") ?? "",
                    Code = tab.GetString("tabCode") ?? "",
                });
            }
            return page;
        }

        /// <summary>Aggregates the default hot list with each category tab to build a TOP-N feed.</summary>
        public async Task<HotPage> GetHotFeedAsync(int limit = 30, CancellationToken ct = default)
        {
            var result = await GetHotAsync(null, ct).ConfigureAwait(false);
            var seen = new HashSet<long>(result.Threads.Select(t => t.Id));
            var tasks = result.Tabs
                .Where(t => !string.IsNullOrWhiteSpace(t.Code))
                .Select(async tab =>
                {
                    try
                    {
                        return await GetHotAsync(tab.Code, ct).ConfigureAwait(false);
                    }
                    catch
                    {
                        return null;
                    }
                })
                .ToArray();
            var pages = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (var page in pages)
            {
                if (page == null) continue;
                foreach (var thread in page.Threads)
                {
                    if (thread.Id != 0 && !seen.Add(thread.Id)) continue;
                    result.Threads.Add(thread);
                }
            }
            result.Threads.Sort((a, b) => b.HotNum.CompareTo(a.HotNum));
            if (result.Threads.Count > limit)
            {
                result.Threads.RemoveRange(limit, result.Threads.Count - limit);
            }
            return result;
        }

        // ---------------------------------------------------------------- forum
        public async Task<ForumPage> GetForumAsync(string kw, int page = 1, int sortType = 0,
            int? goodClassifyId = null, CancellationToken ct = default)
        {
            var body = Envelope(new Dictionary<string, object>
            {
                ["kw"] = kw,
                ["rn"] = 30,
                ["rn_need"] = 30,
                ["pn"] = page,
                ["q_type"] = 0,
                ["scr_w"] = 1280,
                ["scr_h"] = 860,
                ["scr_dip"] = 1.0,
                ["load_type"] = page == 1 ? 1 : 2,
                ["st_type"] = "frs_page",
                ["sort_type"] = sortType,
                ["is_good"] = goodClassifyId.HasValue ? 1 : 0,
                ["class_id"] = goodClassifyId ?? 0,
            });
            var msg = await _client.CallProtoAsync("/c/f/frs/page?cmd=301001",
                "tieba.frsPage.FrsPageRequest", "tieba.frsPage.FrsPageResponse", body, ct).ConfigureAwait(false);
            return ParseForum(msg);
        }

        public async Task<ForumPage> GetForumTabAsync(long forumId, string forumName, ForumTab tab, int page = 1,
            int sortType = 0, long lastThreadId = 0, CancellationToken ct = default)
        {
            var body = Envelope(new Dictionary<string, object>
            {
                ["tab_id"] = tab.Id,
                ["forum_id"] = forumId,
                ["pn"] = page,
                ["rn"] = 30,
                ["scr_w"] = 1280,
                ["scr_h"] = 860,
                ["scr_dip"] = 1,
                ["last_thread_id"] = lastThreadId,
                ["is_default_navtab"] = tab.IsDefault ? 1 : 0,
                ["tab_name"] = tab.Name,
                ["is_general_tab"] = tab.IsGeneral ? 1 : 0,
                ["sort_type"] = sortType,
                ["tab_type"] = tab.Type,
                ["ad_ext_params"] = "",
                ["ad_bear_context"] = "",
                ["has_ad_bear"] = 0,
                ["ad_bear_sid"] = "",
                ["ad_bear_sid_price"] = 0.0,
                ["request_times"] = 0,
                ["frs_common_info"] = "",
                ["is_newfrs"] = 1,
                ["is_video_doublerow"] = 0,
            });
            var previousVersion = _client.VersionOverride;
            _client.VersionOverride = "12.35.1.0";
            ProtoMessage msg;
            try
            {
                msg = await _client.CallProtoAsync("/c/f/frs/generalTabList?cmd=309622&format=protobuf",
                    "tieba.GeneralTabList.GeneralTabListRequest", "tieba.GeneralTabList.GeneralTabListResponse",
                    body, ct).ConfigureAwait(false);
            }
            finally
            {
                _client.VersionOverride = previousVersion;
            }
            var data = msg.GetMessage("data");
            var result = new ForumPage
            {
                Forum = new ForumInfo { Id = forumId, Name = forumName },
                Page = new PageInfo { CurrentPage = page, HasMore = data != null && data.GetInt("has_more") != 0 },
            };
            if (data != null)
            {
                foreach (var thread in data.GetMessages("general_list"))
                {
                    result.Threads.Add(ThreadSummary.From(thread));
                }
            }
            return result;
        }

        public static ForumPage ParseForum(ProtoMessage msg)
        {
            var data = msg.GetMessage("data");
            var result = new ForumPage();
            if (data == null) return result;

            result.Forum = ForumInfo.From(data.GetMessage("forum"));
            result.Forum.RuleTitle = data.GetMessage("forum_rule")?.GetString("title") ?? "";
            result.User = UserInfo.From(data.GetMessage("user"));
            result.Page = PageInfo.From(data.GetMessage("page"));
            var authors = new Dictionary<long, UserInfo>();
            foreach (var u in data.GetMessages("user_list"))
            {
                var info = UserInfo.From(u);
                if (info.Id > 0) authors[info.Id] = info;
            }
            foreach (var t in data.GetMessages("thread_list"))
            {
                var thread = ThreadSummary.From(t);
                if (thread.Author == null || thread.Author.Id == 0)
                {
                    if (authors.TryGetValue(thread.AuthorId, out var known)) thread.Author = known;
                }
                result.Threads.Add(thread);
                if (!string.IsNullOrWhiteSpace(thread.Title) && thread.Title.Contains("吧规", StringComparison.Ordinal))
                {
                    result.Forum.RuleTitle = thread.Title;
                    result.Forum.RuleThreadId = thread.Id;
                }
            }
            var navInfo = data.GetMessage("nav_tab_info");
            var tabMessages = navInfo != null && navInfo.GetMessages("tab").Count > 0
                ? navInfo.GetMessages("tab")
                : data.GetMessages("frs_tab_info");
            foreach (var tab in tabMessages)
            {
                result.Tabs.Add(new ForumTab
                {
                    Id = tab.GetInt("tabId"),
                    Type = tab.GetInt("tabType"),
                    Name = tab.GetString("tabName") ?? "",
                    Code = tab.GetString("tabCode") ?? "",
                    IsGeneral = tab.GetBool("isGeneralTab"),
                    IsDefault = tab.GetBool("isDefault"),
                });
            }
            return result;
        }

        // ---------------------------------------------------------------- thread
        public async Task<ThreadPage> GetThreadAsync(long threadId, int page = 1, bool seeLz = false,
            bool reverse = false, long postId = 0, CancellationToken ct = default)
        {
            var body = Envelope(new Dictionary<string, object>
            {
                ["kz"] = threadId,
                ["pn"] = page,
                ["rn"] = 30,
                ["with_floor"] = 1,
                ["floor_rn"] = 3,
                ["lz"] = seeLz ? 1 : 0,
                ["r"] = 0,
                ["back"] = reverse ? 1 : 0,
                ["scr_w"] = 1280,
                ["scr_h"] = 860,
                ["scr_dip"] = 1.0,
                ["q_type"] = 0,
                ["st_type"] = "tb_frslist",
            });
            if (postId > 0) body["pid"] = postId;
            SetCommonVersion(body, "12.52.1.0");

            var previousVersion = _client.VersionOverride;
            _client.VersionOverride = "12.52.1.0";
            ProtoMessage msg;
            try
            {
                msg = await _client.CallProtoAsync("/c/f/pb/page?cmd=302001&format=protobuf",
                    "tieba.pbPage.PbPageRequest", "tieba.pbPage.PbPageResponse", body, ct).ConfigureAwait(false);
            }
            finally
            {
                _client.VersionOverride = previousVersion;
            }
            return ParseThread(msg);
        }

        public static ThreadPage ParseThread(ProtoMessage msg)
        {
            var data = msg.GetMessage("data");
            var result = new ThreadPage();
            if (data == null) return result;

            result.Thread = ThreadSummary.From(data.GetMessage("thread"));
            result.Forum = ForumInfo.FromSimple(data.GetMessage("forum"));
            if (string.IsNullOrWhiteSpace(result.Thread.ForumName)) result.Thread.ForumName = result.Forum.Name;
            if (string.IsNullOrWhiteSpace(result.Forum.Name)) result.Forum.Name = result.Thread.ForumName;
            result.Page = PageInfo.From(data.GetMessage("page"));
            result.CurrentUser = UserInfo.From(data.GetMessage("user"));
            result.IsOnlyOp = false;

            var users = new Dictionary<long, UserInfo>();
            foreach (var u in data.GetMessages("user_list"))
            {
                var info = UserInfo.From(u);
                if (info.Id > 0) users[info.Id] = info;
            }

            var posts = new List<PostItem>();
            foreach (var p in data.GetMessages("post_list"))
            {
                posts.Add(MapPost(p, users));
            }
            result.Posts.AddRange(posts);

            if (result.Thread.Author == null || result.Thread.Author.Id == 0)
            {
                if (posts.Count > 0) result.Thread.Author = posts[0].Author;
            }
            else if (users.TryGetValue(result.Thread.Author.Id, out var fullOp))
            {
                if (string.IsNullOrWhiteSpace(result.Thread.Author.IpAddress))
                {
                    result.Thread.Author.IpAddress = fullOp.IpAddress;
                }
            }
            var opId = result.Thread.Author?.Id ?? 0;
            foreach (var p in result.Posts)
            {
                p.IsOp = p.AuthorId == opId && opId != 0;
            }
            return result;
        }

        private static PostItem MapPost(ProtoMessage p, Dictionary<long, UserInfo> users)
        {
            var post = new PostItem
            {
                Id = p.GetLong("id"),
                Floor = p.GetInt("floor"),
                Time = p.GetLong("time"),
                AuthorId = p.GetLong("author_id"),
                Title = p.GetString("title") ?? "",
                SubPostCount = p.GetInt("sub_post_number"),
            };
            var author = p.GetMessage("author");
            post.Author = users.TryGetValue(post.AuthorId, out var known)
                ? known
                : author != null && author.GetLong("id") > 0
                    ? UserInfo.From(author)
                    : new UserInfo { Id = post.AuthorId };
            if (string.IsNullOrWhiteSpace(post.Author.IpAddress) && author != null)
            {
                post.Author.IpAddress = author.GetString("ip_address") ?? author.GetString("ip") ?? "";
            }

            var agree = p.GetMessage("agree");
            if (agree != null)
            {
                post.AgreeNum = agree.GetInt("agreeNum");
                post.HasAgree = agree.GetInt("hasAgree") != 0;
                post.DisagreeNum = agree.GetInt("disagreeNum");
            }
            post.Video = VideoInfoModel.From(p.GetMessage("video_info"));
            post.Content.AddRange(ContentParser.ParseSegments(p));

            var sub = p.GetMessage("sub_post_list");
            if (sub != null)
            {
                foreach (var sp in sub.GetMessages("sub_post_list"))
                {
                    var item = new SubPost
                    {
                        Id = sp.GetLong("id"),
                        AuthorId = sp.GetLong("author_id"),
                        Floor = sp.GetInt("floor"),
                        Time = sp.GetLong("time"),
                    };
                    var subAuthor = sp.GetMessage("author");
                    item.Author = subAuthor != null && subAuthor.GetLong("id") > 0
                        ? UserInfo.From(subAuthor)
                        : users.TryGetValue(item.AuthorId, out var knownSub) ? knownSub : new UserInfo { Id = item.AuthorId };
                    item.Content.AddRange(ContentParser.ParseSegments(sp));
                    var subAgree = sp.GetMessage("agree");
                    if (subAgree != null)
                    {
                        item.AgreeNum = subAgree.GetInt("agreeNum");
                        item.HasAgree = subAgree.GetBool("hasAgree");
                    }
                    post.SubPosts.Add(item);
                }
            }
            return post;
        }

        public async Task<(ForumInfo forum, List<SubPost> subPosts, PageInfo page)> GetFloorAsync(long threadId,
            long postId, int page = 1, long forumId = 0, CancellationToken ct = default)
        {
            var body = Envelope(new Dictionary<string, object>
            {
                ["kz"] = threadId,
                ["pid"] = postId,
                ["pn"] = page,
                ["scr_w"] = 1280,
                ["scr_h"] = 860,
                ["scr_dip"] = 1.0,
                ["is_comm_reverse"] = 0,
                ["forum_id"] = forumId,
            });
            SetCommonVersion(body, "22.10.1.0");
            var previousVersion = _client.VersionOverride;
            _client.VersionOverride = "22.10.1.0";
            ProtoMessage msg;
            try
            {
                msg = await _client.CallProtoAsync("/c/f/pb/floor?cmd=302002&format=protobuf",
                    "tieba.pbFloor.PbFloorRequest", "tieba.pbFloor.PbFloorResponse", body, ct).ConfigureAwait(false);
            }
            finally
            {
                _client.VersionOverride = previousVersion;
            }
            var data = msg.GetMessage("data");
            var subs = new List<SubPost>();
            var forum = new ForumInfo();
            var pageInfo = new PageInfo();
            if (data == null) return (forum, subs, pageInfo);

            forum = ForumInfo.FromSimple(data.GetMessage("forum"));
            pageInfo = PageInfo.From(data.GetMessage("page"));
            foreach (var group in data.GetMessages("subpost_list"))
            {
                var children = group.GetMessages("sub_post_list");
                if (children.Count == 0 && group.GetLong("id") > 0)
                {
                    children = new List<ProtoMessage> { group };
                }
                foreach (var sp in children)
                {
                    var item = new SubPost
                    {
                        Id = sp.GetLong("id"),
                        AuthorId = sp.GetLong("author_id"),
                        Floor = sp.GetInt("floor"),
                        Time = sp.GetLong("time"),
                    };
                    var author = sp.GetMessage("author");
                    item.Author = author != null ? UserInfo.From(author) : new UserInfo { Id = item.AuthorId };
                    item.Content.AddRange(ContentParser.ParseSegments(sp));
                    subs.Add(item);
                }
            }
            return (forum, subs, pageInfo);
        }

        // ---------------------------------------------------------------- profile
        public async Task<UserInfo> GetSelfUserAsync(CancellationToken ct = default)
        {
            // app login endpoint: returns user{id,portrait,name} + tbs
            try
            {
                var text = await _client.PostFormAsync("/c/s/login", new Dictionary<string, string>
                {
                    ["bdusstoken"] = Svc.Account.Bduss,
                }, ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("user", out var loginUser) && loginUser.ValueKind == JsonValueKind.Object)
                {
                    var parsed = ParseSelfUser(loginUser);
                    if (parsed != null && parsed.Id != 0) return parsed;
                }
            }
            catch
            {
            }

            foreach (var url in new[]
                     {
                         "https://tieba.baidu.com/f/user/json_userinfo",
                         "https://tieba.baidu.com/mo/q/newmoindex?need_user=1",
                     })
            {
                try
                {
                    var text = await _client.GetStringAsync(url, "https://tieba.baidu.com/", ct).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    using var doc = JsonDocument.Parse(text);
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object) continue;
                    if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                    {
                        if (data.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object)
                        {
                            var parsed = ParseSelfUser(user);
                            if (parsed != null) return parsed;
                        }
                        var direct = ParseSelfUser(data);
                        if (direct != null) return direct;
                    }
                    var single = ParseSelfUser(root);
                    if (single != null) return single;
                }
                catch
                {
                }
            }
            return null;
        }

        private static UserInfo ParseSelfUser(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;
            var id = 0L;
            foreach (var name in new[] { "user_id", "uid", "id" })
            {
                if (element.TryGetProperty(name, out var value)) id = JsonLong(value);
                if (id != 0) break;
            }
            var userName = "";
            foreach (var name in new[] { "user_name_show", "name_show", "user_name", "name", "username" })
            {
                var value = JsonString(element, name);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    userName = value;
                    break;
                }
            }
            if (id == 0 && string.IsNullOrWhiteSpace(userName)) return null;
            return new UserInfo
            {
                Id = id,
                Name = JsonString(element, "user_name"),
                ShowName = userName,
                Portrait = JsonString(element, "portrait"),
            };
        }

        public async Task<string> GetProfileRawAsync(long uid)
        {
            try
            {
                return await _client.PostFormAsync("/c/u/user/profile", new Dictionary<string, string>
                {
                    ["uid"] = uid.ToString(CultureInfo.InvariantCulture),
                    ["need_post_count"] = "1",
                    ["is_guest"] = Svc.Account.IsLoggedIn ? "0" : "1",
                    ["has_plist"] = "1",
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex;
            }
        }

        public async Task<string> GetProfileProtoDumpAsync(long uid)
        {
            try
            {
                var body = Envelope(new Dictionary<string, object>
                {
                    ["uid"] = uid,
                    ["need_post_count"] = 1,
                    ["page"] = 1,
                });
                var msg = await _client.CallProtoAsync("/c/u/user/profile?cmd=303012&format=protobuf",
                    "tieba.profile.ProfileRequest", "tieba.profile.ProfileResponse", body).ConfigureAwait(false);
                var data = msg.GetMessage("data");
                var user = data?.GetMessage("user");
                return "user fields:\n" + (user?.Dump() ?? "(null)") +
                       "\nerror:\n" + (msg.GetMessage("error")?.Dump() ?? "(null)");
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex;
            }
        }

        public async Task<UserInfo> GetProfileProtoAsync(long uid, CancellationToken ct = default)
        {
            try
            {
                var body = Envelope(new Dictionary<string, object>
                {
                    ["uid"] = uid,
                    ["need_post_count"] = 1,
                    ["page"] = 1,
                });
                SetCommonVersion(body, "12.52.1.0");
                var previousVersion = _client.VersionOverride;
                _client.VersionOverride = "12.52.1.0";
                ProtoMessage msg;
                try
                {
                    msg = await _client.CallProtoAsync("/c/u/user/profile?cmd=303012&format=protobuf",
                        "tieba.profile.ProfileRequest", "tieba.profile.ProfileResponse", body, ct).ConfigureAwait(false);
                }
                finally
                {
                    _client.VersionOverride = previousVersion;
                }
                var data = msg.GetMessage("data");
                var user = data?.GetMessage("user");
                return user == null ? null : UserInfo.From(user);
            }
            catch
            {
                return null;
            }
        }

        public async Task<(UserInfo user, List<ThreadSummary> posts)> GetProfileAsync(long uid, int page = 1,
            CancellationToken ct = default)
        {
            // web/app JSON profile: uid lives in the form, not in the protobuf schema
            try
            {
                var text = await _client.PostFormAsync("/c/u/user/profile", new Dictionary<string, string>
                {
                    ["uid"] = uid.ToString(CultureInfo.InvariantCulture),
                    ["need_post_count"] = "1",
                    ["is_guest"] = Svc.Account.IsLoggedIn ? "0" : "1",
                    ["has_plist"] = "1",
                }, ct).ConfigureAwait(false);
                CheckJsonError(text);
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.TryGetProperty("user", out var userElement) && userElement.ValueKind == JsonValueKind.Object)
                {
                    var jsonUser = new UserInfo
                    {
                        Id = userElement.TryGetProperty("id", out var id) ? JsonLong(id) : uid,
                        Name = JsonString(userElement, "name"),
                        ShowName = FirstNonEmpty(JsonString(userElement, "name_show"), JsonString(userElement, "name")),
                        Portrait = JsonString(userElement, "portrait"),
                        Intro = JsonString(userElement, "intro"),
                        Sex = (int)Math.Min(int.MaxValue, userElement.TryGetProperty("sex", out var sex) ? JsonLong(sex) : 0),
                        PostNum = (int)Math.Min(int.MaxValue, userElement.TryGetProperty("post_num", out var pn) ? JsonLong(pn) : 0),
                        ThreadNum = (int)Math.Min(int.MaxValue, userElement.TryGetProperty("thread_num", out var tn) ? JsonLong(tn) : 0),
                        FansNum = (int)Math.Min(int.MaxValue, userElement.TryGetProperty("fans_num", out var fn) ? JsonLong(fn) : 0),
                        AgreeNum = (int)Math.Min(int.MaxValue, FirstLong(userElement, "total_agree_num", "agree_num", "user_agree_num")),
                        FollowNum = (int)Math.Min(int.MaxValue, FirstLong(userElement, "follow_num", "concern_num")),
                        TbAge = ParseDouble(JsonString(userElement, "tb_age")),
                        IpAddress = FirstNonEmpty(JsonString(userElement, "ip_address"), JsonString(userElement, "ip"),
                            JsonString(root, "ip_address"), JsonString(root, "ip"),
                            JsonString(userElement, "location")),
                        HasConcerned = FirstLong(userElement, "has_concerned", "is_followed", "is_concerned") == 1,
                    };
                    return (jsonUser, new List<ThreadSummary>());
                }
            }
            catch
            {
            }

            var body = Envelope(new Dictionary<string, object>
            {
                ["need_post_count"] = 1,
                ["is_guest"] = _client.Account.IsLoggedIn ? 0 : 1,
                ["pn"] = page,
                ["rn"] = 20,
                ["has_plist"] = 1,
                ["scr_w"] = 1280,
                ["scr_h"] = 860,
                ["scr_dip"] = 1.0,
                ["q_type"] = 0,
                ["page"] = page,
                ["st_type"] = "home",
            });
            var msg = await _client.CallProtoAsync("/c/u/user/profile?cmd=303012&format=protobuf",
                "tieba.profile.ProfileRequest", "tieba.profile.ProfileResponse", body, ct).ConfigureAwait(false);
            var data = msg.GetMessage("data");
            var user = data != null ? UserInfo.From(data.GetMessage("user")) : new UserInfo();
            var posts = new List<ThreadSummary>();
            if (data != null)
            {
                foreach (var t in data.GetMessages("newest_dynamic_list"))
                {
                    posts.Add(ThreadSummary.From(t));
                }
                foreach (var t in data.GetMessages("post_list"))
                {
                    var thread = ThreadSummary.From(t);
                    if (thread.Id == 0) thread.Id = t.GetLong("tid");
                    if (thread.Id != 0) posts.Add(thread);
                }
            }
            return (user, posts);
        }

        public async Task<string> GetUserPostsRawAsync(long uid, int page, bool isThread, string viewCard)
        {
            try
            {
                var form = new Dictionary<string, string>
                {
                    ["uid"] = uid.ToString(CultureInfo.InvariantCulture),
                    ["pn"] = page.ToString(CultureInfo.InvariantCulture),
                    ["is_thread"] = isThread ? "1" : "0",
                    ["rn"] = "20",
                    ["need_content"] = "1",
                    ["is_view_card"] = viewCard,
                };
                return await _client.PostFormAsync("/c/u/feed/userpost", form).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex;
            }
        }

        public async Task<List<ThreadSummary>> GetUserPostsAsync(long uid, int page = 1, bool isThread = true,
            CancellationToken ct = default, bool hiddenPass = false)
        {
            // the JSON web endpoint understands is_thread; the proto schema does not
            try
            {
                var form = new Dictionary<string, string>
                {
                    ["uid"] = uid.ToString(CultureInfo.InvariantCulture),
                    ["pn"] = page.ToString(CultureInfo.InvariantCulture),
                    ["is_thread"] = isThread ? "1" : "0",
                    ["rn"] = "20",
                    ["need_content"] = "1",
                    ["is_view_card"] = uid == Svc.Account.Uid ? "1" : "2",
                };
                var text = await _client.PostFormAsync("/c/u/feed/userpost", form, ct).ConfigureAwait(false);
                CheckJsonError(text);
                using var doc = JsonDocument.Parse(text);
                var jsonPosts = new List<ThreadSummary>();
                var arrays = new List<JsonElement>();
                foreach (var key in new[] { "post_list", "hide_post_list", "hidden_post_list" })
                {
                    if (doc.RootElement.TryGetProperty(key, out var candidate) && candidate.ValueKind == JsonValueKind.Array)
                    {
                        arrays.AddRange(candidate.EnumerateArray());
                    }
                }
                {
                    foreach (var item in arrays)
                    {
                        var threadId = item.TryGetProperty("thread_id", out var tid) ? JsonLong(tid) : 0;
                        var postId = item.TryGetProperty("post_id", out var pid) ? JsonLong(pid) : 0;
                        var title = JsonString(item, "title");
                        var textContent = "";
                        var image = "";
                        if (item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                        {
                            CollectUserPostContent(content, ref textContent, ref image);
                        }
                        if (string.IsNullOrWhiteSpace(textContent) &&
                            item.TryGetProperty("abstract", out var abstracts) && abstracts.ValueKind == JsonValueKind.Array)
                        {
                            CollectUserPostContent(abstracts, ref textContent, ref image);
                        }
                        var hidden = FirstLong(item, "is_hide", "is_hidden", "hide", "is_frs_mask") != 0;
                        if (hidden && uid != Svc.Account.Uid) continue;
                        var summary = new ThreadSummary
                        {
                            IsHidden = hidden,
                            Id = threadId != 0 ? threadId : postId,
                            Title = !string.IsNullOrWhiteSpace(title) ? title : textContent,
                            ForumName = JsonString(item, "forum_name"),
                            ForumId = FirstLong(item, "forum_id"),
                            ReplyNum = (int)Math.Min(int.MaxValue, item.TryGetProperty("reply_num", out var rn) ? JsonLong(rn) : 0),
                            AgreeNum = (int)Math.Min(int.MaxValue, item.TryGetProperty("agree_num", out var an) ? JsonLong(an) : 0),
                            LastTimeInt = item.TryGetProperty("create_time", out var ct2) ? JsonLong(ct2) : 0,
                            PreviewText = !string.IsNullOrWhiteSpace(title) && textContent != title ? textContent : "",
                            PreviewImage = image,
                            Author = new UserInfo
                            {
                                Id = item.TryGetProperty("user_id", out var u) ? JsonLong(u) : 0,
                                Name = JsonString(item, "user_name"),
                                ShowName = FirstNonEmpty(JsonString(item, "name_show"), JsonString(item, "user_name")),
                                Portrait = JsonString(item, "user_portrait"),
                            },
                        };
                        jsonPosts.Add(summary);
                    }
                }
                return jsonPosts;
            }
            catch
            {
            }

            var body = Envelope(new Dictionary<string, object>
            {
                ["uid"] = uid,
                ["rn"] = 20,
                ["offset"] = (page - 1) * 20,
                ["is_thread"] = isThread ? 1 : 0,
                ["need_content"] = 1,
                ["pn"] = page,
                ["scr_w"] = 1280,
                ["scr_h"] = 860,
                ["scr_dip"] = 1.0,
                ["q_type"] = 0,
            });
            var msg = await _client.CallProtoAsync("/c/u/feed/userpost?cmd=303002&format=protobuf",
                "tieba.userPost.UserPostRequest", "tieba.userPost.UserPostResponse", body, ct).ConfigureAwait(false);
            var data = msg.GetMessage("data");
            var posts = new List<ThreadSummary>();
            if (data == null) return posts;
            foreach (var item in data.GetMessages("post_list"))
            {
                var thread = ThreadSummary.From(item);
                if (thread.Id == 0)
                {
                    thread.Id = item.GetLong("tid");
                    thread.Title = item.GetString("title") ?? thread.Title;
                }
                posts.Add(thread);
            }
            return posts;
        }

        // ---------------------------------------------------------------- search (mobile web JSON)
        public async Task<List<SearchForumResult>> SearchForumAsync(string keyword, CancellationToken ct = default)
        {
            var url = "https://tieba.baidu.com/mo/q/search/forum?word=" + Uri.EscapeDataString(keyword) + "&ie=utf-8";
            var text = await _client.GetStringAsync(url, "https://tieba.baidu.com/", ct).ConfigureAwait(false);
            var results = new List<SearchForumResult>();
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return results;
            if (data.TryGetProperty("exactMatch", out var exact) && exact.ValueKind == JsonValueKind.Object)
            {
                results.Add(ReadForum(exact));
            }
            if (data.TryGetProperty("fuzzyMatch", out var fuzzy) && fuzzy.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in fuzzy.EnumerateArray()) results.Add(ReadForum(item));
            }
            return results.Where(r => !string.IsNullOrEmpty(r.Name)).ToList();
        }

        private static SearchForumResult ReadForum(JsonElement e)
        {
            string Get(string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
            int GetInt(string name)
            {
                if (!e.TryGetProperty(name, out var v)) return 0;
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
                if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out i)) return i;
                return 0;
            }
            return new SearchForumResult
            {
                Id = GetInt("forum_id"),
                Name = FirstNonEmpty(Get("forum_name_show"), Get("forum_name")),
                Desc = FirstNonEmpty(Get("intro"), Get("slogan"), Get("forum_desc")),
                MemberNum = GetInt("concern_num") != 0 ? GetInt("concern_num") : GetInt("member_num"),
                ThreadNum = GetInt("post_num") != 0 ? GetInt("post_num") : GetInt("thread_num"),
                Avatar = Get("avatar"),
            };
        }

        public async Task<List<UserInfo>> SearchUsersAsync(string keyword, int page = 1,
            CancellationToken ct = default)
        {
            var users = new List<UserInfo>();
            var url = "https://tieba.baidu.com/mo/q/search/user?word=" + Uri.EscapeDataString(keyword) +
                      "&pn=" + page + "&ie=utf-8";
            var text = await _client.GetStringAsync(url, "https://tieba.baidu.com/", ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return users;
            var items = new List<JsonElement>();
            if (data.TryGetProperty("exactMatch", out var exact) && exact.ValueKind == JsonValueKind.Object)
            {
                items.Add(exact);
            }
            if (data.TryGetProperty("fuzzyMatch", out var fuzzy) && fuzzy.ValueKind == JsonValueKind.Array)
            {
                items.AddRange(fuzzy.EnumerateArray());
            }
            var seen = new HashSet<long>();
            foreach (var item in items)
            {
                var id = item.TryGetProperty("id", out var idValue) ? JsonLong(idValue) : 0;
                if (id <= 0 || !seen.Add(id)) continue;
                var name = FirstNonEmpty(JsonString(item, "name"), JsonString(item, "user_name"));
                var nick = FirstNonEmpty(JsonString(item, "show_nickname"), JsonString(item, "user_nickname"));
                users.Add(new UserInfo
                {
                    Id = id,
                    Name = name,
                    ShowName = FirstNonEmpty(nick, name),
                    Portrait = JsonString(item, "portrait"),
                    Level = (int)FirstLong(item, "level_id", "user_level"),
                    FansNum = (int)FirstLong(item, "fans_num"),
                });
            }
            return users;
        }

        public async Task<List<SearchPostResult>> SearchForumAsync(string forumName, string keyword, int page = 1,
            CancellationToken ct = default)
        {
            var results = new List<SearchPostResult>();
            if (string.IsNullOrWhiteSpace(forumName)) return results;
            var text = await _client.PostFormAsync("/c/s/searchpost", new Dictionary<string, string>
            {
                ["_client_version"] = "12.35.1.0",
                ["kw"] = forumName,
                ["only_thread"] = "1",
                ["pn"] = page.ToString(CultureInfo.InvariantCulture),
                ["rn"] = "20",
                ["sm"] = "0",
                ["word"] = keyword,
            }, ct).ConfigureAwait(false);
            CheckJsonError(text);
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("post_list", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return results;
            }
            foreach (var item in list.EnumerateArray())
            {
                string Get(string name) => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString() ?? ""
                    : "";
                long GetLong(string name) => item.TryGetProperty(name, out var value) ? JsonLong(value) : 0;
                var author = item.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object
                    ? JsonString(a, "name_show")
                    : "";
                results.Add(new SearchPostResult
                {
                    ThreadId = GetLong("tid"),
                    PostId = GetLong("pid"),
                    Title = Get("title"),
                    ForumName = FirstNonEmpty(Get("fname"), forumName),
                    Content = StripHtml(Get("content")),
                    AuthorName = author,
                    Time = GetLong("time"),
                });
            }
            return results;
        }

        public async Task<List<SearchPostResult>> SearchThreadAsync(string keyword, int page = 1,
            string forumName = null, CancellationToken ct = default)
        {
            var url = "https://tieba.baidu.com/mo/q/search/thread?word=" + Uri.EscapeDataString(keyword) +
                      "&pn=" + page + "&ie=utf-8";
            if (!string.IsNullOrWhiteSpace(forumName))
            {
                url += "&kw=" + Uri.EscapeDataString(forumName);
            }
            var text = await _client.GetStringAsync(url, "https://tieba.baidu.com/", ct).ConfigureAwait(false);
            var results = new List<SearchPostResult>();
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return results;
            if (!data.TryGetProperty("post_list", out var list) || list.ValueKind != JsonValueKind.Array) return results;
            foreach (var item in list.EnumerateArray())
            {
                string Get(string name) => item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
                long GetLong(string name)
                {
                    if (!item.TryGetProperty(name, out var v)) return 0;
                    if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var l)) return l;
                    return long.TryParse(v.ValueKind == JsonValueKind.String ? v.GetString() : "", out l) ? l : 0;
                }
                var forum = FirstNonEmpty(Get("fname"), Get("forum_name"));
                if (!string.IsNullOrWhiteSpace(forumName) && !string.IsNullOrWhiteSpace(forum) &&
                    !string.Equals(forum, forumName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                results.Add(new SearchPostResult
                {
                    ThreadId = GetLong("tid"),
                    PostId = GetLong("pid"),
                    Title = Get("title"),
                    ForumName = FirstNonEmpty(Get("fname"), Get("forum_name")),
                    Content = StripHtml(Get("content")),
                    AuthorName = FirstNonEmpty(Get("user_name"), Get("author_name")),
                    Time = GetLong("time"),
                });
            }
            return results;
        }

        // ---------------------------------------------------------------- actions
        public async Task<string> GetTbsAsync(CancellationToken ct = default)
        {
            var text = await _client.GetStringAsync("https://tieba.baidu.com/dc/common/tbs",
                "https://tieba.baidu.com/", ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(text);
            var value = doc.RootElement.TryGetProperty("tbs", out var tbs) ? tbs.GetString() : "";
            _client.Tbs = value;
            return value;
        }

        public async Task<string> UploadPictureAsync(string filePath, string forumName, CancellationToken ct = default)
        {
            var bytes = await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);
            var md5 = DeviceProfile.Md5Hex(filePath + bytes.Length);
            var width = "0";
            var height = "0";
            try
            {
                using var stream = File.OpenRead(filePath);
                var frame = System.Windows.Media.Imaging.BitmapFrame.Create(stream,
                    System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,
                    System.Windows.Media.Imaging.BitmapCacheOption.None);
                width = frame.PixelWidth.ToString(CultureInfo.InvariantCulture);
                height = frame.PixelHeight.ToString(CultureInfo.InvariantCulture);
            }
            catch
            {
            }
            using var form = new System.Net.Http.MultipartFormDataContent();
            void Add(string name, string value) => form.Add(new System.Net.Http.StringContent(value), name);
            Add("alt", "json");
            Add("chunkNo", "1");
            if (!string.IsNullOrWhiteSpace(forumName)) Add("forum_name", forumName);
            Add("groupId", "1");
            Add("height", height);
            Add("isFinish", "true");
            Add("is_bjh", "0");
            Add("pic_water_type", "1");
            Add("resourceId", md5 + bytes.Length.ToString(CultureInfo.InvariantCulture));
            Add("saveOrigin", "false");
            Add("size", bytes.Length.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(forumName)) Add("small_flow_fname", forumName);
            Add("width", width);
            var file = new System.Net.Http.ByteArrayContent(bytes);
            file.Headers.TryAddWithoutValidation("Content-Type", "application/octet-stream");
            form.Add(file, "chunk", Path.GetFileName(filePath));
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://tiebac.baidu.com/c/s/uploadPicture") { Content = form };
            request.Headers.TryAddWithoutValidation("User-Agent", "bdtb for Android 12.35.1.0");
            request.Headers.TryAddWithoutValidation("Referer", "https://tieba.baidu.com/");
            request.Headers.TryAddWithoutValidation("Cookie", Svc.Account.BuildCookieHeader());
            using var response = await Svc.Client.Http.SendAsync(request, ct).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.TryGetProperty("picInfo", out var info))
            {
                foreach (var key in new[] { "originPic", "bigPic", "smallPic" })
                {
                    if (info.TryGetProperty(key, out var pic) && pic.TryGetProperty("picUrl", out var url))
                    {
                        var value = url.GetString();
                        if (!string.IsNullOrWhiteSpace(value)) return value;
                    }
                }
            }
            if (root.TryGetProperty("resourceId", out var rid)) return rid.GetString() ?? "";
            throw new InvalidOperationException(root.TryGetProperty("error_msg", out var msg) ? msg.GetString() : "图片上传失败");
        }

        public async Task AddPostAsync(string content, long forumId, string forumName, long threadId,
            long? postId = null, long? subPostId = null, long? replyUid = null, CancellationToken ct = default)
        {
            var tbs = await GetTbsAsync(ct).ConfigureAwait(false);
            var body = Envelope(new Dictionary<string, object>
            {
                ["content"] = content,
                ["fid"] = forumId.ToString(CultureInfo.InvariantCulture),
                ["kw"] = forumName,
                ["tid"] = threadId.ToString(CultureInfo.InvariantCulture),
                ["tbs"] = tbs,
                ["anonymous"] = "1",
                ["can_no_forum"] = "0",
                ["is_feedback"] = "0",
                ["takephoto_num"] = "0",
                ["entrance_type"] = "0",
                ["vcode_tag"] = "12",
                ["new_vcode"] = "1",
                ["is_ad"] = "0",
                ["is_barrage"] = "0",
                ["post_from"] = "3",
                ["name_show"] = Svc.Account.UserName ?? "",
                ["is_pictxt"] = content.Contains("[img]") ? "1" : "0",
                ["show_custom_figure"] = 0,
                ["is_show_bless"] = 0,
                ["st_type"] = "tb_frslist",
                ["from_fourm_id"] = forumId.ToString(CultureInfo.InvariantCulture),
            });
            if (body.TryGetValue("common", out var commonObject) && commonObject is Dictionary<string, object> common)
            {
                common["tbs"] = tbs;
                common["_client_version"] = "12.35.1.0";
            }
            if (postId.HasValue && postId.Value > 0)
            {
                body["floor_num"] = "";
                body["quote_id"] = postId.Value.ToString(CultureInfo.InvariantCulture);
                body["post_id"] = postId.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (subPostId.HasValue && subPostId.Value > 0)
            {
                body["sub_post_id"] = subPostId.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (replyUid.HasValue && replyUid.Value > 0)
            {
                body["reply_uid"] = replyUid.Value.ToString(CultureInfo.InvariantCulture);
            }
            await _client.CallProtoAsync("/c/c/post/add?cmd=309731&format=protobuf",
                "tieba.addPost.AddPostRequest", "tieba.addPost.AddPostResponse", body, ct).ConfigureAwait(false);
        }

        public async Task AgreeAsync(long threadId, long postId, bool isFloor, bool cancel,
            long forumId = 0, CancellationToken ct = default)
        {
            var tbs = await GetTbsAsync(ct).ConfigureAwait(false);
            var form = new Dictionary<string, string>
            {
                ["thread_id"] = threadId.ToString(CultureInfo.InvariantCulture),
                ["post_id"] = postId.ToString(CultureInfo.InvariantCulture),
                ["op_type"] = cancel ? "1" : "0",
                ["obj_type"] = "3",
                ["agree_type"] = "2",
                ["cuid_gid"] = "",
                ["_client_version"] = "8.0.8.0",
                ["stoken"] = Svc.Account.Stoken ?? "",
                ["BDUSS"] = Svc.Account.Bduss ?? "",
                ["tbs"] = tbs,
            };
            var text = await _client.PostFormAsync("/c/c/agree/opAgree", form, ct).ConfigureAwait(false);
            CheckJsonError(text);
        }

        public async Task<List<ThreadSummary>> GetRecommendedAsync(int page = 1, CancellationToken ct = default)
        {
            var result = new List<ThreadSummary>();
            try
            {
                var text = await _client.PostFormAsync("/c/f/excellent/personalized", new Dictionary<string, string>
                {
                    ["load_type"] = "1",
                    ["pn"] = page.ToString(CultureInfo.InvariantCulture),
                    ["need_tags"] = "0",
                    ["page_thread_count"] = "15",
                    ["cuid_gid"] = "",
                    ["_client_version"] = "8.0.8.0",
                    ["scr_w"] = "1280",
                    ["scr_h"] = "860",
                    ["scr_dip"] = "1.5",
                }, ct).ConfigureAwait(false);
                CheckJsonError(text);
                using var doc = JsonDocument.Parse(text);
                if (!doc.RootElement.TryGetProperty("thread_list", out var list) || list.ValueKind != JsonValueKind.Array)
                {
                    return result;
                }
                foreach (var item in list.EnumerateArray())
                {
                    var thread = new ThreadSummary
                    {
                        Id = item.TryGetProperty("id", out var id) ? JsonLong(id) : 0,
                        Title = JsonString(item, "title"),
                        ForumName = JsonString(item, "fname"),
                        ForumId = item.TryGetProperty("fid", out var fid) ? JsonLong(fid) : 0,
                        ReplyNum = (int)Math.Min(int.MaxValue, item.TryGetProperty("reply_num", out var rn) ? JsonLong(rn) : 0),
                        AgreeNum = (int)Math.Min(int.MaxValue, item.TryGetProperty("agree_num", out var an) ? JsonLong(an) : 0),
                        LastTimeInt = item.TryGetProperty("last_time_int", out var lt) ? JsonLong(lt) : 0,
                        PreviewText = JsonString(item, "abstract"),
                        IsTop = item.TryGetProperty("is_top", out var it) && JsonLong(it) != 0,
                        IsGood = item.TryGetProperty("is_good", out var ig) && JsonLong(ig) != 0,
                    };
                    if (item.TryGetProperty("author", out var author) && author.ValueKind == JsonValueKind.Object)
                    {
                        thread.Author = new UserInfo
                        {
                            Id = author.TryGetProperty("id", out var auid) ? JsonLong(auid) : 0,
                            Name = JsonString(author, "name"),
                            ShowName = FirstNonEmpty(JsonString(author, "name_show"), JsonString(author, "name")),
                            Portrait = JsonString(author, "portrait"),
                        };
                        thread.AuthorId = thread.Author.Id;
                    }
                    if (item.TryGetProperty("media", out var media) && media.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var m in media.EnumerateArray())
                        {
                            var type = m.TryGetProperty("type", out var t) ? JsonLong(t) : 0;
                            var src = FirstNonEmpty(JsonString(m, "bigPic"), JsonString(m, "srcPic"), JsonString(m, "cdnSrc"));
                            if (type == 3 && string.IsNullOrEmpty(thread.PreviewImage))
                            {
                                thread.PreviewImage = src;
                            }
                            if (type == 5) thread.HasVideo = true;
                        }
                    }
                    if (thread.Id != 0) result.Add(thread);
                }
            }
            catch
            {
            }
            return result;
        }

        private async Task<List<RecommendedForum>> GetSelfFollowedForumsAsync(CancellationToken ct)
        {
            var result = new List<RecommendedForum>();
            var text = await _client.PostFormAsync("/c/f/forum/forumGuide", new Dictionary<string, string>
            {
                ["sort_type"] = "3",
                ["call_from"] = "3",
            }, ct).ConfigureAwait(false);
            CheckJsonError(text);
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("like_forum", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return result;
            }
            foreach (var item in list.EnumerateArray())
            {
                var name = JsonString(item, "forum_name");
                if (string.IsNullOrWhiteSpace(name)) continue;
                result.Add(new RecommendedForum
                {
                    Id = (long)FirstLong(item, "forum_id"),
                    Name = name,
                    Avatar = JsonString(item, "avatar"),
                    Level = (int)FirstLong(item, "level_id"),
                    IsSigned = FirstLong(item, "is_sign") != 0,
                    MemberNum = (int)FirstLong(item, "member_count"),
                    ThreadNum = (int)FirstLong(item, "thread_num"),
                });
            }
            return result;
        }

        public async Task<List<RecommendedForum>> GetFollowedForumsAsync(long? uid = null, CancellationToken ct = default)
        {
            var result = new List<RecommendedForum>();
            var target = uid ?? Svc.Account.Uid;
            if (!Svc.Account.IsLoggedIn && uid == null) return result;
            if ((uid == null || uid == Svc.Account.Uid) && Svc.Account.IsLoggedIn)
            {
                try
                {
                    var own = await GetSelfFollowedForumsAsync(ct).ConfigureAwait(false);
                    if (own.Count > 0) return own;
                }
                catch
                {
                }
            }
            try
            {
                var myUid = Svc.Account.Uid;
                var isOther = uid.HasValue && uid.Value != 0 && uid.Value != myUid;
                var text = await _client.PostFormAsync("/c/f/forum/like", new Dictionary<string, string>
                {
                    ["page_no"] = "1",
                    ["page_size"] = "100",
                    ["uid"] = (myUid != 0 ? myUid : target).ToString(CultureInfo.InvariantCulture),
                    ["friend_uid"] = isOther ? target.ToString(CultureInfo.InvariantCulture) : "",
                    ["is_guest"] = isOther ? "1" : Svc.Account.IsLoggedIn ? "0" : "1",
                }, ct).ConfigureAwait(false);
                CheckJsonError(text);
                using var doc = JsonDocument.Parse(text);
                if (!doc.RootElement.TryGetProperty("forum_list", out var list) ||
                    list.ValueKind != JsonValueKind.Object)
                {
                    return result;
                }
                foreach (var key in new[] { "non-gconforum", "gconforum" })
                {
                    if (!list.TryGetProperty(key, out var array) || array.ValueKind != JsonValueKind.Array) continue;
                    foreach (var item in array.EnumerateArray())
                    {
                        static long GetField(JsonElement element, string name) =>
                            element.TryGetProperty(name, out var value) ? JsonLong(value) : 0;
                        var name = JsonString(item, "name");
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        result.Add(new RecommendedForum
                        {
                            Id = item.TryGetProperty("id", out var id) ? JsonLong(id) : 0,
                            Name = name,
                            Avatar = JsonString(item, "avatar"),
                            Level = (int)Math.Max(GetField(item, "level_id"),
                                Math.Max(GetField(item, "user_level"), GetField(item, "level"))),
                            IsSigned = GetField(item, "is_sign") != 0 ||
                                       GetField(item, "is_sign_in") != 0 ||
                                       GetField(item, "sign_status") != 0,
                        });
                    }
                }
            }
            catch
            {
            }
            return result;
        }

        // ---------------------------------------------------------------- discovery
        public async Task<List<RecommendedForum>> GetRecommendedForumsAsync(CancellationToken ct = default)
        {
            var text = await _client.GetStringAsync("https://tieba.baidu.com/mg/o/getForumHome?fr=search",
                "https://tieba.baidu.com/", ct).ConfigureAwait(false);
            var result = new List<RecommendedForum>();
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return result;
            if (!data.TryGetProperty("recomm_forum", out var forums) || forums.ValueKind != JsonValueKind.Array)
            {
                return result;
            }
            foreach (var item in forums.EnumerateArray())
            {
                string Get(string name) =>
                    item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
                long GetLong(string name)
                {
                    if (!item.TryGetProperty(name, out var v)) return 0;
                    if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var l)) return l;
                    return long.TryParse(v.ValueKind == JsonValueKind.String ? v.GetString() : "", out l) ? l : 0;
                }
                var name = Get("forum_name_show");
                if (string.IsNullOrWhiteSpace(name)) name = Get("forum_name");
                if (string.IsNullOrWhiteSpace(name)) continue;
                result.Add(new RecommendedForum
                {
                    Id = GetLong("forum_id"),
                    Name = name,
                    Avatar = Get("avatar"),
                    HotNum = GetLong("hot_num"),
                });
            }
            return result;
        }

        public async Task<List<HotTopic>> GetHotTopicsAsync(int limit = 8, CancellationToken ct = default)
        {
            var body = Envelope(new Dictionary<string, object>
            {
                ["call_from"] = "",
                ["list_type"] = "",
                ["need_tab_list"] = "",
                ["fid"] = 0,
            });
            var msg = await _client.CallProtoAsync("/c/f/recommend/topicList?cmd=309289",
                "tieba.topicList.TopicListRequest", "tieba.topicList.TopicListResponse", body, ct)
                .ConfigureAwait(false);
            var topics = new List<HotTopic>();
            var module = msg.GetMessage("data")?.GetMessage("topic_bang");
            if (module == null) return topics;
            foreach (var topic in module.GetMessages("topic_list"))
            {
                topics.Add(new HotTopic
                {
                    Id = topic.GetLong("topic_id"),
                    Name = topic.GetString("topic_name") ?? "",
                    DiscussNum = topic.GetLong("discuss_num"),
                    Desc = topic.GetString("topic_desc") ?? "",
                    Image = topic.GetString("topic_pic") ?? "",
                });
                if (topics.Count >= limit) break;
            }
            return topics;
        }

        private const string MobileUserAgent =
            "Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/135.0.0.0 Mobile Safari/537.36 tieba/12.35.1.0";

        public async Task<TopicPage> GetTopicDetailAsync(long topicId, string topicName, int page = 1,
            CancellationToken ct = default)
        {
            var query = "topic_id=" + topicId + "&topic_name=" + Uri.EscapeDataString(topicName ?? "") +
                        "&is_new=1&is_share=1&pn=" + page + "&rn=10&offset=" + ((page - 1) * 10) +
                        "&last_id=&derivative_to_pic_id=";
            var text = await _client.GetStringAsync("https://tieba.baidu.com/mo/q/newtopic/topicDetail?" + query,
                "https://tieba.baidu.com/", ct, MobileUserAgent).ConfigureAwait(false);
            var result = new TopicPage();
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            static string Str(JsonElement e, string name)
            {
                if (!e.TryGetProperty(name, out var v)) return "";
                return v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString(),
                    JsonValueKind.Number => v.ToString(),
                    _ => "",
                };
            }

            static long Num(JsonElement e, string name)
            {
                var s = Str(e, name);
                return long.TryParse(s, out var n) ? n : 0;
            }

            if (data.TryGetProperty("topic_info", out var info) && info.ValueKind == JsonValueKind.Object)
            {
                result.Info = new HotTopic
                {
                    Id = Num(info, "topic_id"),
                    Name = Str(info, "topic_name"),
                    Desc = Str(info, "topic_desc"),
                    DiscussNum = Num(info, "discuss_num"),
                    Image = Str(info, "topic_image"),
                };
            }

            if (data.TryGetProperty("module_info", out var modules) && modules.ValueKind == JsonValueKind.Array)
            {
                foreach (var module in modules.EnumerateArray())
                {
                    if (!module.TryGetProperty("thread_list", out var threads) ||
                        threads.ValueKind != JsonValueKind.Array) continue;
                    foreach (var thread in threads.EnumerateArray())
                    {
                        result.Threads.Add(ReadTopicThread(thread, Str, Num));
                    }
                }
            }

            if (data.TryGetProperty("has_more", out var more))
            {
                result.HasMore = more.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number => more.GetInt32() != 0,
                    _ => false,
                };
            }
            return result;
        }

        private static ThreadSummary ReadTopicThread(JsonElement thread, Func<JsonElement, string, string> str,
            Func<JsonElement, string, long> num)
        {
            var item = new ThreadSummary
            {
                Id = num(thread, "tid") != 0 ? num(thread, "tid") : num(thread, "id"),
                Title = str(thread, "title"),
                ForumId = num(thread, "forum_id"),
                ForumName = str(thread, "forum_name"),
                ReplyNum = (int)num(thread, "reply_num"),
                AgreeNum = (int)num(thread, "agree_num"),
                LastTime = str(thread, "last_time"),
                LastTimeInt = num(thread, "last_time_int"),
                CreateTime = num(thread, "create_time"),
                PreviewText = str(thread, "abstract"),
            };
            if (thread.TryGetProperty("author", out var author) && author.ValueKind == JsonValueKind.Object)
            {
                item.Author = new UserInfo
                {
                    Id = num(author, "id"),
                    Name = str(author, "name"),
                    ShowName = str(author, "name_show"),
                    Portrait = str(author, "portrait"),
                };
            }
            if (thread.TryGetProperty("media", out var media) && media.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in media.EnumerateArray())
                {
                    if (str(m, "type") != "pic") continue;
                    item.PreviewImage = FirstNonEmpty(str(m, "small_pic"), str(m, "big_pic"));
                    int.TryParse(str(m, "width"), out var w);
                    int.TryParse(str(m, "height"), out var h);
                    item.PreviewWidth = w;
                    item.PreviewHeight = h;
                    break;
                }
            }
            return item;
        }

        // ---------------------------------------------------------------- writing
        public async Task<(long threadId, long postId, string message)> AddThreadAsync(string content, string forumName,
            long forumId, string title, bool hide = false, CancellationToken ct = default)
        {
            var tbs = await GetTbsAsync(ct).ConfigureAwait(false);
            var body = Envelope(new Dictionary<string, object>
            {
                ["anonymous"] = "1",
                ["can_no_forum"] = "0",
                ["content"] = content,
                ["entrance_type"] = "0",
                ["fid"] = forumId.ToString(CultureInfo.InvariantCulture),
                ["kw"] = forumName,
                ["is_hide"] = hide ? "1" : "0",
                ["is_ntitle"] = string.IsNullOrWhiteSpace(title) ? "1" : "0",
                ["is_pictxt"] = content.Contains("[img]") ? "1" : "0",
                ["is_show_bless"] = 0,
                ["name_show"] = _client.Account.UserName ?? "",
                ["new_vcode"] = "1",
                ["show_custom_figure"] = 0,
                ["takephoto_num"] = "0",
                ["title"] = title ?? "",
                ["vcode_tag"] = "12",
                ["tbs"] = tbs,
            });
            var msg = await _client.CallProtoAsync("/c/c/thread/add?cmd=309730&format=protobuf",
                "tieba.addThread.AddThreadRequest", "tieba.addThread.AddThreadResponse", body, ct).ConfigureAwait(false);
            var data = msg.GetMessage("data");
            long.TryParse(data?.GetString("tid"), out var threadId);
            long.TryParse(data?.GetString("pid"), out var postId);
            var message = data?.GetString("msg");
            return (threadId, postId, string.IsNullOrWhiteSpace(message) ? "发布成功" : message);
        }

        public async Task AddPollPostAsync(long threadId, long forumId, int optionId, CancellationToken ct = default)
        {
            var body = Envelope(new Dictionary<string, object>
            {
                ["thread_id"] = threadId,
                ["options"] = optionId.ToString(CultureInfo.InvariantCulture),
                ["forum_id"] = forumId,
            });
            var msg = await _client.CallProtoAsync("/c/c/post/addPollPost?cmd=309006&format=protobuf",
                "tieba.addPollPost.AddPollPostRequest", "tieba.addPollPost.AddPollPostReponse", body, ct)
                .ConfigureAwait(false);
            var data = msg.GetMessage("data");
            if (data != null && data.GetInt("error_code") != 0)
            {
                var message = data.GetString("error_msg");
                throw new TiebaException(data.GetInt("error_code"),
                    string.IsNullOrWhiteSpace(message) ? "投票失败" : message);
            }
        }

        public async Task SetThreadVisibilityAsync(long forumId, string forumName, long threadId, bool hidden,
            CancellationToken ct = default)
        {
            var firstPostId = 0L;
            try
            {
                var threadPage = await GetThreadAsync(threadId, 1, false, false, 0, ct).ConfigureAwait(false);
                if (threadPage != null)
                {
                    if (forumId == 0) forumId = threadPage.Forum?.Id ?? threadPage.Thread?.ForumId ?? 0;
                    firstPostId = threadPage.Posts.FirstOrDefault()?.Id ?? 0;
                }
            }
            catch
            {
            }
            if (firstPostId == 0) firstPostId = threadId;
            var form = new Dictionary<string, string>
            {
                ["BDUSS"] = Svc.Account.Bduss ?? "",
                ["forum_id"] = forumId.ToString(CultureInfo.InvariantCulture),
                ["is_hide"] = hidden ? "1" : "0",
                ["post_id"] = firstPostId.ToString(CultureInfo.InvariantCulture),
                ["thread_id"] = threadId.ToString(CultureInfo.InvariantCulture),
            };
            var text = await _client.PostFormAsync("/c/c/thread/setPrivacy", form, ct).ConfigureAwait(false);
            CheckJsonError(text);
        }

        public async Task RecoverThreadAsync(long forumId, long threadId, CancellationToken ct = default)
        {
            var tbs = await GetTbsAsync(ct).ConfigureAwait(false);
            var form = new Dictionary<string, string>
            {
                ["tbs"] = tbs,
                ["fn"] = "-",
                ["fid"] = forumId.ToString(CultureInfo.InvariantCulture),
                ["tid_list[]"] = threadId.ToString(CultureInfo.InvariantCulture),
                ["pid_list[]"] = "0",
                ["type_list[]"] = "0",
                ["is_frs_mask_list[]"] = "0",
            };
            var text = await _client.PostFormAsync("/mo/q/bawurecoverthread", form, ct).ConfigureAwait(false);
            CheckJsonError(text);
        }

        public async Task DeleteThreadAsync(long forumId, string forumName, long threadId, bool myThread,
            CancellationToken ct = default)
        {
            var tbs = await GetTbsAsync(ct).ConfigureAwait(false);
            var form = new Dictionary<string, string>
            {
                ["fid"] = forumId.ToString(CultureInfo.InvariantCulture),
                ["word"] = forumName,
                ["z"] = threadId.ToString(CultureInfo.InvariantCulture),
                ["tbs"] = tbs,
                ["src"] = "1",
                ["is_vipdel"] = "0",
                ["delete_my_thread"] = myThread ? "1" : "0",
                ["is_frs_mask"] = "0",
            };
            var text = await _client.PostFormAsync("/c/c/bawu/delthread", form, ct).ConfigureAwait(false);
            CheckJsonError(text);
        }

        public async Task DeletePostAsync(long forumId, string forumName, long threadId, long postId, bool isFloor,
            bool myPost, CancellationToken ct = default)
        {
            var tbs = await GetTbsAsync(ct).ConfigureAwait(false);
            var form = new Dictionary<string, string>
            {
                ["fid"] = forumId.ToString(CultureInfo.InvariantCulture),
                ["word"] = forumName,
                ["z"] = threadId.ToString(CultureInfo.InvariantCulture),
                ["pid"] = postId.ToString(CultureInfo.InvariantCulture),
                ["isfloor"] = isFloor ? "1" : "0",
                ["src"] = "1",
                ["is_vipdel"] = "0",
                ["delete_my_post"] = myPost ? "1" : "0",
                ["tbs"] = tbs,
            };
            var text = await _client.PostFormAsync("/c/c/bawu/delpost", form, ct).ConfigureAwait(false);
            CheckJsonError(text);
        }

        public async Task<ForumInfo> GetForumDetailAsync(long forumId, CancellationToken ct = default)
        {
            var body = Envelope(new Dictionary<string, object> { ["forum_id"] = forumId });
            var msg = await _client.CallProtoAsync("/c/f/forum/getforumdetail?cmd=303021&format=protobuf",
                "tieba.getForumDetail.GetForumDetailRequest", "tieba.getForumDetail.GetForumDetailResponse",
                body, ct).ConfigureAwait(false);
            var info = msg.GetMessage("data")?.GetMessage("forum_info");
            if (info == null) return null;
            var content = info.GetMessage("content");
            return new ForumInfo
            {
                Id = info.GetLong("forum_id"),
                Name = info.GetString("forum_name") ?? "",
                Avatar = info.GetString("avatar") ?? "",
                Slogan = info.GetString("slogan") ?? "",
                Description = content?.GetString("text") ?? "",
                MemberNum = info.GetInt("member_count"),
                ThreadNum = info.GetInt("thread_count"),
                HotText = info.GetString("hot_text") ?? "",
            };
        }

        public static string LastUserInfoError { get; private set; }

        public async Task<(int fans, int follows, int threads, int posts, long agree)> GetUserStatsAsync(
            long uid, CancellationToken ct = default)
        {
            var fans = 0;
            var follows = 0;
            var threads = 0;
            var posts = 0;
            long agree = 0;
            try
            {
                var fansText = await _client.PostFormAsync("/c/u/fans/page", new Dictionary<string, string>
                {
                    ["uid"] = uid.ToString(CultureInfo.InvariantCulture),
                    ["pn"] = "1",
                }, ct).ConfigureAwait(false);
                using (var doc = JsonDocument.Parse(fansText))
                {
                    if (doc.RootElement.TryGetProperty("page", out var page) &&
                        page.TryGetProperty("total_count", out var total))
                    {
                        fans = (int)Math.Min(int.MaxValue, JsonLong(total));
                    }
                }
            }
            catch
            {
            }
            try
            {
                var followText = await _client.PostFormAsync("/c/u/follow/followList", new Dictionary<string, string>
                {
                    ["uid"] = uid.ToString(CultureInfo.InvariantCulture),
                    ["pn"] = "1",
                }, ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(followText);
                if (doc.RootElement.TryGetProperty("total_follow_num", out var total))
                {
                    follows = (int)Math.Min(int.MaxValue, JsonLong(total));
                }
            }
            catch
            {
            }
            try
            {
                var threadList = await GetUserPostsAsync(uid, 1, true, ct).ConfigureAwait(false);
                threads = threadList.Count;
                agree += threadList.Sum(t => (long)t.AgreeNum);
            }
            catch
            {
            }
            try
            {
                var postList = await GetUserPostsAsync(uid, 1, false, ct).ConfigureAwait(false);
                posts = postList.Count;
                agree += postList.Sum(t => (long)t.AgreeNum);
            }
            catch
            {
            }
            return (fans, follows, threads, posts, agree);
        }

        public async Task<UserInfo> GetImUserAsync(long uid, CancellationToken ct = default)
        {
            try
            {
                var url = "https://tieba.baidu.com/im/pcmsg/query/getUserInfo?chatUid=" +
                          uid.ToString(CultureInfo.InvariantCulture);
                var text = await _client.GetStringAsync(url, "https://tieba.baidu.com/", ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(text);
                if (!doc.RootElement.TryGetProperty("chatUser", out var user) || user.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }
                var name = FirstNonEmpty(JsonString(user, "show_nickname"), JsonString(user, "uname"));
                return new UserInfo
                {
                    Id = uid,
                    Name = JsonString(user, "uname"),
                    ShowName = name,
                    Portrait = JsonString(user, "portrait"),
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<UserInfo> GetUserInfoAsync(long uid, CancellationToken ct = default)
        {
            LastUserInfoError = null;
            try
            {
                var body = Envelope(new Dictionary<string, object> { ["uid"] = uid });
                SetCommonVersion(body, "12.52.1.0");
                var previousVersion = _client.VersionOverride;
                _client.VersionOverride = "12.52.1.0";
                ProtoMessage msg;
                try
                {
                    msg = await _client.CallProtoAsync("/c/u/user/getuserinfo?cmd=303024&format=protobuf",
                        "tieba.getUserInfo.GetUserInfoRequest", "tieba.getUserInfo.GetUserInfoResponse", body, ct)
                        .ConfigureAwait(false);
                }
                finally
                {
                    _client.VersionOverride = previousVersion;
                }
                var user = msg.GetMessage("data")?.GetMessage("user");
                return user == null ? null : UserInfo.From(user);
            }
            catch (Exception ex)
            {
                LastUserInfoError = ex.ToString();
                return null;
            }
        }

        public async Task<ForumRuleDetail> GetForumRuleAsync(long forumId, CancellationToken ct = default)
        {
            var body = Envelope(new Dictionary<string, object> { ["forum_id"] = forumId });
            var msg = await _client.CallProtoAsync("/c/f/forum/forumRuleDetail?cmd=309690&format=protobuf",
                "tieba.forumRuleDetail.ForumRuleDetailRequest",
                "tieba.forumRuleDetail.ForumRuleDetailResponse", body, ct).ConfigureAwait(false);
            var data = msg.GetMessage("data");
            if (data == null) return null;
            var detail = new ForumRuleDetail
            {
                Title = data.GetString("title") ?? "",
                Preface = data.GetString("preface") ?? "",
                PublishTime = data.GetString("publish_time") ?? "",
            };
            foreach (var rule in data.GetMessages("rules"))
            {
                var item = new ForumRuleItem { Title = rule.GetString("title") ?? "" };
                var parts = new List<string>();
                foreach (var content in rule.GetMessages("content"))
                {
                    var text = content.GetString("text");
                    if (!string.IsNullOrWhiteSpace(text)) parts.Add(text);
                    var link = content.GetString("link");
                    if (!string.IsNullOrWhiteSpace(link) && !parts.Contains(link)) parts.Add(link);
                }
                item.Text = string.Join("\n", parts);
                detail.Rules.Add(item);
            }
            return detail;
        }

        public async Task<List<BawuMember>> GetBawuTeamAsync(long forumId, CancellationToken ct = default)
        {
            var body = Envelope(new Dictionary<string, object> { ["forum_id"] = forumId });
            var msg = await _client.CallProtoAsync("/c/f/forum/getBawuInfo?cmd=301007&format=protobuf",
                "tieba.getBawuInfo.GetBawuInfoRequest", "tieba.getBawuInfo.GetBawuInfoResponse", body, ct)
                .ConfigureAwait(false);
            var team = msg.GetMessage("data")?.GetMessage("bawu_team_info");
            var members = new List<BawuMember>();
            if (team == null) return members;
            foreach (var role in team.GetMessages("bawu_team_list"))
            {
                var roleName = role.GetString("role_name") ?? "";
                foreach (var info in role.GetMessages("role_info"))
                {
                    members.Add(new BawuMember
                    {
                        Uid = info.GetLong("user_id"),
                        Name = FirstNonEmpty(info.GetString("name_show"), info.GetString("user_name")),
                        RoleName = roleName,
                        Portrait = info.GetString("portrait") ?? "",
                        Level = info.GetInt("user_level"),
                    });
                }
            }
            return members;
        }

        public async Task SignForumAsync(long forumId, string forumName, CancellationToken ct = default)
        {
            var tbs = await GetTbsAsync(ct).ConfigureAwait(false);
            var form = new Dictionary<string, string>
            {
                ["fid"] = forumId.ToString(CultureInfo.InvariantCulture),
                ["kw"] = forumName,
                ["tbs"] = tbs,
                ["ie"] = "utf-8",
                ["_client_version"] = "12.35.1.0",
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            };
            try
            {
                var text = await _client.PostFormAsync("/c/c/forum/sign", form, ct).ConfigureAwait(false);
                CheckJsonError(text);
                return;
            }
            catch
            {
                var web = await _client.PostWebFormAsync("https://tieba.baidu.com/sign/add",
                    new Dictionary<string, string>
                    {
                        ["ie"] = "utf-8",
                        ["tbs"] = tbs,
                        ["kw"] = forumName,
                    }, ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(web);
                var no = doc.RootElement.TryGetProperty("no", out var n) ? JsonInt(n) : -1;
                if (no != 0)
                {
                    var msg = JsonString(doc.RootElement, "error");
                    throw new TiebaException(no, string.IsNullOrWhiteSpace(msg) ? "签到失败" : msg);
                }
            }
        }

        public async Task LikeForumAsync(long forumId, string forumName, bool like, CancellationToken ct = default)
        {
            var tbs = await GetTbsAsync(ct).ConfigureAwait(false);
            var form = new Dictionary<string, string>
            {
                ["fid"] = forumId.ToString(CultureInfo.InvariantCulture),
                ["kw"] = forumName,
                ["tbs"] = tbs,
            };
            var text = await _client.PostFormAsync(like ? "/c/c/forum/like" : "/c/c/forum/unfavolike", form, ct)
                .ConfigureAwait(false);
            CheckJsonError(text);
        }

        private static void CheckJsonError(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.TryGetProperty("error_code", out var code) && JsonInt(code) != 0)
                {
                    var msg = JsonString(root, "error_msg");
                    var userMsg = JsonString(root, "user_msg");
                    throw new TiebaException(JsonInt(code), string.IsNullOrWhiteSpace(userMsg) ? msg : userMsg);
                }
                if (root.TryGetProperty("no", out var no) && JsonInt(no) != 0)
                {
                    var msg = JsonString(root, "error");
                    throw new TiebaException(JsonInt(no), string.IsNullOrWhiteSpace(msg) ? "操作失败" : msg);
                }
            }
            catch (JsonException)
            {
                // non-JSON payloads are treated as success unless HTTP failed
            }
        }

        private static int JsonInt(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt32(out var number) ? number : 0,
            JsonValueKind.String => int.TryParse(element.GetString(), out var parsed) ? parsed : 0,
            _ => 0,
        };

        private static double ParseDouble(string text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

        private static long FirstLong(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (element.TryGetProperty(name, out var value))
                {
                    var parsed = JsonLong(value);
                    if (parsed != 0) return parsed;
                }
            }
            return 0;
        }

        private static long JsonLong(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt64(out var number) ? number : 0,
            JsonValueKind.String => long.TryParse(element.GetString(), out var parsed) ? parsed : 0,
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => 0,
        };

        private static string JsonString(JsonElement parent, string name) =>
            parent.TryGetProperty(name, out var value)
                ? value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString() ?? "",
                    JsonValueKind.Number => value.ToString(),
                    _ => "",
                }
                : "";

        private static string FirstNonEmpty(params string[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

        // ---------------------------------------------------------------- messages / favorites
        public async Task<List<MessageItem>> GetMessagesAsync(string type, int page = 1,
            CancellationToken ct = default)
        {
            var path = type switch
            {
                "at" => "/c/u/feed/atme",
                "agree" => "/c/u/feed/agreeme",
                _ => "/c/u/feed/replyme",
            };
            var text = await _client.PostFormAsync(path, new Dictionary<string, string>
            {
                ["pn"] = page.ToString(CultureInfo.InvariantCulture),
            }, ct).ConfigureAwait(false);
            CheckJsonError(text);
            var result = new List<MessageItem>();
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (type == "agree")
            {
                if (root.TryGetProperty("agree_list", out var agreeList) && agreeList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in agreeList.EnumerateArray())
                    {
                        var agreeer = item.TryGetProperty("agreeer", out var a) && a.ValueKind == JsonValueKind.Object ? a : default;
                        var threadInfo = item.TryGetProperty("thread_info", out var ti) && ti.ValueKind == JsonValueKind.Object ? ti : default;
                        var textContent = "";
                        long postId = 0;
                        if (item.TryGetProperty("post_info", out var postInfo) && postInfo.ValueKind == JsonValueKind.Object)
                        {
                            postId = postInfo.TryGetProperty("id", out var pid) ? JsonLong(pid) : 0;
                            if (postInfo.TryGetProperty("content", out var fragments) && fragments.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var fragment in fragments.EnumerateArray())
                                {
                                    textContent += JsonString(fragment, "text");
                                }
                            }
                        }
                        var message = new MessageItem
                        {
                            ThreadId = threadInfo.ValueKind == JsonValueKind.Object ? (threadInfo.TryGetProperty("id", out var tid) ? JsonLong(tid) : 0) : 0,
                            PostId = postId,
                            Title = threadInfo.ValueKind == JsonValueKind.Object ? JsonString(threadInfo, "title") : "",
                            Content = textContent,
                            ForumName = threadInfo.ValueKind == JsonValueKind.Object ? JsonString(threadInfo, "fname") : "",
                            UserName = agreeer.ValueKind == JsonValueKind.Object
                                ? FirstNonEmpty(JsonString(agreeer, "name_show"), JsonString(agreeer, "name"))
                                : "",
                            Portrait = agreeer.ValueKind == JsonValueKind.Object ? JsonString(agreeer, "portrait") : "",
                            Time = item.TryGetProperty("time", out var at) ? JsonLong(at) : 0,
                            IsAgree = true,
                        };
                        result.Add(message);
                    }
                }
                return result;
            }
            foreach (var listName in new[] { "reply_list", "at_list", "agree_list" })
            {
                if (!root.TryGetProperty(listName, out var list) || list.ValueKind != JsonValueKind.Array) continue;
                foreach (var item in list.EnumerateArray())
                {
                    string Get(string name) =>
                        item.TryGetProperty(name, out var v)
                            ? v.ValueKind switch
                            {
                                JsonValueKind.String => v.GetString() ?? "",
                                JsonValueKind.Number => v.ToString(),
                                _ => "",
                            }
                            : "";
                    long GetLong(string name) =>
                        item.TryGetProperty(name, out var v) ? JsonLong(v) : 0;
                    var replyer = item.TryGetProperty("replyer", out var r) && r.ValueKind == JsonValueKind.Object ? r : default;
                    var message = new MessageItem
                    {
                        ThreadId = GetLong("thread_id"),
                        PostId = GetLong("post_id"),
                        Title = Get("title"),
                        Content = Get("content"),
                        ForumName = Get("fname"),
                        Quote = Get("quote_content"),
                        UserName = replyer.ValueKind == JsonValueKind.Object
                            ? FirstNonEmpty(JsonString(replyer, "name_show"), JsonString(replyer, "name"))
                            : "",
                        Portrait = replyer.ValueKind == JsonValueKind.Object ? JsonString(replyer, "portrait") : "",
                        Time = GetLong("time"),
                        IsReply = listName == "reply_list",
                        IsAt = listName == "at_list",
                        IsAgree = listName == "agree_list",
                    };
                    result.Add(message);
                }
            }
            return result;
        }

        public async Task<List<ThreadSummary>> GetFavoritesAsync(int page = 0, CancellationToken ct = default)
        {
            var text = await _client.PostFormAsync("/c/f/post/threadstore", new Dictionary<string, string>
            {
                ["rn"] = "20",
                ["offset"] = (page * 20).ToString(CultureInfo.InvariantCulture),
                ["user_id"] = Svc.Account.Uid.ToString(CultureInfo.InvariantCulture),
                ["stoken"] = Svc.Account.Stoken ?? "",
            }, ct).ConfigureAwait(false);
            CheckJsonError(text);
            var result = new List<ThreadSummary>();
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("store_thread", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return result;
            }
            foreach (var item in list.EnumerateArray())
            {
                string Get(string name) =>
                    item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
                var thread = new ThreadSummary
                {
                    Id = long.TryParse(Get("thread_id"), out var tid) ? tid : 0,
                    Title = Get("title"),
                    ForumName = Get("forum_name"),
                    LastTime = Get("last_time"),
                    ReplyNum = int.TryParse(Get("count"), out var count) ? count : 0,
                };
                if (item.TryGetProperty("author", out var author) && author.ValueKind == JsonValueKind.Object)
                {
                    thread.Author = new UserInfo
                    {
                        Id = long.TryParse(JsonString(author, "lz_uid"), out var id) ? id : 0,
                        Name = JsonString(author, "name"),
                        ShowName = JsonString(author, "name_show"),
                        Portrait = JsonString(author, "user_portrait"),
                    };
                }
                result.Add(thread);
            }
            return result;
        }

        public async Task FollowUserAsync(string portrait, bool follow, CancellationToken ct = default)
        {
            if (!Svc.Account.IsLoggedIn) throw new TiebaException(-1, "請先登錄");
            if (string.IsNullOrWhiteSpace(portrait)) throw new TiebaException(-1, "用戶信息不完整");
            if (portrait.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var marker = portrait.IndexOf("/item/", StringComparison.OrdinalIgnoreCase);
                portrait = marker >= 0
                    ? portrait[(marker + 6)..]
                    : portrait[(portrait.LastIndexOf('/') + 1)..];
            }
            var tbs = await GetTbsAsync(ct).ConfigureAwait(false);
            var form = new Dictionary<string, string>
            {
                ["portrait"] = portrait,
                ["tbs"] = tbs,
                ["_client_version"] = "11.10.8.6",
                ["authsid"] = "null",
                ["stoken"] = Svc.Account.Stoken ?? "",
                ["from_type"] = "2",
                ["in_live"] = "0",
            };
            if (!follow) form["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
            var text = await _client.PostFormAsync(follow ? "/c/c/user/follow" : "/c/c/user/unfollow", form, ct)
                .ConfigureAwait(false);
            CheckJsonError(text);
        }

        public async Task<string> GetReportUrlAsync(long postId, CancellationToken ct = default)
        {
            var form = new Dictionary<string, string>
            {
                ["category"] = "1",
                ["pid"] = postId.ToString(CultureInfo.InvariantCulture),
                ["stoken"] = Svc.Account.Stoken ?? "",
            };
            var text = await _client.PostFormAsync("/c/f/ueg/checkjubao", form, ct).ConfigureAwait(false);
            CheckJsonError(text);
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("url", out var url))
            {
                return url.GetString() ?? "";
            }
            return "";
        }

        public async Task<List<UserInfo>> GetFollowsAsync(long uid, int page = 1, CancellationToken ct = default)
        {
            var result = new List<UserInfo>();
            try
            {
                var text = await _client.PostFormAsync("/c/u/follow/followList", new Dictionary<string, string>
                {
                    ["uid"] = uid.ToString(CultureInfo.InvariantCulture),
                    ["pn"] = page.ToString(CultureInfo.InvariantCulture),
                }, ct).ConfigureAwait(false);
                CheckJsonError(text);
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("follow_list", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in list.EnumerateArray())
                    {
                        result.Add(new UserInfo
                        {
                            Id = FirstLong(item, "id", "user_id", "uid", "tieba_uid"),
                            Name = FirstNonEmpty(JsonString(item, "name"), JsonString(item, "user_name")),
                            ShowName = FirstNonEmpty(JsonString(item, "show_nickname"),
                                JsonString(item, "user_nickname"), JsonString(item, "name_show"), JsonString(item, "name")),
                            Portrait = JsonString(item, "portrait"),
                            HasConcerned = FirstLong(item, "has_concerned", "is_follow") == 1,
                        });
                    }
                }
            }
            catch
            {
            }
            if (uid == Svc.Account.Uid) FollowCache.AddRange(result);
            return result;
        }

        public async Task<List<UserInfo>> GetFansAsync(long uid, int page = 1, CancellationToken ct = default)
        {
            var result = new List<UserInfo>();
            try
            {
                var text = await _client.PostFormAsync("/c/u/fans/page", new Dictionary<string, string>
                {
                    ["uid"] = uid.ToString(CultureInfo.InvariantCulture),
                    ["pn"] = page.ToString(CultureInfo.InvariantCulture),
                }, ct).ConfigureAwait(false);
                CheckJsonError(text);
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("user_list", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in list.EnumerateArray())
                    {
                        result.Add(new UserInfo
                        {
                            Id = FirstLong(item, "id", "user_id", "uid", "tieba_uid"),
                            Name = FirstNonEmpty(JsonString(item, "name"), JsonString(item, "user_name")),
                            ShowName = FirstNonEmpty(JsonString(item, "show_nickname"),
                                JsonString(item, "user_nickname"), JsonString(item, "name_show"), JsonString(item, "name")),
                            Portrait = JsonString(item, "portrait"),
                        });
                    }
                }
            }
            catch
            {
            }
            return result;
        }

        public async Task<bool> IsThreadStoredAsync(long threadId, CancellationToken ct = default)
        {
            try
            {
                for (var page = 0; page < 5; page++)
                {
                    var list = await GetFavoritesAsync(page, ct).ConfigureAwait(false);
                    if (list.Any(t => t.Id == threadId)) return true;
                    if (list.Count < 20) break;
                }
            }
            catch
            {
            }
            return false;
        }

        public async Task AddFavoriteAsync(long threadId, long postId, CancellationToken ct = default)
        {
            var tbs = await GetTbsAsync(ct).ConfigureAwait(false);
            var data = "[{\"tid\":\"" + threadId.ToString(CultureInfo.InvariantCulture) +
                       "\",\"pid\":\"" + postId.ToString(CultureInfo.InvariantCulture) + "\",\"status\":1}]";
            var text = await _client.PostFormAsync("/c/c/post/addstore", new Dictionary<string, string>
            {
                ["data"] = data,
                ["tbs"] = tbs,
                ["stoken"] = Svc.Account.Stoken ?? "",
            }, ct).ConfigureAwait(false);
            CheckJsonError(text);
        }

        public async Task RemoveFavoriteAsync(long threadId, long forumId, CancellationToken ct = default)
        {
            var tbs = await GetTbsAsync(ct).ConfigureAwait(false);
            var text = await _client.PostFormAsync("/c/c/post/rmstore", new Dictionary<string, string>
            {
                ["tid"] = threadId.ToString(CultureInfo.InvariantCulture),
                ["fid"] = forumId.ToString(CultureInfo.InvariantCulture),
                ["tbs"] = tbs,
                ["stoken"] = Svc.Account.Stoken ?? "",
                ["user_id"] = Svc.Account.Uid.ToString(CultureInfo.InvariantCulture),
            }, ct).ConfigureAwait(false);
            CheckJsonError(text);
        }

        private static void CollectUserPostContent(JsonElement array, ref string text, ref string image)
        {
            foreach (var element in array.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Array)
                {
                    CollectUserPostContent(element, ref text, ref image);
                    continue;
                }
                if (element.ValueKind != JsonValueKind.Object) continue;
                if (element.TryGetProperty("post_content", out var postContent) &&
                    postContent.ValueKind == JsonValueKind.Array)
                {
                    CollectUserPostContent(postContent, ref text, ref image);
                    continue;
                }
                var type = JsonString(element, "type");
                if (type == "3" || type == "20")
                {
                    if (string.IsNullOrEmpty(image)) image = JsonString(element, "src");
                    continue;
                }
                var fragment = JsonString(element, "text");
                if (string.IsNullOrEmpty(fragment)) fragment = JsonString(element, "c");
                if (!string.IsNullOrEmpty(fragment)) text += fragment;
            }
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            var sb = new System.Text.StringBuilder(html.Length);
            var inTag = false;
            foreach (var ch in html)
            {
                if (ch == '<') inTag = true;
                else if (ch == '>') inTag = false;
                else if (!inTag) sb.Append(ch);
            }
            return WebUtility.HtmlDecode(sb.ToString()).Trim();
        }
    }
}

