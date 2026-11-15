using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Views
{
    /// <summary>话题页：话题信息 + 相关帖子流。</summary>
    public sealed class TopicView : UserControl, IRefreshable
    {
        private readonly HotTopic _topic;
        private readonly StackPanel _root;
        private readonly StackPanel _threads;
        private readonly TextBlock _status;
        private int _page = 1;
        private bool _loading;
        private bool _hasMore;
        private Button _moreButton;
        private readonly HashSet<long> _seen = new();

        public TopicView(HotTopic topic)
        {
            _topic = topic ?? new HotTopic();
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            _root = new StackPanel
            {
                MaxWidth = 900,
                MinWidth = 720,
                Margin = new Thickness(28, 8, 28, 40),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _status = Ui.Text("", 12, null, "TextTertiary");
            _status.Margin = new Thickness(2, 8, 0, 6);
            _threads = new StackPanel();
            _moreButton = Ui.Button("加载更多讨论", false, 12.5);
            _moreButton.HorizontalAlignment = HorizontalAlignment.Center;
            _moreButton.Margin = new Thickness(0, 10, 0, 0);
            _moreButton.Visibility = Visibility.Collapsed;
            _moreButton.Click += async (_, _) =>
            {
                _page++;
                await LoadAsync();
            };
            _root.Children.Add(_status);
            _root.Children.Add(_threads);
            _root.Children.Add(_moreButton);
            scroll.Content = _root;
            Content = scroll;
            Loaded += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_loading) return;
            _loading = true;
            if (_page == 1)
            {
                _threads.Children.Clear();
                _threads.Children.Add(HomeView.Skeleton(3));
                _status.Text = "加载中…";
            }
            try
            {
                var page = await Data.TopicAsync(_topic.Id, _topic.Name, _page);
                var info = page.Info ?? _topic;
                if (_page == 1)
                {
                    _root.Children.Clear();
                    _root.Children.Add(BuildHeader(info));
                    _root.Children.Add(_status);
                    _root.Children.Add(_threads);
                    _root.Children.Add(_moreButton);
                    _threads.Children.Clear();
                    _seen.Clear();
                }
                var added = 0;
                var index = _threads.Children.Count;
                foreach (var thread in Data.Visible(page.Threads))
                {
                    if (thread.Id != 0 && !_seen.Add(thread.Id)) continue;
                    var card = Ui.Card(new ThreadRow(thread, true), 4, 16);
                    _threads.Children.Add(card);
                    Anim.FadeInUp(card, Math.Min(index, 6) * 28, 12, 280);
                    index++;
                    added++;
                }
                if (page.Threads.Count == 0 && _page == 1)
                {
                    _threads.Children.Add(HomeView.EmptyCard("这个热门话题还没有相关帖子"));
                }
                if (_page == 1 && _threads.Children.Count > 0 && _threads.Children.Count <= 2 && !Svc.DemoMode)
                {
                    // topicDetail often only returns one featured thread; top up with search hits
                    try
                    {
                        var extra = await Svc.Api.SearchThreadAsync(_topic.Name);
                        foreach (var hit in extra)
                        {
                            if (hit.ThreadId == 0 || !_seen.Add(hit.ThreadId)) continue;
                            var summary = new ThreadSummary
                            {
                                Id = hit.ThreadId,
                                Title = hit.Title,
                                ForumName = hit.ForumName,
                                PreviewText = hit.Content,
                                LastTimeInt = hit.Time,
                                Author = new UserInfo { ShowName = hit.AuthorName, Name = hit.AuthorName },
                            };
                            var card = Ui.Card(new ThreadRow(summary, true), 4, 16);
                            _threads.Children.Add(card);
                            Anim.FadeInUp(card, Math.Min(_threads.Children.Count, 8) * 26, 12, 280);
                            added++;
                        }
                        if (added > 0) _status.Text = "共 " + _threads.Children.Count + " 条相关讨论";
                    }
                    catch
                    {
                    }
                }
                _hasMore = page.HasMore && added > 0 && _threads.Children.Count <= 2;
                if (added > 0) _status.Text = "共 " + _threads.Children.Count + " 条相关讨论";
                else _status.Text = "没有更多内容了";
                if (_moreButton != null) _moreButton.Visibility = _hasMore ? Visibility.Visible : Visibility.Collapsed;
                Svc.Window?.SetSubtitle("热门话题");
            }
            catch (Exception ex)
            {
                if (_page == 1)
                {
                    _root.Children.Clear();
                    _root.Children.Add(BuildHeader(_topic));
                    var error = HomeView.ErrorCard(ex.Message, async () =>
                    {
                        _page = 1;
                        _loading = false;
                        await LoadAsync();
                    });
                    _root.Children.Add(error);
                }
                _status.Text = "";
            }
            finally
            {
                _loading = false;
            }
        }

        private UIElement BuildHeader(HotTopic info)
        {
            var card = new Border { CornerRadius = new CornerRadius(20), Margin = new Thickness(0, 4, 0, 12) };
            card.SetResourceReference(BackgroundProperty, "Surface");
            card.SetResourceReference(BorderBrushProperty, "Border");
            card.BorderThickness = new Thickness(1);

            var body = new StackPanel { Margin = new Thickness(20, 18, 20, 16) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            if (!string.IsNullOrWhiteSpace(info.Image))
            {
                var cover = new RemoteImage
                {
                    Width = 132,
                    Height = 92,
                    Url = info.Image,
                    DecodeWidth = 320,
                    CornerRadius = new CornerRadius(12),
                    VerticalAlignment = VerticalAlignment.Top,
                };
                grid.Children.Add(cover);
            }

            var text = new StackPanel { Margin = new Thickness(string.IsNullOrWhiteSpace(info.Image) ? 0 : 16, 0, 0, 0) };
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            titleRow.Children.Add(Ui.Chip("热议", "Accent", "AccentSoft"));
            var title = Ui.Text(string.IsNullOrWhiteSpace(info.Name) ? "热门话题" : info.Name, 20, FontWeights.Bold);
            title.Margin = new Thickness(8, 0, 0, 0);
            title.TextWrapping = TextWrapping.Wrap;
            titleRow.Children.Add(title);
            text.Children.Add(titleRow);

            if (!string.IsNullOrWhiteSpace(info.Desc))
            {
                var desc = Ui.Text(info.Desc, 12.5, null, "TextSecondary");
                desc.Margin = new Thickness(0, 8, 0, 0);
                desc.TextWrapping = TextWrapping.Wrap;
                text.Children.Add(desc);
            }

            var meta = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            meta.Children.Add(Ui.Text(Ui.ShortNum(info.DiscussNum) + " 讨论", 12, null, "TextTertiary"));
            var browser = Ui.Button("在浏览器打开", false, 12);
            browser.Margin = new Thickness(14, 0, 0, 0);
            browser.Padding = new Thickness(12, 4, 12, 4);
            browser.Click += (_, _) =>
            {
                if (Ui.OpenUrl(info.WebUrl)) Svc.Window?.ShowToast("已在浏览器打开话题页");
                else Svc.Window?.ShowToast("无法打开浏览器，链接已复制到剪贴板");
            };
            meta.Children.Add(browser);
            text.Children.Add(meta);

            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            body.Children.Add(grid);

            card.Child = body;
            return card;
        }

        public async Task RefreshAsync()
        {
            _page = 1;
            _loading = false;
            await LoadAsync();
        }
    }
}
