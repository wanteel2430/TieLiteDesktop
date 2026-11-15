using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Controls
{
    /// <summary>One thread row inside a forum / search / home list.</summary>
    public sealed class ThreadRow : Border
    {
        private readonly ThreadSummary _thread;

        public ThreadRow(ThreadSummary thread, bool showForum = true, string fallbackForum = null, int rank = 0)
        {
            _thread = thread;
            CornerRadius = new CornerRadius(14);
            Padding = new Thickness(16, 14, 16, 14);
            Margin = new Thickness(0, 0, 0, 3);
            Background = Brushes.Transparent;
            Cursor = Cursors.Hand;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            if (thread.IsTop) titleRow.Children.Add(WithMargin(Ui.Chip("置顶", "Accent", "AccentSoft"), 0, 0, 6, 0));
            if (thread.IsGood) titleRow.Children.Add(WithMargin(Ui.Chip("精品", "Success", "SurfaceAlt"), 0, 0, 6, 0));
            if (thread.IsPoll) titleRow.Children.Add(WithMargin(Ui.Chip("投票", "Accent", "AccentSoft"), 0, 0, 6, 0));

            var title = Ui.Text(string.IsNullOrWhiteSpace(thread.Title) ? "（无标题）" : thread.Title, 15.5, FontWeights.SemiBold);
            title.TextWrapping = TextWrapping.Wrap;
            title.MaxHeight = 46;
            title.ClipToBounds = true;
            titleRow.Children.Add(title);
            left.Children.Add(titleRow);

            if (!string.IsNullOrWhiteSpace(thread.PreviewText))
            {
                var preview = Ui.Text(thread.PreviewText, 12.8, null, "TextSecondary");
                preview.Margin = new Thickness(0, 7, 0, 0);
                preview.TextWrapping = TextWrapping.NoWrap;
                preview.TextTrimming = TextTrimming.CharacterEllipsis;
                left.Children.Add(preview);
            }

            var meta = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 9, 0, 0) };
            var forumName = !string.IsNullOrWhiteSpace(thread.ForumName) ? thread.ForumName : fallbackForum;
            if (showForum && !string.IsNullOrWhiteSpace(forumName))
            {
                var forumChip = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(7, 2, 7, 2),
                    Margin = new Thickness(0, 0, 8, 0),
                    Child = Ui.Text(forumName, 11.5, FontWeights.Medium, "Accent"),
                };
                forumChip.SetResourceReference(BackgroundProperty, "AccentSoft");
                meta.Children.Add(forumChip);
            }
            if (thread.Author != null && !string.IsNullOrEmpty(thread.Author.DisplayName))
            {
                meta.Children.Add(MetaText(thread.Author.DisplayName));
            }
            if (thread.ReplyNum > 0)
            {
                meta.Children.Add(MetaText("回复 " + Ui.ShortNum(thread.ReplyNum)));
            }
            if (thread.AgreeNum > 0)
            {
                meta.Children.Add(MetaText("赞 " + Ui.ShortNum(thread.AgreeNum)));
            }
            var time = !string.IsNullOrWhiteSpace(thread.LastTime) ? thread.LastTime : Ui.TimeAgo(thread.LastTimeInt);
            if (!string.IsNullOrWhiteSpace(time)) meta.Children.Add(MetaText(time, true));
            left.Children.Add(meta);
            grid.Children.Add(left);

            var thumbnail = BuildThumbnail(thread);
            if (thumbnail != null)
            {
                Grid.SetColumn(thumbnail, 1);
                grid.Children.Add(thumbnail);
            }

            var root = new Grid();
            root.Children.Add(grid);
            if (rank > 0)
            {
                var top = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6, 1, 6, 1),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    IsHitTestVisible = false,
                    Child = Ui.Text("TOP" + rank, 10, FontWeights.Bold, rank <= 3 ? "Accent" : "TextSecondary"),
                };
                top.SetResourceReference(BackgroundProperty, rank <= 3 ? "AccentSoft" : "SurfaceAlt");
                Panel.SetZIndex(top, 5);
                root.Children.Add(top);
            }
            Child = root;
            MouseLeftButtonUp += (_, _) =>
            {
                var target = string.IsNullOrWhiteSpace(thread.ForumName) ? fallbackForum : thread.ForumName;
                Svc.Window?.OpenThread(thread.Id, thread.Title, target, thread.Poll);
            };
        }

        /// <summary>Cover for the row; video threads always get a thumbnail with a play badge.</summary>
        private static UIElement BuildThumbnail(ThreadSummary thread)
        {
            var hasCover = !string.IsNullOrWhiteSpace(thread.PreviewImage);
            if (!hasCover && !thread.HasVideo) return null;

            var host = new Grid
            {
                Width = 104,
                Height = 76,
                Margin = new Thickness(14, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (hasCover)
            {
                host.Children.Add(new RemoteImage
                {
                    Url = thread.PreviewImage,
                    DecodeWidth = 208,
                    CornerRadius = new CornerRadius(10),
                });
                var frame = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x24, 0x00, 0x00, 0x00)),
                    IsHitTestVisible = false,
                };
                host.Children.Add(frame);
            }
            else
            {
                var placeholder = new Border { CornerRadius = new CornerRadius(10) };
                placeholder.SetResourceReference(Border.BackgroundProperty, "SurfaceAlt");
                placeholder.SetResourceReference(Border.BorderBrushProperty, "Border");
                placeholder.BorderThickness = new Thickness(1);
                var stack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var icon = new TextBlock
                {
                    Text = "\uE768",
                    FontFamily = Application.Current.TryFindResource("IconFont") as FontFamily ?? new FontFamily("Segoe UI Symbol"),
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                icon.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiary");
                stack.Children.Add(icon);
                var caption = new TextBlock
                {
                    Text = "视频",
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0),
                };
                caption.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiary");
                stack.Children.Add(caption);
                placeholder.Child = stack;
                host.Children.Add(placeholder);
            }

            if (hasCover)
            {
                host.Cursor = Cursors.Hand;
                host.AddHandler(UIElement.MouseLeftButtonDownEvent,
                    new MouseButtonEventHandler((_, e) =>
                    {
                        e.Handled = true;
                        Svc.Window?.OpenImageViewer(new System.Collections.Generic.List<string> { thread.PreviewImage }, 0);
                    }), true);
            }

            if (thread.HasVideo && hasCover)
            {
                var badge = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(12),
                    Background = new SolidColorBrush(Color.FromArgb(190, 0, 0, 0)),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 6, 6),
                    Child = new TextBlock
                    {
                        Text = "\uE768",
                        FontFamily = Application.Current.TryFindResource("IconFont") as FontFamily ?? new FontFamily("Segoe UI Symbol"),
                        FontSize = 10,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(1, 0, 0, 0),
                    },
                };
                host.Children.Add(badge);
            }
            return host;
        }

        private static void AnimateHover(UIElement element, double target, int durationMs)
        {
            var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(durationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            element.BeginAnimation(OpacityProperty, animation);
        }

        private static TextBlock MetaText(string text, bool subtle = false)
        {
            var block = Ui.Text(text, 11.8, null, subtle ? "TextTertiary" : "TextSecondary");
            block.Margin = new Thickness(0, 0, 10, 0);
            return block;
        }

        private static FrameworkElement WithMargin(UIElement element, double left, double top, double right, double bottom)
        {
            if (element is FrameworkElement fe) fe.Margin = new Thickness(left, top, right, bottom);
            return element as FrameworkElement ?? new Border();
        }

    }
}
