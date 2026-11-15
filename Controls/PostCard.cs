using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Api;
using TieLiteDesktop.Core.Models;
using TieLiteDesktop.Views;

namespace TieLiteDesktop.Controls
{
    /// <summary>A single floor (post) inside the thread page.</summary>
    public sealed class PostCard : Border
    {
        public PostItem Post => _post;

        private readonly PostItem _post;
        private readonly long _threadId;
        private readonly long _forumId;
        private readonly string _forumName;
        private readonly Action<PostItem> _onReply;
        private readonly StackPanel _subPanel;
        private readonly bool _canDelete;
        private readonly StackPanel _subList = new();
        private readonly StackPanel _subFooter = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 2) };
        private readonly List<SubPost> _subAll = new();
        private int _subShown;
        private int _subPage = 1;
        private bool _subsLoaded;
        private bool _subsExhausted;
        private VideoInfoModel _fallbackVideo;
        private readonly TextBlock _agreeLabel;

        public PostCard(PostItem post, long threadId, long forumId, string forumName, Action<PostItem> onReply,
            bool isOp = false, bool canDelete = false)
        {
            _post = post;
            _threadId = threadId;
            _forumId = forumId;
            _forumName = forumName ?? "";
            _onReply = onReply;
            _canDelete = canDelete;

            if (BlockWords.Match(post.Content?.Where(segment => segment.Kind == ContentKind.Text)
                    .Select(segment => segment.Text) ?? Enumerable.Empty<string>()))
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            CornerRadius = new CornerRadius(16);
            Padding = new Thickness(20, 16, 20, 12);
            Margin = new Thickness(0, 0, 0, 14);
            SetResourceReference(BackgroundProperty, "CardSurface");
            SetResourceReference(BorderBrushProperty, "Border");
            BorderThickness = new Thickness(1);

            var root = new StackPanel();

            // header
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatar = Ui.Avatar(post.Author?.AvatarUrl, 38, post.Author?.DisplayName);
            avatar.VerticalAlignment = VerticalAlignment.Top;
            avatar.Cursor = Cursors.Hand;
            avatar.MouseLeftButtonUp += (_, _) =>
            {
                if (post.Author != null && post.Author.Id > 0) Svc.Window?.OpenProfile(post.Author.Id, post.Author.DisplayName);
            };
            header.Children.Add(avatar);

            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
            var name = Ui.Text(post.Author?.DisplayName ?? "匿名用户", 13.8, FontWeights.SemiBold);
            name.SetResourceReference(TextBlock.ForegroundProperty, isOp ? "Accent" : "TextPrimary");
            name.Cursor = Cursors.Hand;
            name.MouseLeftButtonUp += (_, _) =>
            {
                if (post.Author != null && post.Author.Id > 0) Svc.Window?.OpenProfile(post.Author.Id, post.Author.DisplayName);
            };
            nameRow.Children.Add(name);
            if (post.Author != null && post.Author.Level > 0)
            {
                var level = Ui.Chip("Lv." + post.Author.Level, "TextSecondary");
                level.Margin = new Thickness(6, 0, 0, 0);
                nameRow.Children.Add(level);
            }
            if (isOp)
            {
                var opChip = Ui.Chip("楼主", "Accent", "AccentSoft");
                opChip.Margin = new Thickness(5, 0, 0, 0);
                nameRow.Children.Add(opChip);
            }
            if (post.Author != null && post.Author.IsBawu)
            {
                var bawu = Ui.Chip("吧务", "Success", "SurfaceAlt");
                bawu.Margin = new Thickness(5, 0, 0, 0);
                nameRow.Children.Add(bawu);
            }

            var meta = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
            meta.Children.Add(Ui.Text(Ui.TimeFull(post.Time), 11.5, null, "TextTertiary"));
            meta.Children.Add(Ui.Text(" · " + post.Floor + "楼", 11.5, null, "TextTertiary"));
            if (!string.IsNullOrWhiteSpace(post.IpLocation))
            {
                meta.Children.Add(Ui.Text(" · " + post.IpLocation, 11.5, null, "TextTertiary"));
            }

            var headStack = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            headStack.Children.Add(nameRow);
            headStack.Children.Add(meta);
            Grid.SetColumn(headStack, 1);
            header.Children.Add(headStack);

            var floorIndex = Ui.Text("#" + post.Floor, 11.5, null, "TextTertiary");
            floorIndex.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(floorIndex, 2);
            header.Children.Add(floorIndex);
            root.Children.Add(header);

            // content
            var contentPanel = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            BuildContent(contentPanel, post.Content);
            var video = post.Video ?? _fallbackVideo;
            if (video != null)
            {
                contentPanel.Children.Add(BuildVideoBlock(video));
            }
            root.Children.Add(contentPanel);

            // actions
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(44, 8, 0, 0) };
            var replyButton = ActionButton("\uE90A", post.SubPostCount > 0 ? post.SubPostCount.ToString() : "回复", out _);
            replyButton.Click += (_, _) => _onReply?.Invoke(post);
            actions.Children.Add(replyButton);

            var agreeButton = ActionButton("\uE8E1", post.AgreeNum > 0 ? post.AgreeNum.ToString() : "赞", out _agreeLabel);
            if (post.HasAgree) _agreeLabel.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
            agreeButton.Click += async (_, _) => await ToggleAgreeAsync();
            actions.Children.Add(agreeButton);

            var share = ActionButton("\uE72D", "分享", out _);
            share.Click += async (_, _) =>
            {
                var url = "https://tieba.baidu.com/p/" + _threadId + "?pid=" + _post.Id;
                if (await ClipboardHelper.SetTextAsync(url))
                {
                    Svc.Window?.ShowToast("链接已复制到剪贴板");
                }
                else
                {
                    System.Windows.MessageBox.Show(url, "分享链接", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };
            actions.Children.Add(share);

            var copy = ActionButton("\uE8C8", "复制", out _);
            copy.Click += (_, _) =>
            {
                var text = ContentToText(_post.Content);
                if (string.IsNullOrWhiteSpace(text))
                {
                    Svc.Window?.ShowToast("这条回复没有可复制的文字");
                    return;
                }
                var dialog = new CopyTextWindow("复制内容", text) { Owner = Svc.Window };
                dialog.Show();
                dialog.Activate();
            };
            actions.Children.Add(copy);
            var report = ActionButton("\uE7BA", "举报", out _);
            report.Click += async (_, _) =>
            {
                try
                {
                    var url = await Svc.Api.GetReportUrlAsync(_post.Id);
                    if (string.IsNullOrWhiteSpace(url) || !Ui.OpenUrl(url))
                    {
                        Svc.Window?.ShowToast("无法打开举报页面");
                    }
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("举报失败：" + ex.Message);
                }
            };
            actions.Children.Add(report);
            if (_canDelete)
            {
                var delete = ActionButton("\uE74D", "删除", out _);
                delete.Click += async (_, _) => await DeletePostAsync();
                actions.Children.Add(delete);
            }
            root.Children.Add(actions);

            // sub posts
            _subPanel = new StackPanel { Margin = new Thickness(48, 6, 0, 0) };
            _subPanel.Children.Add(_subList);
            _subPanel.Children.Add(_subFooter);
            root.Children.Add(_subPanel);
            _subAll.AddRange(post.SubPosts);
            _subShown = Math.Min(3, _subAll.Count);
            _subsExhausted = post.SubPostCount > 0 && _subAll.Count >= post.SubPostCount;
            RenderSubPosts(_subAll, _subShown);
            BuildSubFooter();

            Child = root;
        }

        private static Button ActionButton(string glyph, string text, out TextBlock label)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = Ui.Icon(glyph, 12.5, "TextSecondary");
            label = Ui.Text(text, 12.2, null, "TextSecondary");
            label.Margin = new Thickness(4, 0, 0, 0);
            panel.Children.Add(icon);
            panel.Children.Add(label);
            var button = new Button
            {
                Content = panel,
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 6, 0),
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, "GhostButton");
            return button;
        }

        private UIElement BuildVideoBlock(VideoInfoModel video)
        {
            var width = 520.0;
            var height = 292.0;
            if (video.Width > 0 && video.Height > 0)
            {
                height = width * video.Height / video.Width;
                if (height > 420) height = 420;
                if (height < 180) height = 180;
            }
            var host = new Grid
            {
                Width = width,
                Height = height,
                Margin = new Thickness(0, 4, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Left,
                ClipToBounds = true,
            };
            if (!string.IsNullOrWhiteSpace(video.Thumbnail))
            {
                host.Children.Add(new RemoteImage
                {
                    Url = video.Thumbnail,
                    DecodeWidth = 1080,
                    CornerRadius = new CornerRadius(12),
                });
            }
            else
            {
                var placeholder = new Border { CornerRadius = new CornerRadius(12) };
                placeholder.SetResourceReference(BackgroundProperty, "SurfaceAlt");
                host.Children.Add(placeholder);
            }

            var play = new Border
            {
                Width = 62,
                Height = 62,
                CornerRadius = new CornerRadius(31),
                Background = new SolidColorBrush(Color.FromArgb(216, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "\uE768",
                    FontFamily = (FontFamily)Application.Current.FindResource("IconFont"),
                    FontSize = 24,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x13, 0x18)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 0, 0),
                },
            };
            host.Children.Add(play);

            if (video.Duration > 0)
            {
                var duration = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
                    Padding = new Thickness(7, 2, 7, 2),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 10, 10),
                    Child = new TextBlock
                    {
                        Text = TimeSpan.FromSeconds(video.Duration).ToString(video.Duration >= 3600 ? @"h\:mm\:ss" : @"m\:ss"),
                        FontSize = 11.5,
                        Foreground = Brushes.White,
                    },
                };
                host.Children.Add(duration);
            }

            host.Cursor = Cursors.Hand;
            host.MouseLeftButtonUp += (_, _) =>
            {
                var window = new VideoWindow(video, _threadId) { Owner = Svc.Window };
                window.ShowDialog();
            };
            return host;
        }

        private async System.Threading.Tasks.Task DeletePostAsync()
        {
            if (MessageBox.Show("确定删除这条回复？", "删除回复", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            {
                return;
            }
            try
            {
                var mine = Svc.Account.IsLoggedIn && _post.AuthorId == Svc.Account.Uid;
                await Svc.Api.DeletePostAsync(_forumId, _forumName, _threadId, _post.Id, false, mine);
                if (Parent is Panel panel) panel.Children.Remove(this);
                Svc.Window?.ShowToast("已删除");
            }
            catch (Exception ex)
            {
                Svc.Window?.ShowToast(ex.Message);
            }
        }

        private async System.Threading.Tasks.Task ToggleAgreeAsync()
        {
            if (!Svc.Account.IsLoggedIn)
            {
                Svc.Window?.ShowToast("登录后才能点赞");
                return;
            }
            var target = !_post.HasAgree;
            try
            {
                await Svc.Api.AgreeAsync(_threadId, _post.Id, false, !target, _forumId);
                _post.HasAgree = target;
                _post.AgreeNum += target ? 1 : -1;
                if (_post.AgreeNum < 0) _post.AgreeNum = 0;
                _agreeLabel.Text = _post.AgreeNum > 0 ? _post.AgreeNum.ToString() : "赞";
                _agreeLabel.SetResourceReference(TextBlock.ForegroundProperty, target ? "Accent" : "TextSecondary");
            }
            catch (Exception ex)
            {
                var code = ex is TiebaException tieba ? "(" + tieba.Code + ") " : "";
                Svc.Window?.ShowToast("点赞失败" + code + "：" + ex.Message);
            }
        }

        private async System.Threading.Tasks.Task LoadNextSubPageAsync()
        {
            if (_subsExhausted) return;
            try
            {
                var (_, subs, _) = await Svc.Api.GetFloorAsync(_threadId, _post.Id, _subPage, _forumId);
                _subPage++;
                _subsLoaded = true;
                if (subs.Count == 0)
                {
                    _subsExhausted = true;
                    return;
                }
                foreach (var sub in subs)
                {
                    if (_subAll.All(existing => existing.Id != sub.Id))
                    {
                        _subAll.Add(sub);
                    }
                }
                if (_post.SubPostCount > 0 && _subAll.Count >= _post.SubPostCount)
                {
                    _subsExhausted = true;
                }
            }
            catch (Exception ex)
            {
                _subsExhausted = true;
                Svc.Window?.ShowToast("加载回复失败：" + ex.Message);
            }
        }

        private async System.Threading.Tasks.Task LoadMoreSubPostsAsync()
        {
            if (_subShown >= _subAll.Count && !_subsExhausted)
            {
                await LoadNextSubPageAsync();
            }
            if (_subShown < _subAll.Count)
            {
                _subShown = Math.Min(_subAll.Count, _subShown < 10 ? 10 : _subShown + 10);
            }
            if (_subShown == 0 && _subAll.Count == 0)
            {
                Svc.Window?.ShowToast("没有更多回复");
                return;
            }
            RenderSubPosts(_subAll, _subShown);
            BuildSubFooter();
        }

        private void CollapseSubPosts()
        {
            _subShown = Math.Min(3, _subAll.Count);
            RenderSubPosts(_subAll, _subShown);
            BuildSubFooter();
        }

        private void BuildSubFooter()
        {
            _subFooter.Children.Clear();
            if (_post.SubPostCount > 0 && _subAll.Count >= _post.SubPostCount) _subsExhausted = true;
            var remaining = _subAll.Count - _subShown;
            var hasMore = remaining > 0 || (!_subsExhausted && _subAll.Count > 0);
            if (hasMore)
            {
                var totalKnown = _post.SubPostCount > 0 ? _post.SubPostCount : _subAll.Count;
                var remainingTotal = Math.Max(0, totalKnown - _subShown);
                var moreText = !_subsLoaded || _subShown <= 3
                    ? "查看全部 " + totalKnown + " 条回复"
                    : remainingTotal > 0 && remainingTotal < 10
                        ? "继续查看 " + remainingTotal + " 条"
                        : "继续查看 10 条";
                var more = Ui.Button(moreText, false, 12);
                more.Padding = new Thickness(12, 5, 12, 5);
                more.Margin = new Thickness(0, 0, 6, 0);
                more.Click += async (_, _) => await LoadMoreSubPostsAsync();
                _subFooter.Children.Add(more);
            }
            if (_subShown > 3)
            {
                var collapse = Ui.Button("收起", false, 12);
                collapse.Padding = new Thickness(12, 5, 12, 5);
                collapse.Click += (_, _) => CollapseSubPosts();
                _subFooter.Children.Add(collapse);
            }
        }

        private void RenderSubPosts(List<SubPost> subs, int count)
        {
            _subList.Children.Clear();
            if (count <= 0) return;
            var shown = subs.Take(count).ToList();
            foreach (var sub in shown)
            {
                var row = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(10, 7, 10, 7),
                    Margin = new Thickness(0, 2, 0, 2),
                };
                row.SetResourceReference(BackgroundProperty, "SurfaceAlt");
                var stack = new StackPanel();
                var line = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12.8, LineHeight = 20 };
                line.SetResourceReference(TextBlock.FontFamilyProperty, "UiFont");
                line.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
                line.Inlines.Add(new Run(sub.Author?.DisplayName ?? "用户")
                {
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Ui.Brush("TextSecondary"),
                });
                if (sub.Author != null && sub.Author.Level > 0)
                {
                    line.Inlines.Add(new Run(" Lv." + sub.Author.Level)
                    {
                        FontSize = 10.5,
                        Foreground = Ui.Brush("Accent"),
                    });
                }
                if (!string.IsNullOrWhiteSpace(sub.Author?.IpAddress))
                {
                    line.Inlines.Add(new Run(" · IP属地:" + sub.Author.IpAddress)
                    {
                        FontSize = 10.5,
                        Foreground = Ui.Brush("TextTertiary"),
                    });
                }
                line.Inlines.Add(new Run("：") { Foreground = Ui.Brush("TextTertiary") });
                var hasImageMarker = false;
                foreach (var segment in sub.Content.Where(s => s.Kind != ContentKind.Image))
                {
                    if (segment.Kind == ContentKind.Text && segment.Text.Contains("[图片]"))
                    {
                        hasImageMarker = true;
                        var cleaned = segment.Text.Replace("[图片]", "").Trim();
                        if (cleaned.Length > 0)
                        {
                            line.Inlines.Add(new Run(cleaned));
                        }
                    }
                    else
                    {
                        AppendSegment(line, segment);
                    }
                }
                stack.Children.Add(line);

                var subImages = sub.Content.Where(s => s.Kind == ContentKind.Image).ToList();
                if (subImages.Count > 0)
                {
                    stack.Children.Add(BuildImagePanel(subImages, subImages, 0));
                }
                else if (hasImageMarker)
                {
                    var viewImage = Ui.Text("查看图片", 11.5, FontWeights.Medium, "Accent");
                    viewImage.Cursor = Cursors.Hand;
                    viewImage.Margin = new Thickness(0, 2, 0, 0);
                    viewImage.MouseLeftButtonUp += async (_, _) => await LoadSubPostImagesAsync(sub);
                    stack.Children.Add(viewImage);
                }

                var subActions = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 2, 0, 0),
                };
                var subCopy = Ui.Text("复制", 11.5, FontWeights.Medium, "TextTertiary");
                subCopy.Cursor = Cursors.Hand;
                subCopy.Margin = new Thickness(0, 0, 12, 0);
                subCopy.MouseLeftButtonUp += (_, _) =>
                {
                    var text = ContentToText(sub.Content);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        Svc.Window?.ShowToast("这条楼中楼没有可复制的文字");
                        return;
                    }
                    var dialog = new CopyTextWindow("复制楼中楼", text) { Owner = Svc.Window };
                    dialog.Show();
                    dialog.Activate();
                };
                subActions.Children.Add(subCopy);
                var reply = Ui.Text("回复", 11.5, FontWeights.Medium, "Accent");
                reply.Cursor = Cursors.Hand;
                reply.MouseLeftButtonUp += (_, _) =>
                {
                    _onReply?.Invoke(new PostItem
                    {
                        Id = sub.Id,
                        Author = sub.Author,
                        AuthorId = sub.AuthorId,
                        Floor = _post.Floor,
                    });
                };
                subActions.Children.Add(reply);
                stack.Children.Add(subActions);
                row.Child = stack;
                _subList.Children.Add(row);
            }
        }

        private static string ContentToText(List<ContentSegment> segments)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var segment in segments)
            {
                switch (segment.Kind)
                {
                    case ContentKind.Text:
                        sb.Append(segment.Text);
                        break;
                    case ContentKind.Emoticon:
                        sb.Append("#(").Append(segment.Text).Append(")");
                        break;
                    case ContentKind.Link:
                        sb.Append(segment.Text);
                        break;
                    case ContentKind.Image:
                    case ContentKind.Video:
                        break;
                }
            }
            return sb.ToString().Trim();
        }

        private async System.Threading.Tasks.Task LoadSubPostImagesAsync(SubPost sub)
        {
            List<string> Extract(IEnumerable<SubPost> list)
            {
                var target = list.FirstOrDefault(item => item.Id == sub.Id);
                return target?.Content
                    .Where(segment => segment.Kind == ContentKind.Image)
                    .Select(segment => string.IsNullOrWhiteSpace(segment.Url) ? segment.ThumbUrl : segment.Url)
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .ToList() ?? new List<string>();
            }

            var immediate = Extract(new[] { sub });
            if (immediate.Count > 0)
            {
                Svc.Window?.OpenImageViewer(immediate, 0);
                return;
            }
            for (var attempt = 0; attempt < 3; attempt++)
            {
                for (var page = 1; page <= 3; page++)
                {
                    try
                    {
                        var (_, subs, _) = await Svc.Api.GetFloorAsync(_threadId, _post.Id, page, _forumId);
                        var urls = Extract(subs);
                        if (urls.Count > 0)
                        {
                            Svc.Window?.OpenImageViewer(urls, 0);
                            return;
                        }
                    }
                    catch
                    {
                    }
                }
                await System.Threading.Tasks.Task.Delay(180);
            }
            Svc.Window?.ShowToast("图片加载失败");
        }

        private void BuildContent(StackPanel panel, List<ContentSegment> segments)
        {
            TextBlock current = null;
            var images = new List<ContentSegment>();
            var allImages = segments.Where(s => s.Kind == ContentKind.Image).ToList();

            void FlushText()
            {
                if (current != null && current.Inlines.Count > 0)
                {
                    current.Margin = new Thickness(0, 0, 0, 8);
                    panel.Children.Add(current);
                }
                current = null;
            }

            void FlushImages()
            {
                if (images.Count == 0) return;
                var batch = new List<ContentSegment>(images);
                var offset = allImages.Count > 0 ? allImages.IndexOf(batch[0]) : 0;
                panel.Children.Add(BuildImagePanel(batch, allImages, Math.Max(0, offset)));
                images.Clear();
            }

            foreach (var segment in segments)
            {
                if (segment.Kind == ContentKind.Video)
                {
                    if (_fallbackVideo == null && !string.IsNullOrWhiteSpace(segment.Url))
                    {
                        _fallbackVideo = new VideoInfoModel
                        {
                            Url = segment.Url,
                            PageUrl = segment.Text != null && segment.Text.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                                ? segment.Text
                                : "",
                            Thumbnail = segment.ThumbUrl,
                            Title = segment.Text,
                        };
                    }
                    continue;
                }
                if (segment.Kind == ContentKind.Image)
                {
                    FlushText();
                    images.Add(segment);
                    continue;
                }
                FlushImages();
                current ??= NewTextBlock();
                AppendSegment(current, segment);
            }
            FlushText();
            FlushImages();
        }

        private static TextBlock NewTextBlock()
        {
            var block = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 15,
                LineHeight = 25,
            };
            block.SetResourceReference(TextBlock.FontFamilyProperty, "UiFont");
            block.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
            return block;
        }

        private static void AppendSegment(TextBlock block, ContentSegment segment)
        {
            switch (segment.Kind)
            {
                case ContentKind.Text:
                    block.Inlines.Add(new Run(segment.Text));
                    break;
                case ContentKind.At:
                {
                    var link = new Hyperlink(new Run(segment.Text)) { Cursor = Cursors.Hand };
                    link.SetResourceReference(TextElement.ForegroundProperty, "Accent");
                    var uid = segment.Uid;
                    var name = segment.Text.TrimStart('@');
                    link.Click += (_, _) => Svc.Window?.OpenProfile(uid, name);
                    block.Inlines.Add(link);
                    break;
                }
                case ContentKind.Link:
                {
                    var link = new Hyperlink(new Run(segment.Text)) { Cursor = Cursors.Hand };
                    link.SetResourceReference(TextElement.ForegroundProperty, "Accent");
                    var url = segment.Url;
                    link.Click += (_, _) =>
                    {
                        if (!Ui.OpenUrl(url)) Svc.Window?.ShowToast("无法打开浏览器，链接已复制到剪贴板");
                    };
                    block.Inlines.Add(link);
                    break;
                }
                case ContentKind.Emoticon:
                {
                    BitmapImage image = null;
                    try
                    {
                        var path = ContentParser.EmoticonPath(segment.EmoticonId);
                        if (System.IO.File.Exists(path)) image = ImageLoader.FromFile(path);
                    }
                    catch
                    {
                    }
                    if (image != null)
                    {
                        var control = new Image
                        {
                            Source = image,
                            Width = 21,
                            Height = 21,
                            Stretch = Stretch.Uniform,
                            Margin = new Thickness(0, 0, 1, 0),
                        };
                        block.Inlines.Add(new InlineUIContainer(control) { BaselineAlignment = BaselineAlignment.Center });
                    }
                    else
                    {
                        block.Inlines.Add(new Run("#(" + segment.Text + ")"));
                    }
                    break;
                }
                case ContentKind.Video:
                    block.Inlines.Add(new Run("🎬 视频") { Foreground = Ui.Brush("Accent") });
                    break;
                case ContentKind.Voice:
                    block.Inlines.Add(new Run("🎤 语音") { Foreground = Ui.Brush("Accent") });
                    break;
            }
        }

        private UIElement BuildImagePanel(List<ContentSegment> images, List<ContentSegment> viewerImages, int offset)
        {
            if (images.Count == 1)
            {
                var segment = images[0];
                var width = 520.0;
                var height = 320.0;
                if (segment.Width > 0 && segment.Height > 0)
                {
                    height = width * segment.Height / segment.Width;
                    if (height > 360) height = 360;
                    if (height < 120) height = 120;
                }
                var image = new RemoteImage
                {
                    Width = width,
                    Height = height,
                    Url = segment.ThumbUrl,
                    DecodeWidth = 1080,
                    CornerRadius = new CornerRadius(12),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Cursor = Cursors.Hand,
                };
                var isLong = segment.Width > 0 && segment.Height > 0 && segment.Height > segment.Width * 1.8;
                var host = new Grid
                {
                    Width = width,
                    Height = height,
                    Margin = new Thickness(0, 2, 0, 10),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Cursor = Cursors.Hand,
                    ToolTip = "点击查看大图",
                    Background = Brushes.Transparent,
                };
                host.Children.Add(image);
                if (isLong)
                {
                    var badge = new Border
                    {
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(7, 3, 7, 3),
                        Margin = new Thickness(0, 0, 8, 8),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Background = new SolidColorBrush(Color.FromArgb(0xB8, 0x00, 0x00, 0x00)),
                        IsHitTestVisible = false,
                        Child = new TextBlock
                        {
                            Text = "长图 · 点击查看全图",
                            FontSize = 10.5,
                            Foreground = Brushes.White,
                        },
                    };
                    host.Children.Add(badge);
                }
                host.AddHandler(UIElement.MouseLeftButtonDownEvent,
                    new MouseButtonEventHandler((_, e) =>
                    {
                        e.Handled = true;
                        OpenViewer(viewerImages, offset);
                    }), true);
                return host;
            }

            var wrap = new WrapPanel { Margin = new Thickness(0, 2, 0, 10) };
            for (var i = 0; i < images.Count; i++)
            {
                var segment = images[i];
                var thumb = new RemoteImage
                {
                    Width = 168,
                    Height = 168,
                    Url = segment.ThumbUrl,
                    DecodeWidth = 420,
                    CornerRadius = new CornerRadius(10),
                    Margin = new Thickness(0, 0, 8, 8),
                    Cursor = Cursors.Hand,
                    StretchMode = Stretch.UniformToFill,
                };
                var index = i;
                var host = new Border
                {
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    ToolTip = "点击查看大图",
                    Child = thumb,
                };
                host.AddHandler(UIElement.MouseLeftButtonDownEvent,
                    new MouseButtonEventHandler((_, e) =>
                    {
                        e.Handled = true;
                        OpenViewer(viewerImages, offset + index);
                    }), true);
                wrap.Children.Add(host);
            }
            return wrap;
        }

        private static void OpenViewer(List<ContentSegment> images, int index)
        {
            var pairs = images.Select(i => (main: string.IsNullOrEmpty(i.Url) ? i.ThumbUrl : i.Url, thumb: i.ThumbUrl))
                .Where(p => !string.IsNullOrEmpty(p.main)).ToList();
            if (pairs.Count == 0) return;
            Svc.Window?.OpenImageViewer(
                pairs.Select(p => p.main).ToList(),
                Math.Min(index, pairs.Count - 1),
                pairs.Select(p => p.thumb).ToList());
        }
    }
}
