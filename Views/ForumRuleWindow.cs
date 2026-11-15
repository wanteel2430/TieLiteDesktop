using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FC = TieLiteDesktop.Controls;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Views
{
    /// <summary>吧规详情窗口，以帖子形式展示。</summary>
    public sealed class ForumRuleWindow : Window
    {
        public ForumRuleWindow(string forumName, ForumRuleDetail detail)
        {
            Width = 760;
            Height = 700;
            MinWidth = 480;
            MinHeight = 360;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Title = forumName + " 吧规";
            SetResourceReference(BackgroundProperty, "AppBackground");
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 0,
                GlassFrameThickness = new Thickness(0),
                ResizeBorderThickness = new Thickness(6),
                CornerRadius = new CornerRadius(12),
            });
            Anim.RoundWindowCorners(this);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = new Grid { Margin = new Thickness(20, 16, 16, 8) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleStack = new StackPanel();
            titleStack.Children.Add(FC.Ui.Text(forumName + " 吧规", 17, FontWeights.SemiBold));
            if (!string.IsNullOrWhiteSpace(detail?.PublishTime))
            {
                titleStack.Children.Add(FC.Ui.Text("发布于 " + detail.PublishTime, 11.5, null, "TextTertiary"));
            }
            header.Children.Add(titleStack);
            var close = FC.Ui.IconButton("\uE8BB", "关闭", 34);
            close.Click += (_, _) => Anim.AnimateClose(this);
            Anim.HoverScale(close);
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            root.Children.Add(header);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(20, 0, 20, 16),
            };
            var body = new StackPanel();
            if (!string.IsNullOrWhiteSpace(detail?.Title))
            {
                body.Children.Add(FC.Ui.Text(detail.Title, 20, FontWeights.Bold));
            }
            if (!string.IsNullOrWhiteSpace(detail?.Preface))
            {
                var preface = FC.Ui.Text(detail.Preface, 13, null, "TextSecondary");
                preface.Margin = new Thickness(0, 8, 0, 10);
                preface.TextWrapping = TextWrapping.Wrap;
                body.Children.Add(preface);
            }
            if (detail?.Rules != null)
            {
                foreach (var rule in detail.Rules)
                {
                    var card = new Border
                    {
                        CornerRadius = new CornerRadius(10),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(14, 10, 14, 10),
                        Margin = new Thickness(0, 0, 0, 10),
                    };
                    card.SetResourceReference(Border.BackgroundProperty, "Surface");
                    card.SetResourceReference(Border.BorderBrushProperty, "Border");
                    var stack = new StackPanel();
                    if (!string.IsNullOrWhiteSpace(rule.Title))
                    {
                        stack.Children.Add(FC.Ui.Text(rule.Title, 14.5, FontWeights.SemiBold));
                    }
                    if (!string.IsNullOrWhiteSpace(rule.Text))
                    {
                        var text = FC.Ui.Text(rule.Text, 13, null, "TextSecondary");
                        text.Margin = new Thickness(0, 6, 0, 0);
                        text.TextWrapping = TextWrapping.Wrap;
                        stack.Children.Add(text);
                    }
                    card.Child = stack;
                    body.Children.Add(card);
                }
            }
            if (detail == null || (string.IsNullOrWhiteSpace(detail.Title) && detail.Rules.Count == 0))
            {
                body.Children.Add(FC.Ui.Text("该吧暂未设置吧规", 13, null, "TextTertiary"));
            }
            scroll.Content = body;
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);
            Content = root;

            Loaded += (_, _) => Anim.PopIn(this);
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Anim.AnimateClose(this);
            };
            Deactivated += (_, _) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!IsActive) Anim.AnimateClose(this);
                }), System.Windows.Threading.DispatcherPriority.Background);
            };
        }
    }
}
