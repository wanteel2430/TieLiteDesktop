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
    /// <summary>吧页面：吧信息 + 排序标签 + 帖子列表。</summary>
    public sealed class ForumView : UserControl, IRefreshable
    {
        private readonly string _forumName;
        private readonly StackPanel _threadList;
        private readonly StackPanel _tabPanel;
        private readonly StackPanel _sectionPanel;
        private ForumTab _activeTab;
        private Button _signButton;
        private bool _detailLoaded;
        private readonly Border _headerHost;
        private readonly Border _ruleHost;
        private readonly TextBlock _ruleText;
        private readonly TextBlock _status;
        private readonly Button _loadMoreButton;
        private ForumInfo _forum;
        private int _page = 1;
        private int _sortType;
        private bool _loading;

        public ForumView(string forumName)
        {
            _forumName = forumName;
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            var root = new StackPanel
            {
                MaxWidth = 1040,
                MinWidth = 780,
                Margin = new Thickness(28, 6, 28, 40),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var searchHost = new Border
            {
                Height = 38,
                CornerRadius = new CornerRadius(9),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 4, 0, 10),
            };
            searchHost.SetResourceReference(Border.BackgroundProperty, "Surface");
            searchHost.SetResourceReference(Border.BorderBrushProperty, "Border");
            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var searchIcon = Ui.Icon("\uE721", 13, "TextSecondary");
            searchIcon.Margin = new Thickness(11, 0, 8, 0);
            searchGrid.Children.Add(searchIcon);
            var searchInput = new TextBox
            {
                BorderThickness = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                FontSize = 12.5,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            searchInput.SetResourceReference(TextBox.FontFamilyProperty, "UiFont");
            searchInput.SetResourceReference(TextBox.ForegroundProperty, "TextPrimary");
            searchInput.SetResourceReference(TextBox.CaretBrushProperty, "TextPrimary");
            Grid.SetColumn(searchInput, 1);
            searchGrid.Children.Add(searchInput);
            var searchHint = Ui.Text("搜索本吧内容…", 12.5, null, "TextSecondary");
            searchHint.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9A, 0x9D, 0xA6));
            searchHint.IsHitTestVisible = false;
            searchHint.Margin = new Thickness(2, 0, 0, 0);
            Grid.SetColumn(searchHint, 1);
            searchGrid.Children.Add(searchHint);
            searchInput.TextChanged += (_, _) =>
            {
                searchHint.Visibility = string.IsNullOrEmpty(searchInput.Text) ? Visibility.Visible : Visibility.Collapsed;
            };
            searchInput.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter) return;
                var keyword = searchInput.Text?.Trim();
                if (string.IsNullOrEmpty(keyword)) return;
                var name = _forum?.Name ?? _forumName;
                Svc.Nav.Push(() => new SearchView(keyword, name), "吧内搜索");
            };
            searchHost.Child = searchGrid;
            root.Children.Add(searchHost);

            _headerHost = new Border { CornerRadius = new CornerRadius(20), Margin = new Thickness(0, 4, 0, 14) };
            _headerHost.SetResourceReference(BackgroundProperty, "Surface");
            _headerHost.SetResourceReference(BorderBrushProperty, "Border");
            _headerHost.BorderThickness = new Thickness(1);
            _headerHost.Child = new Border
            {
                Height = 96,
                Child = new TextBlock
                {
                    Text = "加载中…",
                    FontSize = 13,
                    Foreground = Ui.Brush("TextTertiary"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            root.Children.Add(_headerHost);

            _ruleHost = new Border
            {
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(14, 9, 14, 9),
                Visibility = Visibility.Collapsed,
                Cursor = Cursors.Hand,
            };
            _ruleHost.SetResourceReference(Border.BackgroundProperty, "SurfaceAlt");
            _ruleHost.SetResourceReference(Border.BorderBrushProperty, "Border");
            _ruleText = Ui.Text("", 12, null, "TextSecondary");
            _ruleText.TextWrapping = TextWrapping.Wrap;
            _ruleHost.Child = _ruleText;
            _ruleHost.MouseLeftButtonUp += async (_, _) =>
            {
                if (_forum == null || _forum.Id <= 0) return;
                try
                {
                    var detail = await Svc.Api.GetForumRuleAsync(_forum.Id);
                    var window = new ForumRuleWindow(_forum.Name, detail) { Owner = Svc.Window };
                    window.Show();
                    window.Activate();
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("加载吧规失败：" + ex.Message);
                }
            };
            root.Children.Add(_ruleHost);

            _tabPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            root.Children.Add(_tabPanel);
            _sectionPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            root.Children.Add(_sectionPanel);

            _status = Ui.Text("", 12, null, "TextTertiary");
            _status.Margin = new Thickness(2, 0, 0, 8);
            root.Children.Add(_status);

            _threadList = new StackPanel();
            root.Children.Add(_threadList);

            _loadMoreButton = Ui.Button("加载更多", false, 12.5);
            _loadMoreButton.HorizontalAlignment = HorizontalAlignment.Center;
            _loadMoreButton.Margin = new Thickness(0, 10, 0, 0);
            _loadMoreButton.Visibility = Visibility.Collapsed;
            _loadMoreButton.Click += async (_, _) => await LoadPageAsync(_page + 1);
            root.Children.Add(_loadMoreButton);

            scroll.Content = root;
            Content = scroll;
            Loaded += async (_, _) => await LoadPageAsync(1);
        }

        private async Task LoadPageAsync(int page)
        {
            if (_loading) return;
            _loading = true;
            if (page == 1)
            {
                _status.Text = "加载中…";
                _threadList.Children.Clear();
                _threadList.Children.Add(HomeView.Skeleton(4));
            }
            _loadMoreButton.IsEnabled = false;
            try
            {
                var isVideoSection = _activeTab != null && _activeTab.Code == "video";
                var result = _activeTab == null || isVideoSection
                    ? await Data.ForumAsync(_forumName, page, _sortType)
                    : await Svc.Api.GetForumTabAsync(_forum?.Id ?? 0, _forumName, _activeTab, page, _sortType);
                if (isVideoSection)
                {
                    var videos = result.Threads.Where(thread => thread.HasVideo).ToList();
                    var scan = page;
                    while (videos.Count < 3 && scan < page + 3)
                    {
                        scan++;
                        var more = await Data.ForumAsync(_forumName, scan, _sortType);
                        if (more.Threads.Count == 0) break;
                        videos.AddRange(more.Threads.Where(thread => thread.HasVideo));
                    }
                    result.Threads.Clear();
                    result.Threads.AddRange(videos.Take(30));
                }
                if (page == 1)
                {
                    if (_activeTab == null)
                    {
                        _forum = result.Forum;
                        RenderHeader();
                        if (!_detailLoaded)
                        {
                            _detailLoaded = true;
                            _ = LoadForumDetailAsync(result.Forum.Id);
                        }
                        if (!isVideoSection) RenderTabs(result);
                    }
                    _threadList.Children.Clear();
                }
                else
                {
                    _threadList.Children.Add(new Border { Height = 4 });
                }
                foreach (var thread in Data.Visible(result.Threads))
                {
                    var card = Ui.Card(new ThreadRow(thread, false, _forumName), 4, 16);
                    _threadList.Children.Add(card);
                    if (page == 1) Anim.FadeInUp(card, _threadList.Children.Count * 24, 14, 300);
                }
                _page = page;
                var hasMore = result.Threads.Count > 0 && (result.Page == null || result.Page.HasMore || result.Page.TotalPage > page);
                _loadMoreButton.Visibility = hasMore ? Visibility.Visible : Visibility.Collapsed;
                _status.Text = result.Threads.Count == 0 && page == 1
                    ? "这个吧还没有帖子"
                    : "第 " + page + " 页 · 共 " + Math.Max(1, result.Page?.TotalPage ?? 1) + " 页";
                Svc.Window?.SetSubtitle(_forum?.Name);
            }
            catch (Exception ex)
            {
                if (page == 1 && _activeTab != null)
                {
                    _activeTab = null;
                    _loading = false;
                    _ = LoadPageAsync(1);
                    Svc.Window?.ShowToast("该分区暂时无法加载");
                    return;
                }
                if (page == 1)
                {
                    _threadList.Children.Clear();
                    _threadList.Children.Add(HomeView.ErrorCard(ex.Message, () => LoadPageAsync(1)));
                }
                _status.Text = "";
            }
            finally
            {
                _loadMoreButton.IsEnabled = true;
                _loading = false;
            }
        }

        private void RenderHeader()
        {
            var forum = _forum ?? new ForumInfo { Name = _forumName };
            if (_ruleHost != null && _ruleText != null)
            {
                _ruleText.Text = "点击查看吧规";
                _ruleHost.Visibility = Visibility.Visible;
            }
            var grid = new Grid { Margin = new Thickness(20, 18, 20, 18) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatar = Ui.Avatar(forum.AvatarUrl, 62, forum.Name);
            avatar.CornerRadius = new CornerRadius(16);
            avatar.VerticalAlignment = VerticalAlignment.Top;
            grid.Children.Add(avatar);

            var info = new StackPanel { Margin = new Thickness(16, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
            nameRow.Children.Add(Ui.Text(forum.Name, 21, FontWeights.Bold));
            if (!string.IsNullOrWhiteSpace(forum.FirstClass))
            {
                var chip = Ui.Chip(forum.FirstClass, "TextSecondary");
                chip.Margin = new Thickness(8, 0, 0, 0);
                nameRow.Children.Add(chip);
            }
            if (forum.SignDayCount > 0)
            {
                var signChip = Ui.Chip("连签 " + forum.SignDayCount + " 天", "Accent", "AccentSoft");
                signChip.Margin = new Thickness(8, 0, 0, 0);
                nameRow.Children.Add(signChip);
            }
            info.Children.Add(nameRow);

            if (!string.IsNullOrWhiteSpace(forum.Slogan))
            {
                var slogan = Ui.Text(forum.Slogan, 12.5, null, "TextSecondary");
                slogan.Margin = new Thickness(0, 4, 0, 0);
                slogan.MaxHeight = 20;
                info.Children.Add(slogan);
            }

            var stats = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            stats.Children.Add(Stat("关注", Ui.ShortNum(forum.MemberNum)));
            stats.Children.Add(Stat("帖子", Ui.ShortNum(forum.PostNum)));
            stats.Children.Add(Stat("主题", Ui.ShortNum(forum.ThreadNum)));
            if (!string.IsNullOrWhiteSpace(forum.HotText))
            {
                stats.Children.Add(Stat("热度", forum.HotText));
            }
            info.Children.Add(stats);



            if (!string.IsNullOrWhiteSpace(forum.Description))
            {
                var desc = Ui.Text(forum.Description, 12, null, "TextTertiary");
                desc.Margin = new Thickness(0, 8, 0, 0);
                desc.MaxHeight = 54;
                desc.TextTrimming = TextTrimming.CharacterEllipsis;
                desc.TextWrapping = TextWrapping.Wrap;
                info.Children.Add(desc);
            }
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var compose = Ui.Button("发帖", true, 12.5);
            compose.Margin = new Thickness(0, 0, 8, 0);
            compose.Click += async (_, _) => await OpenComposeAsync(forum);
            actions.Children.Add(compose);

            var follow = Ui.Button(forum.IsLiked ? "已关注" : "关注", !forum.IsLiked, 12.5);
            follow.Margin = new Thickness(0, 0, 8, 0);
            follow.Click += async (_, _) => await ToggleFollowAsync(forum);
            actions.Children.Add(follow);

            if (forum.IsLiked || forum.IsSigned)
            {
                _signButton = Ui.Button(forum.IsSigned ? "已签到" : "签到", false, 12.5);
                _signButton.IsEnabled = !forum.IsSigned;
                _signButton.Click += async (_, _) => await SignAsync(forum);
                actions.Children.Add(_signButton);
            }
            else
            {
                _signButton = null;
            }

            var bawu = Ui.Button("吧务", false, 12.5);
            bawu.Margin = new Thickness(8, 0, 0, 0);
            bawu.Click += (_, _) =>
            {
                var window = new BawuWindow(forum.Id, forum.Name) { Owner = Svc.Window };
                window.ShowDialog();
            };
            actions.Children.Add(bawu);
            Grid.SetColumn(actions, 2);
            grid.Children.Add(actions);

            _headerHost.Child = grid;
        }

        private static UIElement Stat(string label, string value)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 0) };
            panel.Children.Add(Ui.Text(value, 14, FontWeights.SemiBold));
            var caption = Ui.Text(" " + label, 11.5, null, "TextTertiary");
            caption.VerticalAlignment = VerticalAlignment.Bottom;
            panel.Children.Add(caption);
            return panel;
        }

        private void RenderTabs(ForumPage page)
        {
            _tabPanel.Children.Clear();
            var sorts = new (string label, int value)[] { ("回复时间", 0), ("最新发布", 1) };
            foreach (var sort in sorts)
            {
                var radio = new RadioButton { Content = sort.label, GroupName = "ForumSort", IsChecked = _sortType == sort.value };
                radio.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
                var value = sort.value;
                radio.Checked += async (_, _) =>
                {
                    if (_sortType == value) return;
                    _sortType = value;
                    await LoadPageAsync(1);
                };
                _tabPanel.Children.Add(radio);
            }
            _sectionPanel.Children.Clear();
            var keepTabs = page?.Tabs?
                .Where(t => !string.IsNullOrWhiteSpace(t.Name) && t.Id > 0 &&
                            t.Name != "热门" && t.Name != "最新" && t.Name != "吧主推荐" &&
                            !(Svc.Settings.HideVideoThreads && t.Name == "视频"))
                .ToList() ?? new List<ForumTab>();
            _sectionPanel.Visibility = keepTabs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (keepTabs.Count > 0)
            {
                var all = new RadioButton
                {
                    Content = "全部",
                    GroupName = "ForumSection",
                    IsChecked = _activeTab == null,
                    Margin = new Thickness(0, 0, 8, 0),
                };
                all.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
                all.Checked += async (_, _) =>
                {
                    if (_activeTab == null) return;
                    _activeTab = null;
                    await LoadPageAsync(1);
                };
                _sectionPanel.Children.Add(all);

                foreach (var tab in keepTabs)
                {
                    var radio = new RadioButton
                    {
                        Content = tab.Name,
                        GroupName = "ForumSection",
                        IsChecked = _activeTab != null && _activeTab.Id == tab.Id,
                        Margin = new Thickness(0, 0, 8, 0),
                    };
                    radio.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
                    var selected = tab;
                    radio.Checked += async (_, _) =>
                    {
                        if (_activeTab != null && _activeTab.Id == selected.Id) return;
                        _activeTab = selected;
                        await LoadPageAsync(1);
                    };
                    _sectionPanel.Children.Add(radio);
                }

                if (!Svc.Settings.HideVideoThreads && !keepTabs.Any(t => t.Name == "视频"))
                {
                    var videoTab = new ForumTab { Name = "视频", Code = "video" };
                    var videoRadio = new RadioButton
                    {
                        Content = "视频",
                        GroupName = "ForumSection",
                        Margin = new Thickness(0, 0, 8, 0),
                    };
                    videoRadio.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
                    videoRadio.Checked += async (_, _) =>
                    {
                        if (_activeTab == videoTab) return;
                        _activeTab = videoTab;
                        await LoadPageAsync(1);
                    };
                    _sectionPanel.Children.Add(videoRadio);
                }
            }

            if (page?.Forum?.Managers != null && page.Forum.Managers.Count > 0)
            {
                var managers = string.Join(" · ", page.Forum.Managers
                    .Select(m => string.IsNullOrEmpty(m.ShowName) ? m.Name : m.ShowName)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Take(3));
                if (!string.IsNullOrWhiteSpace(managers))
                {
                    var label = Ui.Text("吧主：" + managers, 11.5, null, "TextTertiary");
                    label.Margin = new Thickness(12, 0, 0, 0);
                    _tabPanel.Children.Add(label);
                }
            }
        }

        private async Task OpenComposeAsync(ForumInfo forum)
        {
            if (!Svc.Account.IsLoggedIn)
            {
                Svc.Window?.ShowToast("登录后才能发帖");
                return;
            }
            var window = new ComposeWindow(forum) { Owner = Svc.Window };
            if (window.ShowDialog() == true)
            {
                if (window.CreatedThreadId > 0)
                {
                    Svc.Window?.OpenThread(window.CreatedThreadId, "", forum.Name);
                }
                await LoadPageAsync(1);
            }
        }

        private async Task ToggleFollowAsync(ForumInfo forum)
        {
            if (!Svc.Account.IsLoggedIn)
            {
                Svc.Window?.ShowToast("登录后才能关注贴吧");
                return;
            }
            try
            {
                await Svc.Api.LikeForumAsync(forum.Id, forum.Name, !forum.IsLiked);
                forum.IsLiked = !forum.IsLiked;
                RenderHeader();
                Svc.Window?.ShowToast(forum.IsLiked ? "已关注 " + forum.Name + " 吧" : "已取消关注");
            }
            catch (Exception ex)
            {
                Svc.Window?.ShowToast(ex.Message);
            }
        }

        private async System.Threading.Tasks.Task LoadForumDetailAsync(long forumId)
        {
            try
            {
                var detail = await Svc.Api.GetForumDetailAsync(forumId);
                if (detail == null || _forum == null) return;
                if (!string.IsNullOrWhiteSpace(detail.Slogan)) _forum.Slogan = detail.Slogan;
                if (!string.IsNullOrWhiteSpace(detail.Description)) _forum.Description = detail.Description;
                if (detail.MemberNum > 0) _forum.MemberNum = detail.MemberNum;
                if (detail.ThreadNum > 0) _forum.ThreadNum = detail.ThreadNum;
                if (!string.IsNullOrWhiteSpace(detail.HotText)) _forum.HotText = detail.HotText;
                RenderHeader();
            }
            catch
            {
            }
        }

        private async Task SignAsync(ForumInfo forum)
        {
            if (!Svc.Account.IsLoggedIn)
            {
                Svc.Window?.ShowToast("登录后才能签到");
                return;
            }
            try
            {
                await Svc.Api.SignForumAsync(forum.Id, forum.Name);
                forum.IsSigned = true;
                forum.SignDayCount = Math.Max(1, forum.SignDayCount + 1);
                RenderHeader();
                Svc.Window?.ShowToast("签到成功，已连签 " + forum.SignDayCount + " 天");
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("已签", StringComparison.Ordinal))
                {
                    forum.IsSigned = true;
                    if (_signButton != null)
                    {
                        _signButton.Content = "已签到";
                        _signButton.IsEnabled = false;
                    }
                }
                Svc.Window?.ShowToast(ex.Message);
            }
        }

        public async Task RefreshAsync()
        {
            _loading = false;
            await LoadPageAsync(1);
        }
    }
}
