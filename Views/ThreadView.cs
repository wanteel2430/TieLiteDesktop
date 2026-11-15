using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Views
{
    /// <summary>帖子页：楼层列表 + 只看楼主/倒序 + 回复框。</summary>
    public sealed class ThreadView : UserControl, IRefreshable
    {
        private readonly long _threadId;
        private readonly string _forumName;
        private readonly string _initialTitle;
        private readonly PollInfoModel _fallbackPoll;
        private readonly StackPanel _postList;
        private readonly ScrollViewer _scroll;
        private readonly Border _headerHost;
        private readonly TextBlock _pageLabel;
        private readonly TextBox _replyBox;
        private readonly TextBlock _replyHint;
        private readonly Button _cancelReply;
        private Button _backToTop;
        private Button _nextButton;
        private Button _prevButton;
        private TextBlock _endLabel;
        private StackPanel _pollHost;
        private ThreadPage _data;
        private int _page = 1;
        private bool _seeLz;
        private bool _reverse;
        private bool _reverseInitialized;
        private bool _scrollTopOnLoad;
        private bool _loading;
        private PostItem _replyTarget;
        private Button _favoriteButton;
        private bool _isFavorite;
        private bool _bawuLoaded;
        private bool _isBawu;
        private List<BawuMember> _bawuTeam = new();

        public ThreadView(long threadId, string forumName, string title, PollInfoModel poll = null)
        {
            _threadId = threadId;
            _forumName = forumName;
            _initialTitle = title;
            _fallbackPoll = poll;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            var root = new StackPanel
            {
                MaxWidth = 1080,
                MinWidth = 780,
                Margin = new Thickness(28, 6, 28, 24),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            _headerHost = new Border { CornerRadius = new CornerRadius(20), Margin = new Thickness(0, 4, 0, 14) };
            _headerHost.SetResourceReference(BackgroundProperty, "Surface");
            _headerHost.SetResourceReference(BorderBrushProperty, "Border");
            _headerHost.BorderThickness = new Thickness(1);
            root.Children.Add(_headerHost);

            _postList = new StackPanel();
            root.Children.Add(_postList);

            var pager = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
            var prev = Ui.Button("上一页", false, 12);
            _prevButton = prev;
            prev.Click += async (_, _) =>
            {
                _scrollTopOnLoad = true;
                await LoadPageAsync(Math.Max(1, _page - 1));
            };
            var next = Ui.Button("下一页", false, 12);
            _nextButton = next;
            next.Margin = new Thickness(8, 0, 0, 0);
            next.Click += async (_, _) =>
            {
                var total = Math.Max(1, _data?.Page?.TotalPage ?? 1);
                _scrollTopOnLoad = true;
                await LoadPageAsync(Math.Min(total, _page + 1));
            };
            _pageLabel = Ui.Text("第 1 页", 12, null, "TextTertiary");
            _pageLabel.Margin = new Thickness(12, 0, 12, 0);
            _pageLabel.VerticalAlignment = VerticalAlignment.Center;
            pager.Children.Add(prev);
            pager.Children.Add(_pageLabel);

            var jumpHost = CreateJumpBox(out var jumpBox);
            jumpHost.Margin = new Thickness(4, 0, 6, 0);
            jumpBox.KeyDown += async (_, e) =>
            {
                if (e.Key != Key.Enter) return;
                e.Handled = true;
                await JumpToFloorAsync(jumpBox.Text);
            };
            pager.Children.Add(jumpHost);
            var jump = Ui.Button("跳转", false, 12);
            jump.Margin = new Thickness(0, 0, 8, 0);
            jump.Click += async (_, _) => await JumpToFloorAsync(jumpBox.Text);
            pager.Children.Add(jump);

            pager.Children.Add(next);
            root.Children.Add(pager);

            _endLabel = Ui.Text("已经到底了", 11.5, null, "TextTertiary");
            _endLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _endLabel.Margin = new Thickness(0, 8, 0, 4);
            _endLabel.Visibility = Visibility.Collapsed;
            root.Children.Add(_endLabel);

            _scroll.Content = root;
            Grid.SetRow(_scroll, 0);
            grid.Children.Add(_scroll);

            _backToTop = Ui.IconButton("\uE74A", "返回顶部", 42);
            _backToTop.HorizontalAlignment = HorizontalAlignment.Right;
            _backToTop.VerticalAlignment = VerticalAlignment.Bottom;
            _backToTop.Margin = new Thickness(0, 0, 28, 84);
            _backToTop.Visibility = Visibility.Collapsed;
            Panel.SetZIndex(_backToTop, 20);
            _backToTop.Click += (_, _) => AnimateScrollToTop();
            grid.Children.Add(_backToTop);
            _scroll.ScrollChanged += (_, _) =>
            {
                _backToTop.Visibility = _scroll.VerticalOffset > 320 ? Visibility.Visible : Visibility.Collapsed;
            };

            // reply composer
            var replyBar = new Border
            {
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
            };
            var replyCapsule = new Border
            {
                CornerRadius = new CornerRadius(18),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18, 9, 18, 9),
                Margin = new Thickness(28, 8, 28, 12),
                Width = 1024,
                HorizontalAlignment = HorizontalAlignment.Center,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 18,
                    ShadowDepth = 2,
                    Direction = 270,
                    Opacity = 0.16,
                },
            };
            replyCapsule.SetResourceReference(BackgroundProperty, "Surface");
            replyCapsule.SetResourceReference(BorderBrushProperty, "Border");
            void SizeReplyCapsule()
            {
                var available = ActualWidth > 0 ? ActualWidth : 1184;
                replyCapsule.Width = Math.Max(320, Math.Min(1024, available - 56));
            }
            SizeChanged += (_, _) => SizeReplyCapsule();
            SizeReplyCapsule();
            var replyGrid = new Grid();
            replyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            replyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var boxHost = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 8),
                MinHeight = 44,
                MaxHeight = 120,
            };
            boxHost.SetResourceReference(BackgroundProperty, "SurfaceAlt");
            boxHost.SetResourceReference(BorderBrushProperty, "Border");
            boxHost.BorderThickness = new Thickness(1);
            var boxStack = new StackPanel();
            _replyHint = Ui.Text("写下你的回复…", 11.5, null, "TextTertiary");
            _replyHint.Visibility = Visibility.Collapsed;
            _replyHint.Margin = new Thickness(0, 0, 0, 2);
            _cancelReply = new Button
            {
                Content = "取消",
                FontSize = 11,
                Padding = new Thickness(6, 0, 6, 0),
                Margin = new Thickness(8, 0, 0, 0),
                Visibility = Visibility.Collapsed,
                Cursor = Cursors.Hand,
            };
            _cancelReply.SetResourceReference(FrameworkElement.StyleProperty, "GhostButton");
            _cancelReply.Click += (_, _) => ClearReplyTarget();
            var hintRow = new StackPanel { Orientation = Orientation.Horizontal };
            hintRow.Children.Add(_replyHint);
            hintRow.Children.Add(_cancelReply);
            boxStack.Children.Add(hintRow);
            _replyBox = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 90, FontSize = 13.5 };
            _replyBox.SetResourceReference(FrameworkElement.StyleProperty, "ReplyBox");
            _replyBox.SetResourceReference(TextBox.FontFamilyProperty, "UiFont");
            _replyBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimary");
            _replyBox.SetResourceReference(TextBox.CaretBrushProperty, "TextPrimary");
            var watermark = Ui.Text("写下你的回复…", 13.5, null, "TextTertiary");
            watermark.IsHitTestVisible = false;
            watermark.VerticalAlignment = VerticalAlignment.Center;
            watermark.Margin = new Thickness(2, 0, 0, 0);
            var boxGrid = new Grid();
            boxGrid.Children.Add(watermark);
            boxGrid.Children.Add(_replyBox);
            _replyBox.TextChanged += (_, _) => watermark.Visibility = string.IsNullOrEmpty(_replyBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            boxStack.Children.Add(boxGrid);
            _replyBox.KeyDown += async (_, e) =>
            {
                if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    e.Handled = true;
                    await SendReplyAsync();
                }
            };
            boxHost.Child = boxStack;
            replyGrid.Children.Add(boxHost);

            var replyActions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(10, 0, 0, 0),
            };
            var attachImage = Ui.Button("图片", false, 12);
            attachImage.Margin = new Thickness(0, 0, 8, 0);
            attachImage.Click += async (_, _) =>
            {
                if (!Svc.Account.IsLoggedIn)
                {
                    Svc.Window?.ShowToast("请先登录后再发图");
                    return;
                }
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "图片|*.jpg;*.jpeg;*.png;*.gif;*.webp|所有文件|*.*",
                    Title = "选择要上传的图片",
                };
                if (dialog.ShowDialog() != true) return;
                try
                {
                    Svc.Window?.ShowToast("图片上传中…");
                    var forumName = _data?.Forum?.Name ?? _forumName;
                    var url = await Svc.Api.UploadPictureAsync(dialog.FileName, forumName);
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        Svc.Window?.ShowToast("图片上传失败");
                        return;
                    }
                    _replyBox.Text = (_replyBox.Text ?? "") + "[img]" + url + "[/img]";
                    _replyBox.CaretIndex = _replyBox.Text.Length;
                    Svc.Window?.ShowToast("图片已插入，发送后生效");
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("图片上传失败：" + ex.Message);
                }
            };
            replyActions.Children.Add(attachImage);
            var send = Ui.Button("发送", true, 12.5);
            send.Click += async (_, _) => await SendReplyAsync();
            replyActions.Children.Add(send);
            Grid.SetColumn(replyActions, 1);
            replyGrid.Children.Add(replyActions);

            replyCapsule.Child = replyGrid;
            replyBar.Child = replyCapsule;
            Grid.SetRow(replyBar, 1);
            grid.Children.Add(replyBar);

            Content = grid;
            var savedPosition = ReadingPositionStore.Get(_threadId);
            Loaded += async (_, _) =>
            {
                await LoadPageAsync(savedPosition?.Page ?? 1);
                if (savedPosition != null && savedPosition.Offset > 1)
                {
                    await Task.Delay(320);
                    _scroll.UpdateLayout();
                    _scroll.ScrollToVerticalOffset(savedPosition.Offset);
                }
            };
            Unloaded += (_, _) => ReadingPositionStore.Save(_threadId, _page, _scroll.VerticalOffset);
        }

        private void AnimateScrollToTop()
        {
            var start = _scroll.VerticalOffset;
            if (start <= 1) return;
            _scroll.BeginAnimation(Anim.ScrollOffsetProperty, null);
            var animation = new System.Windows.Media.Animation.DoubleAnimation(start, 0,
                TimeSpan.FromMilliseconds(420))
            {
                EasingFunction = Anim.EaseOut,
            };
            animation.Completed += (_, _) => _scroll.ScrollToVerticalOffset(0);
            _scroll.BeginAnimation(Anim.ScrollOffsetProperty, animation);
        }

        private async System.Threading.Tasks.Task JumpToFloorAsync(string text)
        {
            if (!int.TryParse(text?.Trim(), out var floor) || floor <= 0)
            {
                Svc.Window?.ShowToast("请输入要跳转的楼层号");
                return;
            }
            var size = _data?.Page?.PageSize ?? 0;
            if (size <= 0) size = 30;
            var total = Math.Max(1, _data?.Page?.TotalPage ?? 1);
            var page = Math.Clamp((floor - 1) / size + 1, 1, total);
            await LoadPageAsync(page);
            var target = _postList.Children.OfType<PostCard>().FirstOrDefault(c => c.Post.Floor >= floor);
            target?.BringIntoView();
        }

        private static Border CreateJumpBox(out TextBox input)
        {
            input = new TextBox
            {
                FontSize = 12.5,
                Padding = new Thickness(8, 0, 8, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            input.SetResourceReference(TextBox.ForegroundProperty, "TextPrimary");
            input.SetResourceReference(TextBox.CaretBrushProperty, "TextPrimary");
            var placeholder = Ui.Text("楼层", 12, null, "TextTertiary");
            placeholder.IsHitTestVisible = false;
            placeholder.VerticalAlignment = VerticalAlignment.Center;
            placeholder.Margin = new Thickness(9, 0, 0, 0);
            var grid = new Grid();
            var box = input;
            grid.Children.Add(box);
            grid.Children.Add(placeholder);
            box.TextChanged += (_, _) =>
                placeholder.Visibility = string.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed;
            var host = new Border
            {
                Width = 92,
                Height = 32,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                ToolTip = "输入楼层号后回车跳转",
            };
            host.SetResourceReference(BackgroundProperty, "SurfaceAlt");
            host.SetResourceReference(BorderBrushProperty, "BorderStrong");
            host.Child = grid;
            return host;
        }

        private async Task LoadPageAsync(int page)
        {
            if (_loading) return;
            _loading = true;
            if (page == 1)
            {
                _postList.Children.Clear();
                _postList.Children.Add(HomeView.Skeleton(3));
            }
            try
            {
                var firstPage = page == 1;
                var data = await Data.ThreadAsync(_threadId, page, _seeLz, _reverse);
                if (_reverse && page == 1 && !_reverseInitialized && data.Page != null && data.Page.TotalPage > 1)
                {
                    page = data.Page.TotalPage;
                    data = await Data.ThreadAsync(_threadId, page, _seeLz, true);
                }
                if (_reverse) _reverseInitialized = true;
                if (_reverse) data.Posts.Sort((a, b) => b.Floor.CompareTo(a.Floor));
                if (firstPage)
                {
                    _data = data;
                    _postList.Children.Clear();
                    RenderHeader();
                }
                else
                {
                    _postList.Children.Clear();
                    _data.Posts.Clear();
                    _data.Posts.AddRange(data.Posts);
                    _data.Page = data.Page;
                }

                var forumId = _data.Forum?.Id ?? _data.Thread?.ForumId ?? 0;
                if (!_bawuLoaded && forumId > 0 && Svc.Account.IsLoggedIn)
                {
                    _bawuLoaded = true;
                    try
                    {
                        _bawuTeam = await Svc.Api.GetBawuTeamAsync(forumId);
                        _isBawu = _bawuTeam.Any(m => m.Uid == Svc.Account.Uid);
                        if (_isBawu) RenderHeader();
                    }
                    catch
                    {
                    }
                }

                var canModerate = _isBawu;
                var postIndex = 0;
                foreach (var post in data.Posts)
                {
                    var mine = post.AuthorId == Svc.Account.Uid && Svc.Account.Uid != 0;
                    var card = new PostCard(post, _threadId, forumId, _data.Forum?.Name ?? _forumName,
                        OnReplyTo, post.IsOp, canModerate || mine);
                    _postList.Children.Add(card);
                    if (page == 1 && postIndex < 8)
                    {
                        Anim.FadeInUp(card, postIndex * 30, 14, 300);
                    }
                    postIndex++;
                }
                _page = page;
                var totalPages = Math.Max(1, _data.Page?.TotalPage ?? 1);
                _pageLabel.Text = "第 " + page + " / " + totalPages + " 页";
                if (_nextButton != null) _nextButton.IsEnabled = page < totalPages;
                if (_prevButton != null) _prevButton.IsEnabled = page > 1;
                if (_endLabel != null)
                {
                    _endLabel.Visibility = (_reverse ? page <= 1 : page >= totalPages) ? Visibility.Visible : Visibility.Collapsed;
                }
                Svc.Window?.SetSubtitle(_data.Forum?.Name);
                if (_scrollTopOnLoad)
                {
                    _scrollTopOnLoad = false;
                    await Task.Delay(120);
                    _scroll.UpdateLayout();
                    AnimateScrollToTop();
                }
                else if (page == 1)
                {
                    _scroll.UpdateLayout();
                    _scroll.ScrollToTop();
                }
            }
            catch (Exception ex)
            {
                if (page == 1)
                {
                    _postList.Children.Clear();
                    _postList.Children.Add(HomeView.ErrorCard(ex.Message, () => LoadPageAsync(1)));
                }
                Svc.Window?.ShowToast(ex.Message);
            }
            finally
            {
                _loading = false;
            }
        }

        private UIElement BuildPoll(PollInfoModel poll)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 12),
            };
            card.SetResourceReference(BackgroundProperty, "SurfaceAlt");

            var panel = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(Ui.Chip("投票", "Accent", "AccentSoft"));
            var question = Ui.Text(poll.Title, 14, FontWeights.SemiBold);
            question.Margin = new Thickness(8, 0, 0, 0);
            head.Children.Add(question);
            panel.Children.Add(head);

            var total = poll.Options.Sum(o => o.Num);
            var meta = Ui.Text("共 " + Ui.ShortNum(poll.TotalNum > 0 ? poll.TotalNum : total) + " 人参与" +
                               (poll.IsPolled ? " · 你已投票" : ""), 11.5, null, "TextTertiary");
            meta.Margin = new Thickness(0, 4, 0, 8);
            panel.Children.Add(meta);

            foreach (var option in poll.Options)
            {
                var percent = total > 0 ? option.Num * 100.0 / total : 0;
                var row = BuildPollOption(option, percent, poll);
                panel.Children.Add(row);
            }
            card.Child = panel;
            return card;
        }

        private UIElement BuildPollOption(PollOption option, double percent, PollInfoModel poll)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 7, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand,
            };
            border.SetResourceReference(BackgroundProperty, "Surface");
            border.MouseEnter += (_, _) => border.SetResourceReference(BackgroundProperty, "SurfaceHover");
            border.MouseLeave += (_, _) => border.SetResourceReference(BackgroundProperty, "Surface");

            var stack = new StackPanel();
            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = Ui.Text(option.Text, 13, FontWeights.Medium);
            line.Children.Add(text);
            var right = Ui.Text(Ui.ShortNum(option.Num) + " 票 · " + percent.ToString("0.#") + "%", 11.5, null, "TextTertiary");
            Grid.SetColumn(right, 1);
            line.Children.Add(right);
            stack.Children.Add(line);

            var bar = new Grid { Height = 5, Margin = new Thickness(0, 6, 0, 0) };
            var fill = new Border { CornerRadius = new CornerRadius(3) };
            fill.SetResourceReference(BackgroundProperty, poll.IsPolled ? "Accent" : "BorderStrong");
            var rest = new Border { CornerRadius = new CornerRadius(3) };
            rest.SetResourceReference(BackgroundProperty, "SurfaceHover");
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.5, percent), GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.5, 100 - percent), GridUnitType.Star) });
            Grid.SetColumn(fill, 0);
            Grid.SetColumn(rest, 1);
            bar.Children.Add(fill);
            bar.Children.Add(rest);
            stack.Children.Add(bar);

            border.Child = stack;
            border.MouseLeftButtonUp += async (_, _) => await VoteAsync(poll, option);
            return border;
        }

        private async System.Threading.Tasks.Task VoteAsync(PollInfoModel poll, PollOption option)
        {
            if (poll.IsPolled)
            {
                Svc.Window?.ShowToast("你已经投过票了");
                return;
            }
            if (!Svc.Account.IsLoggedIn)
            {
                Svc.Window?.ShowToast("登录后才能投票");
                return;
            }
            try
            {
                var forumId = _data?.Forum?.Id ?? 0;
                await Svc.Api.AddPollPostAsync(_threadId, forumId, option.Id);
                poll.IsPolled = true;
                option.Num++;
                poll.TotalNum++;
                if (_pollHost != null)
                {
                    _pollHost.Children.Clear();
                    _pollHost.Children.Add(BuildPoll(poll));
                }
                Svc.Window?.ShowToast("投票成功");
            }
            catch (Exception ex)
            {
                Svc.Window?.ShowToast(ex.Message);
            }
        }

        private void RenderHeader()
        {
            var thread = _data?.Thread ?? new ThreadSummary { Title = _initialTitle, Id = _threadId };
            var stack = new StackPanel { Margin = new Thickness(22, 18, 22, 16) };

            var forumRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var displayForum = string.IsNullOrWhiteSpace(thread.ForumName) ? _forumName : thread.ForumName;
            if (!string.IsNullOrWhiteSpace(displayForum))
            {
                var forumChip = new Border
                {
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(8, 2, 8, 2),
                    Cursor = Cursors.Hand,
                    Child = Ui.Text(displayForum, 11.5, FontWeights.Medium, "Accent"),
                };
                forumChip.SetResourceReference(BackgroundProperty, "AccentSoft");
                forumChip.MouseLeftButtonUp += (_, _) => Svc.Window?.OpenForum(displayForum);
                forumRow.Children.Add(forumChip);
            }
            if (thread.IsGood)
            {
                var good = Ui.Chip("精品", "Success");
                good.Margin = new Thickness(6, 0, 0, 0);
                forumRow.Children.Add(good);
            }
            stack.Children.Add(forumRow);

            var title = Ui.Text(string.IsNullOrWhiteSpace(thread.Title) ? _initialTitle : thread.Title, 23, FontWeights.Bold);
            title.TextWrapping = TextWrapping.Wrap;
            title.Margin = new Thickness(0, 0, 0, 10);
            stack.Children.Add(title);

            var poll = thread.Poll ?? _fallbackPoll;
            if (poll != null)
            {
                _pollHost = new StackPanel();
                _pollHost.Children.Add(BuildPoll(poll));
                stack.Children.Add(_pollHost);
            }

            var meta = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            var author = thread.Author?.DisplayName;
            if (!string.IsNullOrWhiteSpace(author))
            {
                var authorText = Ui.Text(author, 12.5, FontWeights.Medium, "TextSecondary");
                authorText.Margin = new Thickness(0, 0, 14, 0);
                authorText.Cursor = Cursors.Hand;
                if (thread.Author.Id > 0)
                {
                    authorText.MouseLeftButtonUp += (_, _) => Svc.Window?.OpenProfile(thread.Author.Id, thread.Author.DisplayName);
                }
                meta.Children.Add(authorText);
            }
            var stats = new StringBuilder();
            stats.Append(Ui.ShortNum(thread.ReplyNum)).Append(" 回复");
            if (thread.ViewNum > 0)
            {
                stats.Append(" · ").Append(Ui.ShortNum(thread.ViewNum)).Append(" 浏览");
            }
            if (thread.CreateTime > 0) stats.Append(" · ").Append(Ui.TimeFull(thread.CreateTime));
            meta.Children.Add(Ui.Text(stats.ToString(), 12, null, "TextSecondary"));
            stack.Children.Add(meta);

            var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
            var onlyOp = new CheckBox { Content = "只看楼主", IsChecked = _seeLz, Margin = new Thickness(0, 0, 8, 0) };
            onlyOp.SetResourceReference(FrameworkElement.StyleProperty, "PillCheck");
            onlyOp.Checked += async (_, _) =>
            {
                if (_seeLz) return;
                _seeLz = true;
                _reverseInitialized = false;
                await LoadPageAsync(1);
            };
            onlyOp.Unchecked += async (_, _) =>
            {
                if (!_seeLz) return;
                _seeLz = false;
                _reverseInitialized = false;
                await LoadPageAsync(1);
            };
            buttons.Children.Add(onlyOp);

            var reverse = new CheckBox { Content = "倒序", IsChecked = _reverse, Margin = new Thickness(0, 0, 8, 0) };
            reverse.SetResourceReference(FrameworkElement.StyleProperty, "PillCheck");
            reverse.Checked += async (_, _) =>
            {
                if (_reverse) return;
                _reverse = true;
                _reverseInitialized = false;
                await LoadPageAsync(1);
            };
            reverse.Unchecked += async (_, _) =>
            {
                if (!_reverse) return;
                _reverse = false;
                _reverseInitialized = false;
                await LoadPageAsync(1);
            };
            buttons.Children.Add(reverse);

            var copy = Ui.Button("复制链接", false, 12);
            Anim.HoverScale(copy, 1.05);
            copy.Click += async (_, _) =>
            {
                var ok = await ClipboardHelper.SetTextAsync("https://tieba.baidu.com/p/" + _threadId);
                Svc.Window?.ShowToast(ok ? "链接已复制" : "复制失败，剪贴板被占用");
            };
            buttons.Children.Add(copy);

            _favoriteButton = Ui.Button("收藏", false, 12);
            _favoriteButton.Margin = new Thickness(8, 0, 0, 0);
            _favoriteButton.Click += async (_, _) => await ToggleFavoriteAsync();
            buttons.Children.Add(_favoriteButton);

            var cache = Ui.Button("缓存", false, 12);
            cache.Margin = new Thickness(8, 0, 0, 0);
            cache.Click += async (_, _) =>
            {
                cache.IsEnabled = false;
                try
                {
                    Svc.Window?.ShowToast("正在缓存全部楼层…");
                    var first = await Data.ThreadAsync(_threadId, 1, false, false);
                    if (first == null || first.Posts.Count == 0)
                    {
                        Svc.Window?.ShowToast("当前帖子还没有内容可缓存");
                        return;
                    }
                    var total = Math.Max(1, first.Page?.TotalPage ?? 1);
                    var maxPages = Math.Clamp(Svc.Settings.CacheMaxPages, 1, 200);
                    var cachedPages = Math.Min(total, maxPages);
                    var posts = new List<PostItem>(first.Posts);
                    for (var page = 2; page <= cachedPages; page++)
                    {
                        try
                        {
                            var more = await Data.ThreadAsync(_threadId, page, false, false);
                            if (more?.Posts != null) posts.AddRange(more.Posts);
                        }
                        catch
                        {
                            break;
                        }
                    }
                    if (Svc.Settings.CacheOnlyOp)
                    {
                        posts = posts.Where(post => post.IsOp).ToList();
                    }
                    var snapshot = new ThreadPage
                    {
                        Thread = first.Thread,
                        Forum = first.Forum,
                        Page = first.Page,
                    };
                    snapshot.Posts.AddRange(posts);
                    await CachedThreadStore.SaveAsync(snapshot, Svc.Settings.CacheOnlyOp, cachedPages);
                    Svc.Window?.ShowToast("已缓存 " + posts.Count + " 个楼层（" +
                                          (Svc.Settings.CacheOnlyOp ? "只看楼主" : "全部楼层") + "，" +
                                          cachedPages + " 页，含图片）");
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("缓存失败：" + ex.Message);
                }
                finally
                {
                    cache.IsEnabled = true;
                }
            };
            buttons.Children.Add(cache);
            _ = RefreshFavoriteStateAsync();

            var headerJumpHost = CreateJumpBox(out var headerJumpBox);
            headerJumpHost.Margin = new Thickness(10, 0, 6, 0);
            headerJumpBox.KeyDown += async (_, e) =>
            {
                if (e.Key != Key.Enter) return;
                e.Handled = true;
                await JumpToFloorAsync(headerJumpBox.Text);
            };
            buttons.Children.Add(headerJumpHost);
            var headerJump = Ui.Button("跳至楼层", false, 12);
            headerJump.Click += async (_, _) => await JumpToFloorAsync(headerJumpBox.Text);
            buttons.Children.Add(headerJump);

            var mineThread = Svc.Account.IsLoggedIn && thread.Author != null && thread.Author.Id == Svc.Account.Uid;
            if (_isBawu || mineThread)
            {
                var delete = Ui.Button("删除主题", false, 12);
                delete.Margin = new Thickness(8, 0, 0, 0);
                delete.Click += async (_, _) => await DeleteThreadAsync(mineThread);
                buttons.Children.Add(delete);
            }
            stack.Children.Add(buttons);

            if (_bawuTeam.Count > 0)
            {
                var team = string.Join("、", _bawuTeam.Select(m => m.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(6));
                var teamText = Ui.Text("吧务团队：" + team, 11.5, null, "TextTertiary");
                teamText.Margin = new Thickness(0, 10, 0, 0);
                teamText.TextWrapping = TextWrapping.Wrap;
                stack.Children.Add(teamText);
            }

            _headerHost.Child = stack;
        }

        private static UIElement MetaItem(UIElement icon, string text)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 16, 0) };
            icon.Opacity = 0.7;
            panel.Children.Add(icon);
            var label = Ui.Text(" " + text, 12, null, "TextSecondary");
            label.Margin = new Thickness(3, 0, 0, 0);
            panel.Children.Add(label);
            return panel;
        }

        private async System.Threading.Tasks.Task RefreshFavoriteStateAsync()
        {
            if (!Svc.Account.IsLoggedIn)
            {
                _favoriteButton.IsEnabled = false;
                return;
            }
            try
            {
                _isFavorite = await Svc.Api.IsThreadStoredAsync(_threadId);
                _favoriteButton.Content = _isFavorite ? "已收藏" : "收藏";
            }
            catch
            {
            }
        }

        private async System.Threading.Tasks.Task ToggleFavoriteAsync()
        {
            if (!Svc.Account.IsLoggedIn)
            {
                Svc.Window?.ShowToast("登录后才能收藏");
                return;
            }
            try
            {
                if (_isFavorite)
                {
                    await Svc.Api.RemoveFavoriteAsync(_threadId, _data?.Forum?.Id ?? 0);
                    _isFavorite = false;
                    _favoriteButton.Content = "收藏";
                    Svc.Window?.ShowToast("已取消收藏");
                }
                else
                {
                    var postId = _data?.Thread?.FirstPostId ?? 0;
                    if (postId == 0) postId = _data?.Posts?.FirstOrDefault()?.Id ?? 0;
                    await Svc.Api.AddFavoriteAsync(_threadId, postId);
                    _isFavorite = true;
                    _favoriteButton.Content = "已收藏";
                    Svc.Window?.ShowToast("已收藏");
                }
            }
            catch (Exception ex)
            {
                Svc.Window?.ShowToast("收藏失败：" + ex.Message);
            }
        }

        private async System.Threading.Tasks.Task DeleteThreadAsync(bool mine)
        {
            if (MessageBox.Show("确定删除这个主题？删除后不可恢复。", "删除主题", MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning) != MessageBoxResult.OK)
            {
                return;
            }
            try
            {
                var forumId = _data?.Forum?.Id ?? 0;
                var forumName = _data?.Forum?.Name ?? _forumName;
                await Svc.Api.DeleteThreadAsync(forumId, forumName, _threadId, mine);
                Svc.Window?.ShowToast("主题已删除");
                Svc.Nav.Back();
            }
            catch (Exception ex)
            {
                Svc.Window?.ShowToast(ex.Message);
            }
        }

        private void OnReplyTo(PostItem post)
        {
            _replyTarget = post;
            _replyHint.Text = "回复 " + (post.Author?.DisplayName ?? "该用户") + "（#" + post.Floor + "楼）";
            _replyHint.Visibility = Visibility.Visible;
            _cancelReply.Visibility = Visibility.Visible;
            _replyBox.Focus();
        }

        private void ClearReplyTarget()
        {
            _replyTarget = null;
            _replyHint.Text = "";
            _replyHint.Visibility = Visibility.Collapsed;
            _cancelReply.Visibility = Visibility.Collapsed;
        }

        private async Task SendReplyAsync()
        {
            var text = _replyBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(Svc.Settings?.TailText))
            {
                text = (text ?? "") + "\n" + Svc.Settings.TailText.Trim();
            }
            if (string.IsNullOrEmpty(text))
            {
                Svc.Window?.ShowToast("回复内容不能为空");
                return;
            }
            if (!Svc.Account.IsLoggedIn)
            {
                Svc.Window?.ShowToast("请先登录后再回复");
                return;
            }
            try
            {
                var forumId = _data?.Forum?.Id ?? 0;
                var forumName = _data?.Forum?.Name ?? _forumName;
                if (!PostRiskGuard.Confirm()) return;
                await Svc.Api.AddPostAsync(text, forumId, forumName, _threadId,
                    _replyTarget?.Id, null, _replyTarget?.AuthorId);
                _replyBox.Text = "";
                ClearReplyTarget();
                Svc.Window?.ShowToast("回复成功");
                await LoadPageAsync(Math.Max(1, _data?.Page?.TotalPage ?? 1));
            }
            catch (Exception ex)
            {
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
