using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Views
{
    /// <summary>发现页：热帖流 + 分类标签。</summary>
    public sealed class HomeView : UserControl, IRefreshable
    {
        private readonly StackPanel _list;
        private readonly ScrollViewer _scroll;
        private readonly HashSet<long> _recommendSeen = new();
        private int _recommendPage = 1;
        private bool _recommendLoading;
        private bool _recommendHasMore = true;
        private TextBlock _recommendFooter;
        private readonly StackPanel _topicsHost;
        private readonly StackPanel _recentHost;
        private bool _topicsLoaded;
        private readonly StackPanel _tabs;
        private readonly StackPanel _modePanel;
        private readonly TextBlock _headTitle;
        private string _mode = "recommend";
        private readonly TextBlock _statusText;
        private string _tabCode = "";
        private bool _loading;

        public HomeView()
        {
            _scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            var scroll = _scroll;
            var root = new StackPanel
            {
                MaxWidth = 1040,
                MinWidth = 780,
                Margin = new Thickness(28, 6, 28, 40),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            root.Children.Add(BuildHero());

            _modePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 18, 2, 0) };
            AddMode("recommend", "推荐");
            AddMode("hot", "热门");
            AddMode("follow", "关注");
            root.Children.Add(_modePanel);
            _topicsHost = new StackPanel { Margin = new Thickness(2, 4, 2, 0) };
            root.Children.Add(_topicsHost);
            _recentHost = new StackPanel { Margin = new Thickness(2, 2, 2, 0) };
            root.Children.Add(_recentHost);

            var head = new Grid { Margin = new Thickness(2, 22, 2, 10) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _headTitle = Ui.Text("为你推荐", 20, FontWeights.SemiBold);
            head.Children.Add(_headTitle);

            _statusText = Ui.Text("", 12, null, "TextTertiary");
            _statusText.HorizontalAlignment = HorizontalAlignment.Right;
            _statusText.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_statusText, 1);
            head.Children.Add(_statusText);
            root.Children.Add(head);

            _tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            root.Children.Add(_tabs);

            _list = new StackPanel();
            root.Children.Add(_list);

            scroll.Content = root;
            Content = scroll;
            Loaded += async (_, _) => await LoadAsync();
            _scroll.ScrollChanged += async (_, _) =>
            {
                if (_mode != "recommend" || _scroll.ScrollableHeight <= 0) return;
                if (_scroll.VerticalOffset < _scroll.ScrollableHeight - 160) return;
                await LoadMoreRecommendAsync();
            };
        }

        private UIElement BuildHero()
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(22),
                Padding = new Thickness(28, 24, 28, 24),
                Margin = new Thickness(0, 6, 0, 0),
            };
            border.SetResourceReference(BackgroundProperty, "HeroGradient");
            border.BorderThickness = new Thickness(1);
            border.BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

            var now = DateTime.Now;
            var greeting = now.Hour < 6 ? "夜深了" : now.Hour < 9 ? "早上好" :
                now.Hour < 12 ? "上午好" : now.Hour < 14 ? "中午好" :
                now.Hour < 18 ? "下午好" : "晚上好";
            var weekdays = new[] { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = greeting,
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
            });
            stack.Children.Add(new TextBlock
            {
                Text = now.ToString("HH:mm") + " · " + now.Month + "月" + now.Day + "日 " + weekdays[(int)now.DayOfWeek],
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                Margin = new Thickness(0, 6, 0, 0),
            });

            var chips = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
            chips.Children.Add(HeroAction("刷新热帖", async () =>
            {
                _loading = false;
                await LoadAsync();
            }));
            chips.Children.Add(HeroAction("搜索贴吧", () =>
            {
                Svc.Nav.Push(() => new SearchView(), "搜索");
                return Task.CompletedTask;
            }));
            stack.Children.Add(chips);
            border.Child = stack;
            return border;
        }

        private static UIElement HeroAction(string text, Func<Task> action)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = new TextBlock { Text = text, FontSize = 12, Foreground = Brushes.White },
            };
            border.MouseLeftButtonUp += async (_, _) => await action();
            return border;
        }

        private async Task LoadAsync()
        {
            if (_loading) return;
            _loading = true;
            _statusText.Text = "加载中…";
            _list.Children.Clear();
            _list.Children.Add(Skeleton(3));
            if (!_topicsLoaded)
            {
                _topicsLoaded = true;
                _ = LoadTopicsAsync();
            }
            RenderRecentForums();
            try
            {
                if (_mode == "recommend")
                {
                    var recommended = Svc.DemoMode ? DemoData.Hot().Threads : await Svc.Api.GetRecommendedAsync();
                    if (recommended.Count == 0 && !Svc.DemoMode)
                    {
                        recommended = (await Data.HotAsync(null)).Threads;
                        _recommendHasMore = false;
                    }
                    _tabs.Visibility = Visibility.Collapsed;
                    _headTitle.Text = "为你推荐";
                    _list.Children.Clear();
                    _recommendSeen.Clear();
                    _recommendPage = 1;
                    _recommendHasMore = !Svc.DemoMode && recommended.Count > 0;
                    if (recommended.Count == 0) _list.Children.Add(EmptyCard("暂时没有推荐内容，稍后再来看看"));
                    foreach (var thread in recommended)
                    {
                        AddRecommendCard(thread);
                    }
                    _recommendFooter = Ui.Text(_recommendHasMore ? "继续下滑加载更多" : "已经到底了", 11.5, null, "TextTertiary");
                    _recommendFooter.HorizontalAlignment = HorizontalAlignment.Center;
                    _recommendFooter.Margin = new Thickness(0, 10, 0, 4);
                    _list.Children.Add(_recommendFooter);
                    _statusText.Text = "推荐 · " + DateTime.Now.ToString("HH:mm");
                    return;
                }
                if (_mode == "follow")
                {
                    await RenderFollowAsync();
                    return;
                }
                _tabs.Visibility = Visibility.Visible;
                _headTitle.Text = "热门讨论";
                var page = await Data.HotAsync(_tabCode);
                RenderTabs(page);
                _list.Children.Clear();
                if (page.Threads.Count == 0)
                {
                    _list.Children.Add(EmptyCard("这里暂时没有热帖，稍后再来看看"));
                }
                var threads = Data.Visible(page.Threads).Take(30).ToList();
                foreach (var thread in threads)
                {
                    var card = Ui.Card(new ThreadRow(thread, true, null, _list.Children.Count + 1), 4, 16);
                    _list.Children.Add(card);
                    Anim.FadeInUp(card, Math.Min(_list.Children.Count * 26, 320), 14, 300);
                }
                _statusText.Text = "TOP " + threads.Count + " · " + DateTime.Now.ToString("HH:mm");
            }
            catch (Exception ex)
            {
                _list.Children.Clear();
                _list.Children.Add(ErrorCard(ex.Message, LoadAsync));
                _statusText.Text = "";
            }
            finally
            {
                _loading = false;
            }
        }

        private async System.Threading.Tasks.Task RenderFollowAsync()
        {
            _tabs.Visibility = Visibility.Collapsed;
            _tabs.Children.Clear();
            _headTitle.Text = "关注的人";
            _list.Children.Clear();
            _recommendSeen.Clear();
            if (!Svc.Account.IsLoggedIn)
            {
                _list.Children.Add(EmptyCard("登录后查看关注的人的帖子"));
                _statusText.Text = "";
                return;
            }
            if (Svc.DemoMode)
            {
                foreach (var thread in DemoData.Hot().Threads.Take(10))
                {
                    AddRecommendCard(thread);
                }
                _statusText.Text = "关注 · " + DateTime.Now.ToString("HH:mm");
                return;
            }
            var follows = await Svc.Api.GetFollowsAsync(Svc.Account.Uid);
            if (follows.Count == 0)
            {
                _list.Children.Add(EmptyCard("还没有关注任何人，去用户主页点关注吧"));
                _statusText.Text = "";
                return;
            }
            var tasks = follows.Take(10).Select(async user =>
            {
                try
                {
                    return await Svc.Api.GetUserPostsAsync(user.Id, 1, true);
                }
                catch
                {
                    return new List<ThreadSummary>();
                }
            });
            var results = await System.Threading.Tasks.Task.WhenAll(tasks);
            var threads = results.SelectMany(items => items)
                .Where(thread => thread != null && thread.Id != 0)
                .GroupBy(thread => thread.Id)
                .Select(group => group.First())
                .Where(thread => !BlockWords.Match(thread.Title) && !BlockWords.Match(thread.PreviewText))
                .OrderByDescending(thread => thread.LastTimeInt)
                .Take(40)
                .ToList();
            if (threads.Count == 0)
            {
                _list.Children.Add(EmptyCard("关注的人最近还没有发帖"));
            }
            foreach (var thread in threads)
            {
                AddRecommendCard(thread);
            }
            _statusText.Text = "关注 " + follows.Count + " 人 · " + DateTime.Now.ToString("HH:mm");
        }

        private void RenderRecentForums()
        {
            _recentHost.Children.Clear();
            if (!Svc.Settings.ShowRecentForums) return;
            var names = (Svc.Settings.RecentForums ?? Array.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .Take(8)
                .ToList();
            if (names.Count == 0) return;
            _recentHost.Children.Add(Ui.Text("最近浏览的吧", 13, FontWeights.SemiBold, "TextSecondary"));
            var wrap = new WrapPanel { Margin = new Thickness(0, 6, 0, 2) };
            foreach (var name in names)
            {
                var chip = Ui.Button(name, false, 11.5);
                chip.Margin = new Thickness(0, 0, 8, 8);
                var captured = name;
                chip.Click += (_, _) => Svc.Window?.OpenForum(captured);
                wrap.Children.Add(chip);
            }
            _recentHost.Children.Add(wrap);
        }

        private async System.Threading.Tasks.Task LoadTopicsAsync()
        {
            try
            {
                var topics = await Data.TopicsAsync();
                if (topics.Count == 0) return;
                _topicsHost.Children.Add(Ui.Text("最有料热点", 13, FontWeights.SemiBold, "TextSecondary"));
                var wrap = new WrapPanel { Margin = new Thickness(0, 6, 0, 2) };
                foreach (var topic in topics.Take(6))
                {
                    var chip = Ui.Button(topic.Name, false, 11.5);
                    chip.Margin = new Thickness(0, 0, 8, 8);
                    var captured = topic;
                    chip.Click += (_, _) => Svc.Window?.OpenTopic(captured);
                    wrap.Children.Add(chip);
                }
                _topicsHost.Children.Add(wrap);
            }
            catch
            {
            }
        }

        private void AddRecommendCard(ThreadSummary thread)
        {
            if (thread.Id != 0 && !_recommendSeen.Add(thread.Id)) return;
            var card = Ui.Card(new ThreadRow(thread, true), 4, 16);
            _list.Children.Add(card);
            Anim.FadeInUp(card, Math.Min(_list.Children.Count * 26, 320), 14, 300);
        }

        private async System.Threading.Tasks.Task LoadMoreRecommendAsync()
        {
            if (_mode != "recommend" || _recommendLoading || !_recommendHasMore) return;
            _recommendLoading = true;
            try
            {
                if (_recommendFooter != null) _recommendFooter.Text = "正在加载…";
                var next = await Svc.Api.GetRecommendedAsync(_recommendPage + 1);
                if (next.Count == 0)
                {
                    _recommendHasMore = false;
                    if (_recommendFooter != null) _recommendFooter.Text = "已经到底了";
                    return;
                }
                _recommendPage++;
                var before = _list.Children.Count;
                if (_recommendFooter != null) _list.Children.Remove(_recommendFooter);
                foreach (var thread in Data.Visible(next)) AddRecommendCard(thread);
                if (_recommendFooter != null)
                {
                    _recommendFooter.Text = "继续下滑加载更多";
                    _list.Children.Add(_recommendFooter);
                }
                if (_list.Children.Count == before)
                {
                    _recommendHasMore = false;
                    if (_recommendFooter != null) _recommendFooter.Text = "已经到底了";
                }
            }
            catch
            {
            }
            finally
            {
                _recommendLoading = false;
                if (_recommendFooter != null && _recommendFooter.Text == "正在加载…")
                {
                    _recommendFooter.Text = _recommendHasMore ? "继续下滑加载更多" : "已经到底了";
                }
            }
        }

        private void AddMode(string code, string label)
        {
            var radio = new RadioButton
            {
                Content = label,
                GroupName = "HomeMode",
                IsChecked = _mode == code,
                Margin = new Thickness(0, 0, 8, 0),
            };
            radio.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
            radio.Checked += async (_, _) =>
            {
                if (_mode == code) return;
                _mode = code;
                await LoadAsync();
            };
            _modePanel.Children.Add(radio);
        }

        private void RenderTabs(HotPage page)
        {
            _tabs.Children.Clear();
            var all = new RadioButton { Content = "全部", GroupName = "HotTabs", IsChecked = _tabCode == "" };
            all.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
            all.Checked += async (_, _) =>
            {
                if (_tabCode == "") return;
                _tabCode = "";
                await LoadAsync();
            };
            _tabs.Children.Add(all);
            foreach (var tab in page.Tabs)
            {
                if (string.IsNullOrWhiteSpace(tab.Name)) continue;
                if (Svc.Settings.HideVideoThreads &&
                    (tab.Name.Contains("视频", StringComparison.Ordinal) || tab.Code == "shipin"))
                {
                    continue;
                }
                var code = tab.Code;
                var radio = new RadioButton
                {
                    Content = tab.Name,
                    GroupName = "HotTabs",
                    IsChecked = _tabCode == code,
                };
                radio.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
                radio.Checked += async (_, _) =>
                {
                    if (_tabCode == code) return;
                    _tabCode = code;
                    await LoadAsync();
                };
                _tabs.Children.Add(radio);
            }
        }

        public async Task RefreshAsync()
        {
            _loading = false;
            await LoadAsync();
        }

        public static UIElement Skeleton(int rows)
        {
            var panel = new StackPanel();
            for (var i = 0; i < rows; i++)
            {
                var card = new Border
                {
                    Height = 86,
                    CornerRadius = new CornerRadius(16),
                    Margin = new Thickness(0, 0, 0, 10),
                };
                card.SetResourceReference(BackgroundProperty, "Surface");
                card.SetResourceReference(BorderBrushProperty, "Border");
                card.BorderThickness = new Thickness(1);
                var inner = new StackPanel { Margin = new Thickness(18, 16, 18, 16) };
                var bar1 = new Border { Height = 14, Width = 380, CornerRadius = new CornerRadius(7), HorizontalAlignment = HorizontalAlignment.Left };
                bar1.SetResourceReference(BackgroundProperty, "SurfaceHover");
                var bar2 = new Border { Height = 10, Width = 240, CornerRadius = new CornerRadius(5), Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
                bar2.SetResourceReference(BackgroundProperty, "SurfaceHover");
                inner.Children.Add(bar1);
                inner.Children.Add(bar2);
                card.Child = inner;
                panel.Children.Add(card);
            }
            return panel;
        }

        public static UIElement EmptyCard(string message)
        {
            return Ui.Card(new TextBlock
            {
                Text = message,
                FontSize = 13,
                Foreground = Ui.Brush("TextSecondary"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10, 30, 10, 30),
            }, 8, 16);
        }

        public static UIElement ErrorCard(string message, Func<Task> retry)
        {
            var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(Ui.Text("加载失败", 15, FontWeights.SemiBold));
            var detail = Ui.Text(message, 12, null, "TextTertiary");
            detail.Margin = new Thickness(0, 6, 0, 12);
            detail.HorizontalAlignment = HorizontalAlignment.Center;
            detail.TextAlignment = TextAlignment.Center;
            panel.Children.Add(detail);
            var button = Ui.Button("重试", true, 12.5);
            button.HorizontalAlignment = HorizontalAlignment.Center;
            button.Click += async (_, _) => await retry();
            panel.Children.Add(button);
            return Ui.Card(panel, 26, 16);
        }
    }
}
