using System;
using System.Collections.Generic;
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
    /// <summary>消息页：回复 / @ / 赞。</summary>
    public sealed class MessagesView : UserControl, IRefreshable
    {
        private readonly StackPanel _root;
        private readonly StackPanel _list;
        private readonly StackPanel _tabs;
        private readonly TextBlock _status;
        private readonly Border _listHost;
        private string _mode = "reply";
        private bool _loading;

        public MessagesView()
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            _root = new StackPanel
            {
                MaxWidth = 900,
                MinWidth = 720,
                Margin = new Thickness(28, 10, 28, 40),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            _status = Ui.Text("", 12, null, "TextTertiary");
            _status.Margin = new Thickness(2, 0, 0, 8);
            _list = new StackPanel();
            _listHost = new Border { Child = _list };
            _root.Children.Add(_tabs);
            _root.Children.Add(_status);
            _root.Children.Add(_listHost);
            scroll.Content = _root;
            Content = scroll;
            BuildTabs();
            Loaded += async (_, _) => await LoadAsync();
        }

        private void BuildTabs()
        {
            _tabs.Children.Clear();
            var options = new (string code, string label)[] { ("reply", "回复我的"), ("at", "@我的"), ("agree", "赞我的") };
            foreach (var option in options)
            {
                var radio = new RadioButton
                {
                    Content = option.label,
                    GroupName = "MsgTabs",
                    IsChecked = _mode == option.code,
                };
                radio.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
                var code = option.code;
                radio.Checked += async (_, _) =>
                {
                    if (_mode == code) return;
                    _mode = code;
                    await LoadAsync();
                };
                _tabs.Children.Add(radio);
            }
        }

        private async Task LoadAsync()
        {
            if (_loading) return;
            _loading = true;

            await FadeOutAsync(_listHost, 110);
            _list.Children.Clear();

            if (!Svc.Account.IsLoggedIn)
            {
                _status.Text = "";
                _list.Children.Add(LoginPrompt());
                _listHost.Opacity = 0;
                FadeIn(_listHost, 220);
                _loading = false;
                return;
            }

            _status.Text = "加载中…";
            _list.Children.Add(HomeView.Skeleton(3));
            _listHost.Opacity = 0;
            FadeIn(_listHost, 180);

            try
            {
                var messages = await Svc.Api.GetMessagesAsync(_mode);
                await FadeOutAsync(_listHost, 110);
                _list.Children.Clear();
                _status.Text = messages.Count == 0 ? "暂无消息" : "共 " + messages.Count + " 条";
                if (messages.Count == 0)
                {
                    _list.Children.Add(HomeView.EmptyCard("暂时没有新的消息"));
                }
                var index = 0;
                foreach (var message in messages)
                {
                    var card = Ui.Card(BuildRow(message), 16, 14);
                    _list.Children.Add(card);
                    Anim.FadeInUp(card, Math.Min(index, 8) * 26, 12, 280);
                    index++;
                }
                _listHost.Opacity = 0;
                FadeIn(_listHost, 220);
            }
            catch (Exception ex)
            {
                _list.Children.Clear();
                _list.Children.Add(HomeView.ErrorCard(ex.Message, LoadAsync));
                _status.Text = "";
                _listHost.Opacity = 0;
                FadeIn(_listHost, 200);
            }
            finally
            {
                _loading = false;
            }
        }

        private static async Task FadeOutAsync(UIElement element, int durationMs)
        {
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(durationMs))
            {
                EasingFunction = Anim.EaseInOut,
            };
            element.BeginAnimation(OpacityProperty, fade);
            await Task.Delay(durationMs);
        }

        private static void FadeIn(UIElement element, int durationMs)
        {
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs))
            {
                EasingFunction = Anim.EaseOut,
            };
            element.BeginAnimation(OpacityProperty, fade);
        }

        private UIElement BuildRow(MessageItem message)
        {
            var grid = new Grid { Cursor = Cursors.Hand };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var avatarUrl = string.IsNullOrEmpty(message.Portrait)
                ? ""
                : message.Portrait.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? message.Portrait
                    : "https://himg.bdimg.com/sys/portrait/item/" + message.Portrait;
            var avatar = Ui.Avatar(avatarUrl, 38, message.UserName);
            avatar.VerticalAlignment = VerticalAlignment.Top;
            grid.Children.Add(avatar);

            var body = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(Ui.Text(message.UserName, 13.5, FontWeights.SemiBold));
            var action = Ui.Text(message.IsAgree ? "  赞了你" : message.IsAt ? "  提到了你" : "  回复了你", 11.5, null, "TextTertiary");
            action.Margin = new Thickness(1, 0, 8, 0);
            head.Children.Add(action);
            if (!string.IsNullOrWhiteSpace(message.ForumName))
            {
                var chip = Ui.Chip(message.ForumName, "Accent", "AccentSoft");
                chip.Margin = new Thickness(6, 0, 0, 0);
                head.Children.Add(chip);
            }
            body.Children.Add(head);

            if (!string.IsNullOrWhiteSpace(message.Content))
            {
                var content = Ui.Text(message.Content, 13, null, "TextPrimary");
                content.Margin = new Thickness(0, 6, 0, 0);
                content.MaxHeight = 42;
                body.Children.Add(content);
            }
            var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            if (!string.IsNullOrWhiteSpace(message.Title))
            {
                footer.Children.Add(Ui.Text("原帖：" + message.Title, 11.5, null, "TextTertiary"));
            }
            if (message.Time > 0)
            {
                footer.Children.Add(Ui.Text("  ·  " + Ui.TimeAgo(message.Time), 11.5, null, "TextTertiary"));
            }
            body.Children.Add(footer);
            Grid.SetColumn(body, 1);
            grid.Children.Add(body);

            grid.MouseLeftButtonUp += (_, _) =>
            {
                if (message.ThreadId > 0) Svc.Window?.OpenThread(message.ThreadId, message.Title, message.ForumName);
            };
            return grid;
        }

        public static UIElement LoginPrompt(string title = "登录后查看你的消息")
        {
            var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(20) };
            var icon = Ui.Icon("\uE77B", 34);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.Opacity = 0.4;
            panel.Children.Add(icon);
            var text = Ui.Text(title, 14, FontWeights.SemiBold);
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.Margin = new Thickness(0, 12, 0, 4);
            panel.Children.Add(text);
            var hint = Ui.Text("支持手机号、扫码与 Cookie 登录", 12, null, "TextTertiary");
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            panel.Children.Add(hint);
            var button = Ui.Button("立即登录", true, 12.5);
            button.HorizontalAlignment = HorizontalAlignment.Center;
            button.Margin = new Thickness(0, 14, 0, 0);
            button.Click += (_, _) =>
            {
                var login = new LoginWindow { Owner = Svc.Window };
                if (login.ShowDialog() == true)
                {
                    Svc.Window?.UpdateUserCard();
                    Svc.Nav.Back();
                    Svc.Window?.ShowToast("登录成功");
                }
            };
            panel.Children.Add(button);
            return Ui.Card(panel, 28, 16);
        }

        public async Task RefreshAsync() => await LoadAsync();
    }
}
