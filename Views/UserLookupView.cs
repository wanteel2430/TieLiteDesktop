using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Views
{
    /// <summary>按用户 ID / 昵称 / 贴吧号查找用户。</summary>
    public sealed class UserLookupView : UserControl
    {
        private readonly TextBox _input;
        private readonly StackPanel _result;
        private readonly TextBlock _status;

        public UserLookupView()
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            var root = new StackPanel
            {
                MaxWidth = 860,
                MinWidth = 680,
                Margin = new Thickness(28, 10, 28, 40),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var card = new Border
            {
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18),
            };
            card.SetResourceReference(Border.BackgroundProperty, "Surface");
            card.SetResourceReference(Border.BorderBrushProperty, "Border");
            var stack = new StackPanel();
            stack.Children.Add(Ui.Text("用户 ID", 12.5, FontWeights.SemiBold, "TextSecondary"));

            var row = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var inputHost = new Border
            {
                Height = 42,
                CornerRadius = new CornerRadius(9),
                BorderThickness = new Thickness(1),
            };
            inputHost.SetResourceReference(Border.BackgroundProperty, "SurfaceAlt");
            inputHost.SetResourceReference(Border.BorderBrushProperty, "Border");
            _input = new TextBox
            {
                BorderThickness = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                FontSize = 13.5,
                Margin = new Thickness(10, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            _input.SetResourceReference(TextBox.ForegroundProperty, "TextPrimary");
            _input.SetResourceReference(TextBox.CaretBrushProperty, "TextPrimary");
            _input.SetResourceReference(TextBox.FontFamilyProperty, "UiFont");
            _input.KeyDown += async (_, e) =>
            {
                if (e.Key == Key.Enter) await LookupAsync();
            };
            inputHost.Child = _input;
            row.Children.Add(inputHost);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 0) };
            var paste = Ui.Button("粘贴", false, 12.5);
            paste.Margin = new Thickness(0, 0, 8, 0);
            paste.Click += (_, _) =>
            {
                var pasted = ClipboardHelper.TryGetText();
                if (!string.IsNullOrEmpty(pasted)) _input.Text = pasted.Trim();
            };
            buttons.Children.Add(paste);
            var query = Ui.Button("查询", true, 12.5);
            query.Click += async (_, _) => await LookupAsync();
            buttons.Children.Add(query);
            Grid.SetColumn(buttons, 1);
            row.Children.Add(buttons);
            stack.Children.Add(row);
            stack.Children.Add(Ui.Text("请输入纯数字 ID，例如 1370697782", 11.5, null, "TextTertiary"));
            card.Child = stack;
            root.Children.Add(card);

            _status = Ui.Text("", 12, null, "TextTertiary");
            _status.Margin = new Thickness(2, 12, 0, 8);
            root.Children.Add(_status);

            _result = new StackPanel();
            root.Children.Add(_result);

            scroll.Content = root;
            Content = scroll;
            Loaded += (_, _) => _input.Focus();
        }

        private async Task LookupAsync()
        {
            var raw = _input.Text?.Trim() ?? "";
            if (raw.Length == 0)
            {
                _status.Text = "请输入用户 ID";
                return;
            }
            if (!long.TryParse(raw, out var uid) || uid <= 0)
            {
                _status.Text = "只支持纯数字 ID";
                return;
            }
            _result.Children.Clear();
            _status.Text = "查询中…";
            try
            {
                var user = await Svc.Api.GetUserInfoAsync(uid);
                var imUser = await Svc.Api.GetImUserAsync(uid);
                if (user == null || user.Id == 0) user = imUser;
                else if (imUser != null && (string.IsNullOrWhiteSpace(user.ShowName) ||
                         user.ShowName.StartsWith("贴吧用户", StringComparison.Ordinal)))
                {
                    user.ShowName = imUser.ShowName;
                    if (!string.IsNullOrWhiteSpace(imUser.Portrait)) user.Portrait = imUser.Portrait;
                }
                if (user == null || user.Id == 0)
                {
                    var (profile, _) = await Svc.Api.GetProfileAsync(uid);
                    user = profile;
                }
                if (user == null || user.Id == 0)
                {
                    user = UserCache.Get(uid);
                }
                if (user == null || user.Id == 0)
                {
                    _status.Text = "没有找到该用户";
                    return;
                }
                if (user.FansNum == 0 && user.FollowNum == 0 && user.PostNum == 0 && user.ThreadNum == 0)
                {
                    var stats = await Svc.Api.GetUserStatsAsync(user.Id);
                    user.FansNum = stats.fans;
                    user.FollowNum = stats.follows;
                    user.ThreadNum = stats.threads;
                    user.PostNum = stats.posts;
                    if (user.AgreeNum == 0) user.AgreeNum = (int)Math.Min(int.MaxValue, stats.agree);
                }
                _status.Text = "";
                RenderUser(user);
            }
            catch (Exception ex)
            {
                _status.Text = "查询失败：" + ex.Message;
            }
        }

        private void RenderUser(UserInfo user)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18),
            };
            card.SetResourceReference(Border.BackgroundProperty, "Surface");
            card.SetResourceReference(Border.BorderBrushProperty, "Border");
            var stack = new StackPanel();

            var head = new StackPanel { Orientation = Orientation.Horizontal };
            var lookupAvatar = Ui.Avatar(user.AvatarUrl, 64, user.DisplayName);
            lookupAvatar.Cursor = Cursors.Hand;
            lookupAvatar.MouseLeftButtonUp += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
                {
                    Svc.Window?.OpenImageViewer(new System.Collections.Generic.List<string> { user.AvatarUrl }, 0);
                }
            };
            head.Children.Add(lookupAvatar);
            var info = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(Ui.Text(user.DisplayName, 20, FontWeights.SemiBold));
            var idText = "ID: " + (string.IsNullOrWhiteSpace(user.TiebaUid) ? user.Id.ToString() : user.TiebaUid);
            if (user.TbAge > 0) idText += " · 吧龄 " + user.TbAge.ToString("0.0") + " 年";
            var idRow = new StackPanel { Orientation = Orientation.Horizontal };
            idRow.Children.Add(Ui.Text(idText, 12, null, "TextTertiary"));
            var copyId = Ui.Text("复制", 11, FontWeights.Medium, "Accent");
            copyId.Cursor = Cursors.Hand;
            copyId.Margin = new Thickness(8, 0, 0, 0);
            copyId.MouseLeftButtonUp += async (_, _) =>
            {
                var ok = await ClipboardHelper.SetTextAsync(string.IsNullOrWhiteSpace(user.TiebaUid) ? user.Id.ToString() : user.TiebaUid);
                Svc.Window?.ShowToast(ok ? "复制成功" : "复制失败，剪贴板被占用");
            };
            idRow.Children.Add(copyId);
            info.Children.Add(idRow);
            if (!string.IsNullOrWhiteSpace(user.Intro))
            {
                var intro = Ui.Text(user.Intro, 12.5, null, "TextSecondary");
                intro.Margin = new Thickness(0, 4, 0, 0);
                info.Children.Add(intro);
            }
            head.Children.Add(info);
            stack.Children.Add(head);

            var stats = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 14, 0, 14) };
            stats.Children.Add(Stat(Ui.ShortNum(user.FollowNum), "关注"));
            stats.Children.Add(Stat(Ui.ShortNum(user.FansNum), "粉丝"));
            stats.Children.Add(Stat(Ui.ShortNum(user.ThreadNum), "主题"));
            stats.Children.Add(Stat(Ui.ShortNum(user.PostNum), "回帖"));
            stats.Children.Add(Stat(Ui.ShortNum(user.AgreeNum), "获赞"));
            stack.Children.Add(stats);

            stack.Children.Add(LinkRow("TA 的主题帖", "查看该吧友发表过的主题帖", () => Svc.Window?.OpenProfile(user.Id, user.DisplayName)));
            stack.Children.Add(LinkRow("TA 的回帖", "查看该吧友的历史回帖", () => Svc.Window?.OpenProfile(user.Id, user.DisplayName)));
            stack.Children.Add(LinkRow("TA 的关注", "查看该吧友关注的吧", () => Svc.Window?.OpenProfile(user.Id, user.DisplayName)));
            stack.Children.Add(LinkRow("TA 的粉丝", "查看该吧友的粉丝", () => Svc.Window?.OpenProfile(user.Id, user.DisplayName)));
            stack.Children.Add(LinkRow("TA 关注的吧", "查看该吧友关注过的贴吧", () => Svc.Window?.OpenProfile(user.Id, user.DisplayName)));
            stack.Children.Add(LinkRow("TA 的发言分析", "统计历史发言、分布与高频词", () =>
                Svc.Nav.Push(() => new UserAnalysisView(user.Id, user.DisplayName), "发言分析")));

            card.Child = stack;
            _result.Children.Add(card);
        }

        private static UIElement Stat(string value, string label)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 28, 0) };
            var number = Ui.Text(value, 16, FontWeights.SemiBold);
            number.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(number);
            var text = Ui.Text(label, 11.5, null, "TextTertiary");
            text.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(text);
            return stack;
        }

        private static UIElement LinkRow(string title, string desc, Action action)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 7),
                Cursor = Cursors.Hand,
            };
            border.SetResourceReference(Border.BackgroundProperty, "SurfaceAlt");
            border.SetResourceReference(Border.BorderBrushProperty, "Border");
            var stack = new StackPanel();
            stack.Children.Add(Ui.Text(title, 13.5, FontWeights.SemiBold));
            stack.Children.Add(Ui.Text(desc, 11.5, null, "TextTertiary"));
            border.Child = stack;
            border.MouseLeftButtonUp += (_, _) => action();
            return border;
        }
    }
}
