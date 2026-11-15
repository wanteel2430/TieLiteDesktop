using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Views
{
    /// <summary>“我关注的吧”列表页。</summary>
    public sealed class FollowedForumsView : UserControl, IRefreshable
    {
        private readonly StackPanel _root;
        private readonly StackPanel _list;
        private bool _loading;
        private bool _signing;
        private List<RecommendedForum> _forums = new();
        private Button _signButton;
        private Border _signPanel;
        private ProgressBar _signBar;
        private TextBlock _signStatus;

        public FollowedForumsView()
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            _root = new StackPanel
            {
                MaxWidth = 980,
                MinWidth = 640,
                Margin = new Thickness(30, 10, 30, 40),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var head = new Grid { Margin = new Thickness(2, 4, 2, 10) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(Ui.Text("我关注的吧", 20, FontWeights.SemiBold));
            _signButton = Ui.Button("一键签到", true, 12);
            _signButton.Click += async (_, _) => await SignAllAsync();
            Grid.SetColumn(_signButton, 1);
            head.Children.Add(_signButton);
            _root.Children.Add(head);

            var signStack = new StackPanel();
            _signStatus = Ui.Text("", 11.5, null, "TextSecondary");
            _signStatus.Margin = new Thickness(0, 0, 0, 6);
            signStack.Children.Add(_signStatus);
            _signBar = new ProgressBar
            {
                Height = 6,
                Minimum = 0,
                Maximum = 1,
                Value = 0,
            };
            _signBar.SetResourceReference(ProgressBar.ForegroundProperty, "Accent");
            signStack.Children.Add(_signBar);
            _signPanel = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 9, 12, 11),
                Margin = new Thickness(2, 0, 2, 10),
                Visibility = Visibility.Collapsed,
                Child = signStack,
            };
            _signPanel.SetResourceReference(BackgroundProperty, "SurfaceAlt");
            _signPanel.SetResourceReference(BorderBrushProperty, "Border");
            _signPanel.BorderThickness = new Thickness(1);
            _root.Children.Add(_signPanel);

            _list = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            _root.Children.Add(_list);
            scroll.Content = _root;
            Content = scroll;
            Loaded += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_loading) return;
            _loading = true;
            _list.Children.Clear();
            _list.Children.Add(HomeView.Skeleton(3));
            try
            {
                if (!Svc.Account.IsLoggedIn)
                {
                    _list.Children.Clear();
                    _list.Children.Add(HomeView.EmptyCard("登录后可查看我关注的吧"));
                    return;
                }
                var forums = await Svc.Api.GetFollowedForumsAsync();
                forums = forums.OrderByDescending(forum => IsPinned(forum.Name)).ToList();
                _forums = forums;
                _list.Children.Clear();
                if (forums.Count == 0)
                {
                    _list.Children.Add(HomeView.EmptyCard("还没有关注任何吧"));
                    return;
                }
                var details = await Task.WhenAll(forums.Select(async forum =>
                {
                    try
                    {
                        return await Svc.Api.GetForumDetailAsync(forum.Id);
                    }
                    catch
                    {
                        return null;
                    }
                }));
                for (var i = 0; i < forums.Count; i++)
                {
                    var detail = details[i];
                    if (detail != null)
                    {
                        if (detail.MemberNum > 0) forums[i].MemberNum = detail.MemberNum;
                        if (detail.ThreadNum > 0) forums[i].ThreadNum = detail.ThreadNum;
                        if (!string.IsNullOrWhiteSpace(detail.HotText)) forums[i].HotText = detail.HotText;
                    }
                }
                RenderForums();
            }
            catch (Exception ex)
            {
                _list.Children.Clear();
                _list.Children.Add(HomeView.ErrorCard(ex.Message, LoadAsync));
            }
            finally
            {
                _loading = false;
            }
        }

        private void RenderForums()
        {
            _forums = _forums.OrderByDescending(forum => IsPinned(forum.Name)).ToList();
            _list.Children.Clear();
            for (var i = 0; i < _forums.Count; i++)
            {
                var card = ForumRow(_forums[i]);
                _list.Children.Add(card);
                _ = AnimateRowAsync(card as FrameworkElement, i * 45);
            }
        }

        private async Task SignAllAsync()
        {
            if (_signing) return;
            if (!Svc.Account.IsLoggedIn)
            {
                Svc.Window?.ShowToast("请先登录后再签到");
                return;
            }
            var targets = _forums.Where(f => !f.IsSigned).ToList();
            if (targets.Count == 0)
            {
                Svc.Window?.ShowToast("所有关注的吧都已签到");
                return;
            }
            _signing = true;
            _signButton.IsEnabled = false;
            _signButton.Content = "签到中…";
            _signPanel.Visibility = Visibility.Visible;
            _signBar.Maximum = Math.Max(1, targets.Count);
            _signBar.Value = 0;
            _signStatus.Text = "准备签到 " + targets.Count + " 个吧…";
            var success = 0;
            var failed = 0;
            try
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    var forum = targets[i];
                    _signStatus.Text = "正在签到 " + (i + 1) + "/" + targets.Count + " · " + forum.Name;
                    try
                    {
                        await Svc.Api.SignForumAsync(forum.Id, forum.Name);
                        forum.IsSigned = true;
                        success++;
                    }
                    catch
                    {
                        failed++;
                    }
                    _signBar.Value = i + 1;
                    await Task.Delay(80);
                }
                _signStatus.Text = failed > 0
                    ? "签到完成：成功 " + success + " 个，失败 " + failed + " 个"
                    : "签到完成：成功 " + success + " 个吧";
                Svc.Window?.ShowToast(failed > 0
                    ? "签到完成：成功 " + success + " 个，失败 " + failed + " 个"
                    : "签到成功 " + success + " 个吧");
                await LoadAsync();
            }
            finally
            {
                _signing = false;
                _signButton.IsEnabled = true;
                _signButton.Content = "一键签到";
            }
        }

        private static async System.Threading.Tasks.Task AnimateRowAsync(FrameworkElement card, int delayMs)
        {
            if (card == null) return;
            card.Opacity = 0;
            if (delayMs > 0) await Task.Delay(Math.Min(delayMs, 450));
            Anim.FadeInUp(card, 0, 18, 380);
        }

        private UIElement ForumRow(RecommendedForum forum)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(12),
                Cursor = Cursors.Hand,
            };
            card.SetResourceReference(BackgroundProperty, "Surface");
            card.SetResourceReference(BorderBrushProperty, "Border");
            card.BorderThickness = new Thickness(1);
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var avatar = string.IsNullOrWhiteSpace(forum.Avatar)
                ? ""
                : forum.Avatar.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? forum.Avatar
                    : "https://tiebapic.baidu.com/forum/" + forum.Avatar;
            row.Children.Add(Ui.Avatar(avatar, 40, forum.Name));
            var text = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
            nameRow.Children.Add(Ui.Text(forum.Name, 14, FontWeights.SemiBold));
            if (forum.Level > 0)
            {
                var level = Ui.Chip("Lv." + forum.Level, "Accent", "AccentSoft");
                level.Margin = new Thickness(8, 0, 0, 0);
                nameRow.Children.Add(level);
            }
            var sign = Ui.Chip(forum.IsSigned ? "已签到" : "未签到", forum.IsSigned ? "Success" : "TextTertiary",
                forum.IsSigned ? "SuccessSoft" : "SurfaceAlt");
            sign.Margin = new Thickness(6, 0, 0, 0);
            if (IsPinned(forum.Name))
            {
                var pinned = Ui.Chip("置顶", "Accent", "AccentSoft");
                pinned.Margin = new Thickness(6, 0, 0, 0);
                nameRow.Children.Add(pinned);
            }
            nameRow.Children.Add(sign);
            text.Children.Add(nameRow);
            var meta = "关注 " + Ui.ShortNum(forum.MemberNum) + " · 帖子 " + Ui.ShortNum(forum.ThreadNum);
            if (!string.IsNullOrWhiteSpace(forum.HotText)) meta += " · 热度 " + forum.HotText;
            text.Children.Add(Ui.Text(meta, 11.5, null, "TextTertiary"));
            row.Children.Add(text);
            card.Child = row;
            card.MouseLeftButtonUp += (_, _) => Svc.Window?.OpenForum(forum.Name);
            var host = new Grid();
            host.Children.Add(card);
            var more = Ui.IconButton("\uE712", "更多操作", 26);
            more.HorizontalAlignment = HorizontalAlignment.Right;
            more.VerticalAlignment = VerticalAlignment.Top;
            more.Margin = new Thickness(0, 6, 10, 0);
            Panel.SetZIndex(more, 10);
            host.Children.Add(more);
            var pinnedNow = IsPinned(forum.Name);
            more.Click += (_, _) => ForumPinPopup.Show(more, pinnedNow, () =>
            {
                TogglePin(forum.Name);
                RenderForums();
            });
            return host;
        }

        private static bool IsPinned(string name) =>
            (Svc.Settings.PinnedForums ?? Array.Empty<string>()).Contains(name);

        private static void TogglePin(string name)
        {
            var values = (Svc.Settings.PinnedForums ?? Array.Empty<string>()).ToList();
            if (values.Contains(name)) values.Remove(name);
            else values.Insert(0, name);
            Svc.Settings.PinnedForums = values.ToArray();
            Svc.Settings.Save();
        }

        public async Task RefreshAsync() => await LoadAsync();
    }
}
