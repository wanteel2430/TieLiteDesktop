using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Views
{
    /// <summary>用户主页：资料卡 + 主题贴 / 回复 / 关注 / 粉丝 / 关注的吧。</summary>
    public sealed class ProfileView : UserControl, IRefreshable
    {
        private readonly long _uid;
        private readonly string _name;
        private readonly StackPanel _root;
        private readonly ContentControl _headerHost = new();
        private readonly StackPanel _tabsHost = new();
        private readonly StackPanel _list = new()
        {
            Margin = new Thickness(0, 10, 0, 0),
            MinHeight = 520,
        };
        private UserInfo _user;
        private bool _loading;
        private string _tab = "threads";

        public ProfileView(long uid, string name)
        {
            _uid = uid;
            _name = name;
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            _root = new StackPanel
            {
                MaxWidth = 1000,
                MinWidth = 720,
                Margin = new Thickness(28, 8, 28, 40),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _root.Children.Add(_headerHost);
            _root.Children.Add(_tabsHost);
            _root.Children.Add(_list);
            scroll.Content = _root;
            Content = scroll;
            Loaded += async (_, _) => await LoadAsync();
        }

        private bool IsSelf => _uid == Svc.Account.Uid || _uid == 0;

        private async Task LoadAsync()
        {
            if (_loading) return;
            _loading = true;
            _list.Children.Clear();
            _headerHost.Content = null;
            _list.Children.Add(HomeView.Skeleton(2));
            try
            {
                if (Svc.DemoMode)
                {
                    var demo = DemoData.Thread();
                    _user = demo.Thread.Author ?? new UserInfo { Id = _uid, ShowName = _name };
                    if (_user.ThreadNum == 0) _user.ThreadNum = Math.Max(1, demo.Thread.ReplyNum / 10);
                    if (_user.PostNum == 0) _user.PostNum = demo.Thread.ReplyNum;
                    if (_user.FansNum == 0) _user.FansNum = 284;
                    if (_user.AgreeNum == 0) _user.AgreeNum = demo.Thread.AgreeNum;
                }
                else
                {
                    var (user, _) = await Svc.Api.GetProfileAsync(_uid);
                    var protoUser = await Svc.Api.GetProfileProtoAsync(_uid);
                    var info = protoUser ?? await Svc.Api.GetUserInfoAsync(_uid);
                    if (info != null && info.Id > 0)
                    {
                        if (user == null || user.Id == 0) user = info;
                        else
                        {
                            if (string.IsNullOrWhiteSpace(user.ShowName) || user.ShowName.StartsWith("贴吧用户", StringComparison.Ordinal))
                            {
                                user.ShowName = info.ShowName;
                                user.Name = info.Name;
                            }
                            if (string.IsNullOrWhiteSpace(user.Portrait)) user.Portrait = info.Portrait;
                            if (user.FansNum <= 0) user.FansNum = info.FansNum;
                            if (user.FollowNum <= 0) user.FollowNum = info.FollowNum;
                            if (user.PostNum <= 0) user.PostNum = info.PostNum;
                            if (user.ThreadNum <= 0) user.ThreadNum = info.ThreadNum;
                            if (user.AgreeNum <= 0) user.AgreeNum = info.AgreeNum;
                            if (user.TbAge <= 0) user.TbAge = info.TbAge;
                            if (string.IsNullOrWhiteSpace(user.Intro)) user.Intro = info.Intro;
                            if (string.IsNullOrWhiteSpace(user.IpAddress)) user.IpAddress = info.IpAddress;
                        }
                    }
                    if (user != null && protoUser != null && string.IsNullOrWhiteSpace(user.IpAddress))
                    {
                        user.IpAddress = protoUser.IpAddress;
                    }
                    if (user != null && FollowCache.IsFollowing(_uid)) user.HasConcerned = true;
                    var cached = UserCache.Get(_uid);
                    if (user == null) user = cached;
                    if (user != null && user.Id == 0) user.Id = _uid;
                    if (cached != null && user != null)
                    {
                        if (string.IsNullOrWhiteSpace(user.Portrait)) user.Portrait = cached.Portrait;
                        if (string.IsNullOrWhiteSpace(user.TiebaUid)) user.TiebaUid = cached.TiebaUid;
                        if (user.TbAge <= 0) user.TbAge = cached.TbAge;
                        if (user.FansNum <= 0) user.FansNum = cached.FansNum;
                        if (user.FollowNum <= 0) user.FollowNum = cached.FollowNum;
                        if (user.PostNum <= 0) user.PostNum = cached.PostNum;
                        if (user.ThreadNum <= 0) user.ThreadNum = cached.ThreadNum;
                        if (user.AgreeNum <= 0) user.AgreeNum = cached.AgreeNum;
                        if (string.IsNullOrWhiteSpace(user.Intro)) user.Intro = cached.Intro;
                        if (string.IsNullOrWhiteSpace(user.IpAddress)) user.IpAddress = cached.IpAddress;
                        if (string.IsNullOrWhiteSpace(user.ShowName) ||
                            user.ShowName.StartsWith("贴吧用户", StringComparison.Ordinal))
                        {
                            if (!string.IsNullOrWhiteSpace(cached.ShowName)) user.ShowName = cached.ShowName;
                            if (!string.IsNullOrWhiteSpace(cached.Name)) user.Name = cached.Name;
                        }
                    }
                    if (user != null && user.Id > 0 &&
                        user.FansNum == 0 && user.FollowNum == 0 && user.PostNum == 0 && user.ThreadNum == 0)
                    {
                        var stats = await Svc.Api.GetUserStatsAsync(user.Id);
                        user.FansNum = stats.fans;
                        user.FollowNum = stats.follows;
                        if (user.ThreadNum == 0) user.ThreadNum = stats.threads;
                        if (user.PostNum == 0) user.PostNum = stats.posts;
                        if (user.AgreeNum == 0) user.AgreeNum = (int)Math.Min(int.MaxValue, stats.agree);
                    }
                    if (user == null || (user.Id == 0 && string.IsNullOrWhiteSpace(user.Portrait)))
                    {
                        user = _uid == Svc.Account.Uid || _uid == 0
                            ? new UserInfo
                            {
                                Id = Svc.Account.Uid,
                                Name = Svc.Account.UserName,
                                ShowName = Svc.Account.UserName,
                                Portrait = Svc.Account.Portrait,
                            }
                            : cached ?? new UserInfo
                            {
                                Id = _uid,
                                Name = _name,
                                ShowName = _name,
                            };
                    }
                    _user = user;
                }
                await RenderAsync();
                Svc.Window?.SetSubtitle(_user?.DisplayName ?? _name);
            }
            catch (Exception ex)
            {
                _list.Children.Clear();
                _list.Children.Add(HomeView.ErrorCard(ex.Message, LoadAsync));
            }
            finally
            {
                _loading = false;
            }
        }

        private async Task RenderAsync()
        {
            _headerHost.Content = _user != null ? BuildHeader(_user) : null;
            if (_tabsHost.Children.Count == 0)
            {
                _tabsHost.Children.Add(BuildTabs());
            }
            else
            {
                foreach (var child in _tabsHost.Children.OfType<RadioButton>())
                {
                    child.IsChecked = string.Equals(child.Tag as string, _tab, StringComparison.Ordinal);
                }
            }
            var list = _list;
            list.Children.Clear();
            try
            {
                if (Svc.DemoMode)
                {
                    if (_tab == "threads" || _tab == "replies")
                    {
                        var demo = DemoData.Thread();
                        list.Children.Add(Ui.Card(new ThreadRow(new ThreadSummary
                        {
                            Id = demo.Thread.Id,
                            Title = demo.Thread.Title,
                            ForumName = demo.Thread.ForumName,
                            ReplyNum = demo.Thread.ReplyNum,
                            Author = _user,
                            LastTime = demo.Thread.LastTime,
                            PreviewText = demo.Posts.FirstOrDefault()?.PlainText ?? "",
                        }, true), 4, 16));
                    }
                    else if (_tab == "forums")
                    {
                        var demoForum = DemoData.Forum();
                        var wrap = new WrapPanel();
                        foreach (var name in new[] { demoForum.Forum.Name, "孙笑川", "原神", "steam", "显卡", "李毅" }
                                     .Where(n => !string.IsNullOrWhiteSpace(n)))
                        {
                            var chip = Ui.Button(name, false, 12);
                            chip.Margin = new Thickness(0, 0, 8, 8);
                            wrap.Children.Add(chip);
                        }
                        list.Children.Add(wrap);
                    }
                    return;
                }
                switch (_tab)
                {
                    case "replies":
                    case "threads":
                    {
                        var posts = await Svc.Api.GetUserPostsAsync(_uid, 1, _tab == "threads");
                        if (_user != null && string.IsNullOrWhiteSpace(_user.IpAddress))
                        {
                            var ip = posts.Select(post => post.Author?.IpAddress)
                                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                            if (string.IsNullOrWhiteSpace(ip))
                            {
                                foreach (var post in posts.Take(3))
                                {
                                    try
                                    {
                                        var page = await Svc.Api.GetThreadAsync(post.Id, 1, false, false, 0);
                                        ip = page?.Posts.FirstOrDefault(item => item.Author?.Id == _uid)?.Author?.IpAddress;
                                        if (string.IsNullOrWhiteSpace(ip))
                                        {
                                            ip = page?.Posts.FirstOrDefault()?.Author?.IpAddress;
                                        }
                                        if (!string.IsNullOrWhiteSpace(ip)) break;
                                    }
                                    catch
                                    {
                                    }
                                }
                            }
                            if (!string.IsNullOrWhiteSpace(ip))
                            {
                                _user.IpAddress = ip;
                                _headerHost.Content = BuildHeader(_user);
                            }
                        }
                        if (posts.Count == 0) list.Children.Add(HomeView.EmptyCard(_tab == "threads" ? "暂无主题贴" : "暂无回复"));
                        foreach (var thread in posts)
                        {
                            var hidden = thread.IsHidden || ThreadVisibilityStore.IsHidden(thread.Id);
                            var card = Ui.Card(new ThreadRow(thread, true), 4, 16);
                            if (IsSelf && thread.Id > 0)
                            {
                                var more = Ui.IconButton("\uE712", "更多操作", 26);
                                var badge = Ui.Chip(hidden ? "主页不可见" : "主页可见",
                                    hidden ? "TextSecondary" : "Accent",
                                    hidden ? "SurfaceAlt" : "AccentSoft");
                                badge.VerticalAlignment = VerticalAlignment.Center;
                                badge.Margin = new Thickness(0, 0, 4, 0);
                                var overlay = new StackPanel
                                {
                                    Orientation = Orientation.Horizontal,
                                    HorizontalAlignment = HorizontalAlignment.Right,
                                    VerticalAlignment = VerticalAlignment.Top,
                                    Margin = new Thickness(0, 8, 10, 0),
                                };
                                overlay.Children.Add(badge);
                                overlay.Children.Add(more);
                                var host = new Grid();
                                host.Children.Add(card);
                                Panel.SetZIndex(overlay, 10);
                                host.Children.Add(overlay);
                                more.Click += (_, _) => ThreadActionPopup.Show(more, hidden, async hide =>
                                {
                                    try
                                    {
                                        await Svc.Api.SetThreadVisibilityAsync(thread.ForumId, thread.ForumName, thread.Id, hide);
                                        ThreadVisibilityStore.Set(thread.Id, hide);
                                        thread.IsHidden = hide;
                                        hidden = hide;
                                        UpdateVisibilityBadge(badge, hide);
                                        Svc.Window?.ShowToast(hide ? "已设为主页不可见" : "已设为主页可见");
                                    }
                                    catch (Exception ex)
                                    {
                                        Svc.Window?.ShowToast("操作失败：" + ex.Message);
                                    }
                                }, () =>
                                {
                                    var confirm = new ConfirmWindow("删除帖子", "确定删除这条帖子吗？删除后无法恢复。");
                                    if (confirm.ShowDialog() != true) return;
                                    _ = DeleteOwnThreadAsync(thread);
                                });
                                list.Children.Add(host);
                            }
                            else
                            {
                                list.Children.Add(card);
                            }
                        }
                        break;
                    }
                    case "follows":
                    case "fans":
                    {
                        var users = _tab == "follows"
                            ? await Svc.Api.GetFollowsAsync(_uid)
                            : await Svc.Api.GetFansAsync(_uid);
                        if (users.Count == 0)
                        {
                            list.Children.Add(HomeView.EmptyCard(_tab == "follows" ? "还没有关注任何人" : "还没有粉丝"));
                        }
                        foreach (var user in users) list.Children.Add(UserRow(user));
                        break;
                    }
                    case "forums":
                    {
                        var forums = await Svc.Api.GetFollowedForumsAsync(_uid);
                        if (forums.Count == 0)
                        {
                            list.Children.Add(HomeView.EmptyCard(IsSelf ? "还没有关注任何吧" : "该用户已隐藏关注的吧"));
                            break;
                        }
                        var wrap = new WrapPanel();
                        foreach (var forum in forums)
                        {
                            var chip = Ui.Button(forum.Name, false, 12);
                            chip.Margin = new Thickness(0, 0, 8, 8);
                            var name = forum.Name;
                            chip.Click += (_, _) => Svc.Window?.OpenForum(name);
                            wrap.Children.Add(chip);
                        }
                        list.Children.Add(wrap);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                list.Children.Add(HomeView.ErrorCard("加载失败：" + ex.Message, async () => await RenderAsync()));
            }
        }

        private UIElement UserRow(UserInfo user)
        {
            UserCache.Put(user);
            var card = new Border { CornerRadius = new CornerRadius(14), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12) };
            card.SetResourceReference(BackgroundProperty, "Surface");
            card.SetResourceReference(BorderBrushProperty, "Border");
            card.BorderThickness = new Thickness(1);
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(Ui.Avatar(user.AvatarUrl, 40, user.DisplayName));
            var text = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(Ui.Text(user.DisplayName, 14, FontWeights.SemiBold));
            text.Children.Add(Ui.Text("ID " + user.Id, 11.5, null, "TextTertiary"));
            row.Children.Add(text);
            card.Child = row;
            card.MouseLeftButtonUp += (_, _) => Svc.Window?.OpenProfile(user.Id, user.DisplayName);
            return card;
        }

        private UIElement BuildHeader(UserInfo user)
        {
            var card = new Border { CornerRadius = new CornerRadius(20), Margin = new Thickness(0, 4, 0, 12) };
            card.SetResourceReference(BackgroundProperty, "Surface");
            card.SetResourceReference(BorderBrushProperty, "Border");
            card.BorderThickness = new Thickness(1);

            var grid = new Grid { Margin = new Thickness(22, 20, 22, 20) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var avatar = Ui.Avatar(user.AvatarUrl, 76, user.DisplayName);
            avatar.VerticalAlignment = VerticalAlignment.Top;
            avatar.Cursor = Cursors.Hand;
            avatar.MouseLeftButtonUp += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
                {
                    Svc.Window?.OpenImageViewer(new System.Collections.Generic.List<string> { user.AvatarUrl }, 0);
                }
            };
            grid.Children.Add(avatar);

            var info = new StackPanel { Margin = new Thickness(18, 0, 0, 0) };
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
            nameRow.Children.Add(Ui.Text(user.DisplayName, 22, FontWeights.Bold));
            if (user.Level > 0)
            {
                var level = Ui.Chip("Lv." + user.Level, "Accent", "AccentSoft");
                level.Margin = new Thickness(8, 0, 0, 0);
                nameRow.Children.Add(level);
            }
            if (user.Sex == 1 || user.Sex == 2)
            {
                var gender = Ui.Chip(user.Sex == 1 ? "♂" : "♀", user.Sex == 1 ? "Accent" : "PinkText",
                    user.Sex == 1 ? "AccentSoft" : "SurfaceAlt");
                gender.Margin = new Thickness(8, 0, 0, 0);
                nameRow.Children.Add(gender);
            }
            info.Children.Add(nameRow);



            if (!string.IsNullOrWhiteSpace(user.Intro))
            {
                var intro = Ui.Text(user.Intro, 12.5, null, "TextSecondary");
                intro.Margin = new Thickness(0, 6, 0, 0);
                intro.MaxHeight = 42;
                info.Children.Add(intro);
            }

            var stats = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            stats.Children.Add(Stat(Ui.ShortNum(user.ThreadNum), "主题"));
            stats.Children.Add(Stat(Ui.ShortNum(user.PostNum), "帖子"));
            stats.Children.Add(Stat(Ui.ShortNum(user.FansNum), "粉丝"));
            stats.Children.Add(Stat(Ui.ShortNum(user.AgreeNum), "获赞"));
            info.Children.Add(stats);

            var bottom = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var copy = Ui.IconButton("\uE8C8", "复制 ID", 24);
            copy.Margin = new Thickness(6, 0, 0, 0);
            bottom.Children.Add(Ui.Text("ID " + (string.IsNullOrWhiteSpace(user.TiebaUid) ? user.Id.ToString() : user.TiebaUid),
                11.5, null, "TextTertiary"));
            bottom.Children.Add(copy);
            if (!string.IsNullOrWhiteSpace(user.IpAddress))
            {
                bottom.Children.Add(Ui.Text("   IP: " + user.IpAddress, 11.5, null, "TextTertiary"));
            }
            if (user.TbAge > 0)
            {
                bottom.Children.Add(Ui.Text("   吧龄: " + user.TbAge.ToString("0.0") + " 年", 11.5, null, "TextTertiary"));
            }
            copy.Click += async (_, _) =>
            {
                try
                {
                    await ClipboardHelper.SetTextAsync(user.Id.ToString());
                    Svc.Window?.ShowToast("ID 已复制");
                }
                catch
                {
                    Svc.Window?.ShowToast("复制失败，剪贴板被占用");
                }
            };
            if (!IsSelf && user.Id > 0)
            {
                var follow = Ui.Button(user.HasConcerned ? "取消关注" : "关注", !user.HasConcerned, 11.5);
                follow.Padding = new Thickness(12, 3, 12, 3);
                follow.Margin = new Thickness(8, 0, 0, 0);
                follow.Click += async (_, _) =>
                {
                    follow.IsEnabled = false;
                    try
                    {
                        await Svc.Api.FollowUserAsync(user.Portrait, !user.HasConcerned);
                        user.HasConcerned = !user.HasConcerned;
                        FollowCache.Set(user.Id, user.HasConcerned);
                        follow.Content = user.HasConcerned ? "取消关注" : "关注";
                        follow.SetResourceReference(FrameworkElement.StyleProperty,
                            user.HasConcerned ? "SecondaryButton" : "PrimaryButton");
                        follow.IsEnabled = true;
                        Svc.Window?.ShowToast(user.HasConcerned ? "已关注" : "已取消关注");
                    }
                    catch (Exception ex)
                    {
                        Svc.Window?.ShowToast("操作失败：" + ex.Message);
                        follow.IsEnabled = true;
                    }
                };
                bottom.Children.Add(follow);
            }
            info.Children.Add(bottom);

            Grid.SetColumn(info, 1);
            grid.Children.Add(info);
            card.Child = grid;
            return card;
        }

        private static void UpdateVisibilityBadge(Border badge, bool hidden)
        {
            badge.SetResourceReference(Border.BackgroundProperty, hidden ? "SurfaceAlt" : "AccentSoft");
            if (badge.Child is TextBlock text)
            {
                text.Text = hidden ? "主页不可见" : "主页可见";
                text.SetResourceReference(TextBlock.ForegroundProperty, hidden ? "TextSecondary" : "Accent");
            }
        }

        private async System.Threading.Tasks.Task DeleteOwnThreadAsync(ThreadSummary thread)
        {
            try
            {
                try
                {
                    var page = await Svc.Api.GetThreadAsync(thread.Id, 1, false, false, 0);
                    if (page?.Posts != null && page.Posts.Count > 0)
                    {
                        await CachedThreadStore.SaveAsync(page, false, 1);
                    }
                }
                catch
                {
                }
                await Svc.Api.DeleteThreadAsync(thread.ForumId, thread.ForumName, thread.Id, true);
                RecycleStore.Add(new RecyclePost
                {
                    ThreadId = thread.Id,
                    ForumId = thread.ForumId,
                    ForumName = thread.ForumName,
                    Title = thread.Title,
                    DeletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                });
                ThreadVisibilityStore.Set(thread.Id, false);
                Svc.Window?.ShowToast("帖子已删除");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Svc.Window?.ShowToast("删除失败：" + ex.Message);
            }
        }

        private ContextMenu BuildOwnThreadMenu(ThreadSummary thread)
        {
            var menu = new ContextMenu();
            var makeVisible = new MenuItem { Header = "设为主页可见" };
            makeVisible.Click += async (_, _) =>
            {
                try
                {
                    await Svc.Api.SetThreadVisibilityAsync(thread.ForumId, thread.ForumName, thread.Id, false);
                    Svc.Window?.ShowToast("已设为主页可见");
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("操作失败：" + ex.Message);
                }
            };
            menu.Items.Add(makeVisible);
            var makeHidden = new MenuItem { Header = "设为主页不可见" };
            makeHidden.Click += async (_, _) =>
            {
                try
                {
                    await Svc.Api.SetThreadVisibilityAsync(thread.ForumId, thread.ForumName, thread.Id, true);
                    Svc.Window?.ShowToast("已设为主页不可见");
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("操作失败：" + ex.Message);
                }
            };
            menu.Items.Add(makeHidden);
            var delete = new MenuItem { Header = "删除" };
            delete.Click += async (_, _) =>
            {
                if (MessageBox.Show("确定删除这条帖子吗？", "删除帖子", MessageBoxButton.OKCancel,
                        MessageBoxImage.Warning) != MessageBoxResult.OK) return;
                try
                {
                    await Svc.Api.DeleteThreadAsync(thread.ForumId, thread.ForumName, thread.Id, true);
                    Svc.Window?.ShowToast("帖子已删除");
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("删除失败：" + ex.Message);
                }
            };
            menu.Items.Add(delete);
            return menu;
        }

        private static UIElement Stat(string value, string label)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 20, 0) };
            panel.Children.Add(Ui.Text(value, 15, FontWeights.SemiBold));
            var caption = Ui.Text(" " + label, 11.5, null, "TextTertiary");
            caption.VerticalAlignment = VerticalAlignment.Bottom;
            panel.Children.Add(caption);
            return panel;
        }

        private UIElement BuildTabs()
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            AddTab(panel, "threads", "主题贴");
            AddTab(panel, "replies", "回复");
            AddTab(panel, "follows", "关注");
            AddTab(panel, "fans", "粉丝");
            AddTab(panel, "forums", "关注的吧");
            return panel;
        }

        private void AddTab(StackPanel panel, string key, string label)
        {
            var button = new RadioButton
            {
                Content = label,
                GroupName = "ProfileTab",
                IsChecked = _tab == key,
                Margin = new Thickness(0, 0, 8, 0),
                Tag = key,
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
            button.Checked += async (_, _) =>
            {
                if (_tab == key) return;
                _tab = key;
                await RenderAsync();
            };
            panel.Children.Add(button);
        }

        public async Task RefreshAsync() => await LoadAsync();
    }
}
