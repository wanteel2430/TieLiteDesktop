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
    /// <summary>搜索页：吧 / 帖 双模式搜索。</summary>
    public sealed class SearchView : UserControl
    {
        private readonly TextBox _input;
        private readonly TextBlock _placeholder;
        private readonly StackPanel _results;
        private readonly TextBlock _status;
        private readonly RadioButton _threadTab;
        private readonly RadioButton _forumTab;
        private readonly RadioButton _userTab;
        private bool _searchingThreads = true;
        private bool _searchingUsers;
        private string _lastQuery = "";
        private readonly string _forumFilter;
        private readonly StackPanel _historyPanel;

        public SearchView(string initialQuery = null, string forumName = null)
        {
            _forumFilter = forumName;
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            var root = new StackPanel
            {
                MaxWidth = 900,
                MinWidth = 720,
                Margin = new Thickness(28, 10, 28, 40),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var searchHost = new Border
            {
                Height = 52,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 0, 8, 0),
            };
            searchHost.SetResourceReference(BackgroundProperty, "SurfaceAlt");
            searchHost.SetResourceReference(BorderBrushProperty, "Border");
            searchHost.BorderThickness = new Thickness(1);
            searchHost.ClipToBounds = true;
            searchHost.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 3,
                Direction = 270,
                Opacity = 0.16,
            };

            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = Ui.Icon("\uE721", 16);
            icon.Margin = new Thickness(0, 0, 10, 0);
            searchGrid.Children.Add(icon);

            var inputGrid = new Grid();
            _placeholder = Ui.Text("搜索贴吧、帖子或用户", 14, null, "TextTertiary");
            _placeholder.IsHitTestVisible = false;
            _placeholder.VerticalAlignment = VerticalAlignment.Center;
            _input = new TextBox { FontSize = 14, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
            _input.SetResourceReference(TextBox.ForegroundProperty, "TextPrimary");
            _input.SetResourceReference(TextBox.CaretBrushProperty, "Accent");
            _input.SetResourceReference(TextBox.FontFamilyProperty, "UiFont");
            _input.VerticalContentAlignment = VerticalAlignment.Center;
            _input.Padding = new Thickness(0, 0, 0, 0);
            _input.GotFocus += (_, _) => searchHost.SetResourceReference(BorderBrushProperty, "Accent");
            _input.LostFocus += (_, _) => searchHost.SetResourceReference(BorderBrushProperty, "Border");
            _input.TextChanged += (_, _) =>
            {
                _placeholder.Visibility = string.IsNullOrEmpty(_input.Text) ? Visibility.Visible : Visibility.Collapsed;
                RenderHistory();
            };
            _input.KeyDown += async (_, e) =>
            {
                if (e.Key == Key.Enter) await SearchAsync();
            };
            inputGrid.Children.Add(_placeholder);
            inputGrid.Children.Add(_input);
            Grid.SetColumn(inputGrid, 1);
            searchGrid.Children.Add(inputGrid);

            var go = Ui.Button("搜索", true, 13);
            go.Padding = new Thickness(20, 8, 20, 8);
            go.VerticalAlignment = VerticalAlignment.Center;
            go.Click += async (_, _) => await SearchAsync();
            Grid.SetColumn(go, 2);
            searchGrid.Children.Add(go);

            searchHost.Child = searchGrid;
            root.Children.Add(searchHost);

            _historyPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            root.Children.Add(_historyPanel);

            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 12) };
            _threadTab = new RadioButton { Content = "帖子", GroupName = "SearchMode", IsChecked = true };
            _threadTab.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
            _threadTab.Checked += async (_, _) =>
            {
                if (!_searchingThreads || _searchingUsers)
                {
                    _searchingThreads = true;
                    _searchingUsers = false;
                    if (!string.IsNullOrEmpty(_lastQuery)) await SearchAsync();
                }
            };
            _forumTab = new RadioButton { Content = "贴吧", GroupName = "SearchMode" };
            _forumTab.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
            _forumTab.Checked += async (_, _) =>
            {
                if (_searchingThreads || _searchingUsers)
                {
                    _searchingThreads = false;
                    _searchingUsers = false;
                    if (!string.IsNullOrEmpty(_lastQuery)) await SearchAsync();
                }
            };
            _userTab = new RadioButton { Content = "用户", GroupName = "SearchMode" };
            _userTab.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
            _userTab.Checked += async (_, _) =>
            {
                if (!_searchingUsers)
                {
                    _searchingUsers = true;
                    _searchingThreads = false;
                    if (!string.IsNullOrEmpty(_lastQuery)) await SearchAsync();
                }
            };
            if (string.IsNullOrWhiteSpace(_forumFilter))
            {
                tabs.Children.Add(_threadTab);
                tabs.Children.Add(_forumTab);
                tabs.Children.Add(_userTab);
            }
            else
            {
                tabs.Children.Add(_threadTab);
                _forumTab.Visibility = Visibility.Collapsed;
                _userTab.Visibility = Visibility.Collapsed;
            }
            root.Children.Add(tabs);

            _status = Ui.Text("", 12, null, "TextTertiary");
            _status.Margin = new Thickness(2, 0, 0, 8);
            root.Children.Add(_status);

            _results = new StackPanel();
            root.Children.Add(_results);

            scroll.Content = root;
            Content = scroll;
            Loaded += async (_, _) =>
            {
                _input.Focus();
                RenderHistory();
                if (!string.IsNullOrWhiteSpace(initialQuery))
                {
                    _input.Text = initialQuery;
                    await SearchAsync();
                }
            };
            RenderEmpty();
        }

        private void RenderHistory()
        {
            _historyPanel.Children.Clear();
            if (!string.IsNullOrEmpty(_input.Text)) return;
            var items = SearchHistoryStore.All();
            if (items.Count == 0) return;

            var head = new Grid { Margin = new Thickness(2, 0, 2, 6) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(Ui.Text("搜索记录", 12, FontWeights.SemiBold, "TextSecondary"));
            var clear = Ui.Button("清空", false, 11);
            clear.Padding = new Thickness(8, 2, 8, 2);
            clear.Click += (_, _) =>
            {
                SearchHistoryStore.Clear();
                RenderHistory();
            };
            Grid.SetColumn(clear, 1);
            head.Children.Add(clear);
            _historyPanel.Children.Add(head);

            var wrap = new WrapPanel();
            foreach (var keyword in items)
            {
                var chip = new Border
                {
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(10, 5, 4, 5),
                    Margin = new Thickness(0, 0, 8, 8),
                };
                chip.SetResourceReference(Border.BackgroundProperty, "Surface");
                chip.SetResourceReference(Border.BorderBrushProperty, "Border");
                chip.BorderThickness = new Thickness(1);
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                var text = Ui.Text(keyword, 12, null, "TextSecondary");
                text.Cursor = Cursors.Hand;
                text.MouseLeftButtonUp += async (_, _) =>
                {
                    _input.Text = keyword;
                    await SearchAsync();
                };
                row.Children.Add(text);
                var remove = Ui.IconButton("\uE711", "删除记录", 22);
                remove.Margin = new Thickness(4, 0, 0, 0);
                remove.Click += (_, _) =>
                {
                    SearchHistoryStore.Remove(keyword);
                    RenderHistory();
                };
                row.Children.Add(remove);
                chip.Child = row;
                wrap.Children.Add(chip);
            }
            _historyPanel.Children.Add(wrap);
        }

        private void RenderEmpty()
        {
            _results.Children.Clear();
            var panel = new StackPanel { Margin = new Thickness(0, 22, 0, 0) };
            var icon = Ui.Icon("\uE721", 30);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.Opacity = 0.35;
            panel.Children.Add(icon);
            var hint = Ui.Text("输入关键词开始搜索，或试试下面的热门内容", 13, null, "TextTertiary");
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            hint.Margin = new Thickness(0, 12, 0, 16);
            panel.Children.Add(hint);

            var suggestions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 520 };
            foreach (var word in new[] { "孙笑川", "原神", "考研", "世界杯", "显卡", "摄影" })
            {
                var chip = new Border
                {
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(13, 6, 13, 6),
                    Margin = new Thickness(4, 0, 4, 8),
                    Cursor = Cursors.Hand,
                    Child = Ui.Text(word, 12.5, null, "TextSecondary"),
                };
                chip.SetResourceReference(BackgroundProperty, "Surface");
                chip.SetResourceReference(BorderBrushProperty, "Border");
                chip.BorderThickness = new Thickness(1);
                var captured = word;
                chip.MouseLeftButtonUp += async (_, _) =>
                {
                    _input.Text = captured;
                    await SearchAsync();
                };
                suggestions.Children.Add(chip);
            }
            panel.Children.Add(suggestions);
            _results.Children.Add(panel);
        }

        public void SetQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            _input.Text = query;
            _ = SearchAsync();
        }

        private async Task SearchAsync()
        {
            var query = _input.Text?.Trim();
            if (string.IsNullOrEmpty(query)) return;
            _lastQuery = query;
            SearchHistoryStore.Add(query);
            RenderHistory();
            _results.Children.Clear();
            _results.Children.Add(HomeView.Skeleton(3));
            _status.Text = "搜索中…";
            try
            {
                if (_searchingUsers) await SearchUsersListAsync(query);
                else if (_searchingThreads) await SearchThreadsAsync(query);
                else await SearchForumsAsync(query);
            }
            catch (Exception ex)
            {
                _results.Children.Clear();
                _results.Children.Add(HomeView.ErrorCard(ex.Message, SearchAsync));
                _status.Text = "";
            }
        }

        private async Task SearchUsersListAsync(string query)
        {
            var users = await Data.SearchUsersAsync(query);
            _results.Children.Clear();
            _status.Text = users.Count == 0 ? "没有找到相关用户" : "找到 " + users.Count + " 个用户";
            foreach (var user in users)
            {
                var card = Ui.Card(BuildUserResult(user), 16, 12);
                _results.Children.Add(card);
                Anim.FadeInUp(card, _results.Children.Count * 24, 12, 280);
            }
        }

        private UIElement BuildUserResult(UserInfo user)
        {
            UserCache.Put(user);
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(Ui.Avatar(user.Portrait, 44, user.DisplayName));
            var info = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
            nameRow.Children.Add(Ui.Text(user.DisplayName, 14, FontWeights.SemiBold));
            if (user.Level > 0)
            {
                var level = Ui.Chip("Lv." + user.Level, "Accent", "AccentSoft");
                level.Margin = new Thickness(8, 0, 0, 0);
                nameRow.Children.Add(level);
            }
            info.Children.Add(nameRow);
            info.Children.Add(Ui.Text("粉丝 " + Ui.ShortNum(user.FansNum), 11.5, null, "TextTertiary"));
            row.Children.Add(info);
            var host = new Border { Child = row, Background = System.Windows.Media.Brushes.Transparent, Cursor = Cursors.Hand };
            host.MouseLeftButtonUp += (_, _) => Svc.Window?.OpenProfile(user.Id, user.DisplayName);
            return host;
        }

        private async Task SearchThreadsAsync(string query)
        {
            var results = string.IsNullOrWhiteSpace(_forumFilter)
                ? await Data.SearchThreadsAsync(query, 1, null)
                : await Data.SearchForumAsync(_forumFilter, query);
            if (results.Count == 0 && !string.IsNullOrWhiteSpace(_forumFilter))
            {
                results = await Data.SearchThreadsAsync(query, 1, _forumFilter);
            }
            _results.Children.Clear();
            var scope = string.IsNullOrWhiteSpace(_forumFilter) ? "" : "（" + _forumFilter + "吧内）";
            _status.Text = results.Count == 0 ? "没有找到相关帖子" : "找到 " + results.Count + " 条结果" + scope;
            foreach (var item in results)
            {
                var card = Ui.Card(BuildThreadResult(item), 16, 14);
                _results.Children.Add(card);
                Anim.FadeInUp(card, _results.Children.Count * 24, 12, 280);
            }
        }

        private UIElement BuildThreadResult(SearchPostResult item)
        {
            var stack = new StackPanel { Cursor = Cursors.Hand };
            var title = Ui.Text(string.IsNullOrWhiteSpace(item.Title) ? "（无标题）" : item.Title, 15, FontWeights.SemiBold);
            stack.Children.Add(title);
            if (!string.IsNullOrWhiteSpace(item.Content))
            {
                var content = Ui.Text(item.Content, 12.5, null, "TextSecondary");
                content.Margin = new Thickness(0, 5, 0, 0);
                content.MaxHeight = 40;
                stack.Children.Add(content);
            }
            var meta = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 0) };
            if (!string.IsNullOrWhiteSpace(item.ForumName))
            {
                var chip = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(0, 0, 8, 0),
                    Child = Ui.Text(item.ForumName, 11, null, "Accent"),
                };
                chip.SetResourceReference(BackgroundProperty, "AccentSoft");
                meta.Children.Add(chip);
            }
            if (!string.IsNullOrWhiteSpace(item.AuthorName)) meta.Children.Add(Ui.Text(item.AuthorName, 11.5, null, "TextTertiary"));
            stack.Children.Add(meta);

            stack.MouseLeftButtonUp += (_, _) =>
            {
                if (item.ThreadId > 0) Svc.Window?.OpenThread(item.ThreadId, item.Title, item.ForumName);
            };
            return stack;
        }

        private async Task SearchForumsAsync(string query)
        {
            var results = await Data.SearchForumsAsync(query);
            _results.Children.Clear();
            _status.Text = results.Count == 0 ? "没有找到相关贴吧" : "找到 " + results.Count + " 个贴吧";
            foreach (var forum in results)
            {
                var card = Ui.Card(BuildForumResult(forum), 16, 14);
                _results.Children.Add(card);
            }
        }

        private UIElement BuildForumResult(SearchForumResult forum)
        {
            var grid = new Grid { Cursor = Cursors.Hand };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatar = Ui.Avatar(forum.Avatar, 44, forum.Name);
            avatar.CornerRadius = new CornerRadius(12);
            grid.Children.Add(avatar);

            var info = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(Ui.Text(forum.Name, 15, FontWeights.SemiBold));
            if (!string.IsNullOrWhiteSpace(forum.Desc))
            {
                var desc = Ui.Text(forum.Desc, 12, null, "TextTertiary");
                desc.Margin = new Thickness(0, 3, 0, 0);
                desc.MaxHeight = 20;
                info.Children.Add(desc);
            }
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);

            var stats = Ui.Text(Ui.ShortNum(forum.MemberNum) + " 关注", 11.5, null, "TextTertiary");
            Grid.SetColumn(stats, 2);
            grid.Children.Add(stats);

            grid.MouseLeftButtonUp += (_, _) => Svc.Window?.OpenForum(forum.Name);
            return grid;
        }
    }
}
